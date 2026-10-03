using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for mods: those hooking into drops, loot and spawning are named,
    /// the mod report links their stations, and each mod's page, package and clues.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// The mods found hooking into drops, loot and spawning, and every hook looked at; a
        /// creature and a rock name the mods there are, and say nothing where there are none.
        /// </summary>
        private static IEnumerator HookingMods(Probe p)
        {
            foreach (HookedRule rule in Enum.GetValues(typeof(HookedRule)))
            {
                p.Note($"{rule}: {(ModHooks.Mods(rule).Count > 0 ? string.Join(", ", ModHooks.Mods(rule)) : "no mod")}");
            }
            foreach (var (mod, method, counted) in ModHooks.Seen) p.Note($"{mod} hooks {method}{(counted ? "" : ", not counted: its code names nothing that decides it there")}");
            p.Check(!ModHooks.AllMods.Contains("Scry"), "Scry itself is not named");

            var creature = Pick(Kind.Creature, "Greydwarf", "Boar");
            if (creature != null)
            {
                var note = HookNote(Facts.For(creature), HookedRule.Drops);
                var mods = ModHooks.Mods(HookedRule.Drops);
                p.Check(mods.Count == 0 ? note == null : note != null && mods.All(m => note.Contains(m)), $"{creature.Name} names the mods hooking into its drops", note ?? "no note");
            }
            var rock = Pick(Kind.Resource, "rock1_forest", "Rock_3", "MineRock_Copper", "Beech1");
            if (rock != null)
            {
                var note = HookNote(Facts.For(rock), HookedRule.Loot);
                var mods = ModHooks.Mods(HookedRule.Loot);
                p.Check(mods.Count == 0 ? note == null : note != null, $"{rock.Name} names the mods hooking into what it gives, if it gives anything", note ?? "no note");
            }

            // The rest, each on an entry that tells what the rule decides.
            foreach (var (rule, entry) in new[]
            {
                (HookedRule.Comfort, Pick(Kind.Piece, "bed", "piece_bed02")), (HookedRule.Wear, Pick(Kind.Piece, "wood_wall", "stone_wall_1x1")),
                (HookedRule.Smelting, Pick(Kind.Piece, "smelter")), (HookedRule.Cooking, Pick(Kind.Piece, "piece_cookingstation")),
                (HookedRule.Fermenting, Pick(Kind.Piece, "fermenter")), (HookedRule.Burning, Pick(Kind.Piece, "fire_pit", "piece_brazierfloor01")),
                (HookedRule.Producing, Pick(Kind.Piece, "piece_beehive")), (HookedRule.Crafting, Pick(Kind.Item, "AxeBronze", "SwordIron")),
                (HookedRule.ItemStats, Pick(Kind.Item, "SwordIron", "AxeBronze")), (HookedRule.Food, Pick(Kind.Item, "CookedMeat", "Raspberry")),
                (HookedRule.Growth, Pick(Kind.Piece, "sapling_carrot", "sapling_turnip")), (HookedRule.Taming, Pick(Kind.Creature, "Boar", "Wolf")),
                (HookedRule.Raids, X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && e.Name == "army_eikthyr")), (HookedRule.Trading, X.Catalog.FirstOrDefault(e => e.Source is GameObject g && g.GetComponent<Trader>() != null)),
                (HookedRule.Storage, Pick(Kind.Piece, "piece_chest_wood", "piece_chest")),
            })
            {
                if (entry == null) continue;
                var note = HookNote(Facts.For(entry), rule);
                var mods = ModHooks.Mods(rule);
                p.Check(mods.Count == 0 ? note == null : note != null && mods.All(m => note.Contains(m)), $"{entry.Name} names the mods hooking into {rule}", note ?? "no note");
            }
            yield break;
        }

        /// <summary>A page's note on the mods hooking into a rule, as its one line's hover tells it; null for none.</summary>
        private static string HookNote(Facts facts, HookedRule rule) =>
            facts.Hooks.Where(h => h.Rule == rule).Select(h => ModHookWords.Note(h.Rule, h.Mods)).FirstOrDefault();

        /// <summary>
        /// The mod report as a player opens it, written out whole, and checked against the game's
        /// own lists: every piece a mod's station is named by (Piece.m_craftingStation) and every
        /// recipe made at it must be linked to it.
        /// </summary>
        private static IEnumerator ModReportLinks(Probe p)
        {
            var report = ScryPanel.Report(X);
            var mods = X.Catalog.Where(e => e.Origin == Origin.Mod && e.Kind != Kind.Mod).Select(e => e.ModName.Length > 0 ? e.ModName : ModReportReader.UnknownMod).Distinct().ToList();
            var missing = mods.Where(m => !report.Any(r => r.Mod == m)).ToList();
            p.Check(missing.Count == 0, "every mod that adds anything is in the report", string.Join(", ", missing));
            foreach (var mod in report)
            {
                var gaps = new List<string>();
                if (mod.IdleStations.Count > 0) gaps.Add($"{Numbers.Count(mod.IdleStations.Count)} idle stations");
                if (mod.Sourceless.Count > 0) gaps.Add($"{Numbers.Count(mod.Sourceless.Count)} items with no source ({string.Join(", ", mod.Sourceless.Take(4).Select(e => e.Name))})");
                if (mod.Unspawned.Count > 0) gaps.Add($"{Numbers.Count(mod.Unspawned.Count)} creatures spawning nowhere seen ({string.Join(", ", mod.Unspawned.Take(4).Select(e => e.Name))})");
                if (mod.Unbuilt.Count > 0) gaps.Add($"{Numbers.Count(mod.Unbuilt.Count)} pieces in no build menu");
                p.Note($"{mod.Mod}: {ModReportWords.Counts(mod)}" + (mod.Hooks.Count > 0 ? $"; hooks into {ModReportWords.Hooks(mod.Hooks)}" : "")
                       + string.Concat(mod.Stations.Select(s => $"; station {s.Name}: {ModReportWords.Station(s)}"))
                       + string.Concat(mod.Tools.Select(t => $"; tool {t.Name}: {ModReportWords.Tool(t)}"))
                       + (gaps.Count > 0 ? "; not placed: " + string.Join(", ", gaps) : ""));
            }

            // Scry's links against the game's own lists, station by station.
            var linked = X.Catalog.Where(e => e.Stations != null).ToList();
            var wrong = new List<string>();
            foreach (var station in report.SelectMany(m => m.Stations))
            {
                var pieces = ZNetScene.instance.m_prefabs.Where(g => g != null && g.GetComponent<Piece>() is Piece piece && piece.m_craftingStation != null && piece.m_craftingStation.gameObject.name == station.Name).Select(g => g.name).ToList();
                var recipes = ObjectDB.instance.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null && r.m_craftingStation != null && r.m_craftingStation.gameObject.name == station.Name).Select(r => r.m_item.gameObject.name).Distinct().ToList();
                var builtNear = new HashSet<string>(linked.Where(e => e.Kind == Kind.Piece && e.Stations.Any(s => s.Name == station.Name)).Select(e => e.Name));
                var madeHere = new HashSet<string>(linked.Where(e => e.Kind != Kind.Piece && e.Stations.Any(s => s.Name == station.Name)).Select(e => e.Name));
                var unlinked = pieces.Where(n => !builtNear.Contains(n)).Concat(recipes.Where(n => !madeHere.Contains(n))).ToList();
                p.Note($"{station.Name}: the game names it for {Numbers.Count(pieces.Count)} pieces and {Numbers.Count(recipes.Count)} recipes; Scry links {Numbers.Count(builtNear.Count)} built near and {Numbers.Count(madeHere.Count)} made here");
                if (unlinked.Count > 0) wrong.Add($"{station.Name} misses {string.Join(", ", unlinked.Take(5))}");
            }
            p.Check(wrong.Count == 0, "every piece and recipe the game names a mod's station for is linked to it", string.Join("; ", wrong));

            // As a player opens it: the report in the list's place, drawn.
            ScryPanel.ShowModReport();
            var drawn = ScryPanel.Drawn(PanelPart.ModReport);
            yield return Until(() => ScryPanel.Drawn(PanelPart.ModReport) > drawn, 3);
            p.Check(ScryPanel.ModReportShown && ScryPanel.Drawn(PanelPart.ModReport) > drawn, "the report opens and draws in the panel");
            ScryPanel.HideModReport();
        }

        /// <summary>
        /// Every mod loaded has an entry of its own; the one adding the most tells what it adds
        /// kind by kind, and one of its entries names it as a page to go to.
        /// </summary>
        private static IEnumerator ModPages(Probe p)
        {
            var loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Select(i => i?.Metadata?.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            var pages = X.Catalog.Where(e => e.Kind == Kind.Mod).ToList();
            var missing = loaded.Where(n => !pages.Any(e => e.Name == n)).ToList();
            p.Check(missing.Count == 0, "every mod loaded has a page", string.Join(", ", missing.Take(5)));
            foreach (var group in pages.GroupBy(e => e.Group)) p.Note($"{group.Key}: {Numbers.Count(group.Count())}");

            var busiest = pages.OrderByDescending(e => X.Catalog.Count(c => c.ModName == e.Name && c.Kind != Kind.Mod)).FirstOrDefault();
            if (busiest == null || X.Catalog.All(c => c.ModName != busiest.Name || c.Kind == Kind.Mod))
            {
                p.Note("no mod adds anything, so no page tells what it adds");
                yield break;
            }
            var told = Facts.For(busiest);
            p.Check(Tells(told, "Adds") && told.Rows.Any(r => r.Title.StartsWith("Adds ", StringComparison.Ordinal)), $"{busiest.Name}'s page tells what it adds, kind by kind", string.Join("; ", told.Rows.Select(r => r.Title)));
            p.Note($"{busiest.Name}: {Pairs(told)}");
            var added = X.Catalog.First(c => c.ModName == busiest.Name && c.Kind != Kind.Mod);
            p.Check(X.Catalog.Any(e => e.Key == EntryKeys.For(Kind.Mod, added.ModName)), $"{added.Name} names its mod's page", added.ModName);
            p.Check(X.Jump(busiest.Key) && X.Selected == busiest, "the page can be gone to");
        }

        /// <summary>
        /// What mod managers put beside a mod is read: its package's description, author,
        /// website, icon and readme, each told on its page; and the mods each needs and works
        /// with are told both ways, as BepInEx and the packages declare them.
        /// </summary>
        private static IEnumerator ModPackages(Probe p)
        {
            var pages = X.Catalog.Where(e => e.Kind == Kind.Mod && e.Source is ModSource).ToList();
            var mods = pages.Select(e => (Entry: e, Mod: (ModSource)e.Source)).ToList();
            var packaged = mods.Where(m => m.Mod.Description.Length > 0 || m.Mod.IconPath.Length > 0 || m.Mod.ReadmePath.Length > 0).ToList();
            p.Note($"{Numbers.Count(mods.Count)} mods, {Numbers.Count(packaged.Count)} from a package: {Numbers.Count(mods.Count(m => m.Mod.Description.Length > 0))} described, {Numbers.Count(mods.Count(m => m.Mod.Author.Length > 0))} with an author, "
                   + $"{Numbers.Count(mods.Count(m => ModWords.IsWebsite(m.Mod.Website)))} with a website, {Numbers.Count(mods.Count(m => m.Mod.IconPath.Length > 0))} with an icon, {Numbers.Count(mods.Count(m => m.Mod.ReadmePath.Length > 0))} with a readme");
            if (packaged.Count == 0) p.Skip("no mod was installed by a mod manager");

            var iconless = mods.Where(m => m.Mod.IconPath.Length > 0 && !(m.Entry.Icon is Sprite)).Select(m => m.Mod.Name).ToList();
            p.Check(iconless.Count == 0, "every package's icon is read", string.Join(", ", iconless.Take(5)));

            var described = mods.FirstOrDefault(m => m.Mod.Description.Length > 0 && m.Mod.Author.Length > 0);
            if (described.Mod != null)
            {
                var told = Facts.For(described.Entry);
                p.Check(told.Description == described.Mod.Description && Value(told, "By") == described.Mod.Author, $"{described.Mod.Name}'s page tells what it is and who made it", $"{told.Description} / {Value(told, "By")}");
                if (ModWords.IsWebsite(described.Mod.Website))
                {
                    p.Check(told.Links.TryGetValue("Website", out var web) && web == Facts.OpenWebsite + described.Mod.Website, "its website opens in the browser");
                }
            }

            // Both ways: a mod one needs names it as needing it, and the same for working with.
            var byName = mods.ToDictionary(m => m.Mod.Name, m => m.Mod.Relations);
            var oneWay = new List<string>();
            foreach (var (_, mod) in mods)
            {
                foreach (var other in mod.Relations.Needs) if (!byName.TryGetValue(other, out var them) || !them.NeededBy.Contains(mod.Name)) oneWay.Add($"{mod.Name} needs {other}");
                foreach (var other in mod.Relations.WorksWith) if (!byName.TryGetValue(other, out var them) || !them.WorkedWithBy.Contains(mod.Name)) oneWay.Add($"{mod.Name} works with {other}");
            }
            p.Check(oneWay.Count == 0, "every tie between mods is told on both", string.Join("; ", oneWay.Take(5)));
            var declared = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Where(i => i?.Metadata != null && i.Dependencies != null)
                .SelectMany(i => i.Dependencies.Where(d => (d.Flags & BepInEx.BepInDependency.DependencyFlags.HardDependency) != 0 && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(d.DependencyGUID))
                    .Select(d => (Mod: i.Metadata.Name, Needs: BepInEx.Bootstrap.Chainloader.PluginInfos[d.DependencyGUID].Metadata.Name))).ToList();
            var missed = declared.Where(d => d.Mod != d.Needs && byName.TryGetValue(d.Mod, out var r) && !r.Needs.Contains(d.Needs)).Select(d => $"{d.Mod} needs {d.Needs}").ToList();
            p.Check(missed.Count == 0, $"every mod a mod declares it needs is told ({Numbers.Count(declared.Count)} declared)", string.Join("; ", missed.Take(5)));
            var mostNeeded = mods.OrderByDescending(m => m.Mod.Relations.NeededBy.Count).First();
            p.Note($"{mostNeeded.Mod.Name} is needed by {Numbers.Count(mostNeeded.Mod.Relations.NeededBy.Count)}; {Numbers.Count(mods.Count(m => m.Mod.Relations.WorksWith.Count > 0))} mods work with others when there; {Numbers.Count(mods.Count(m => m.Mod.Relations.WillNotRunWith.Count > 0))} will not run with some");

            var withReadme = mods.Where(m => m.Mod.ReadmePath.Length > 0).OrderBy(m => m.Mod.Name, StringComparer.Ordinal).Select(m => m.Entry).FirstOrDefault();
            if (withReadme == null)
            {
                p.Note("no mod has a readme");
                yield break;
            }
            var text = ScryPanel.ReadmeOf((ModSource)withReadme.Source);
            p.Note($"{withReadme.Name}'s readme: {Numbers.Count(text.Length)} characters, starting \"{text.Substring(0, Math.Min(60, text.Length)).Replace('\n', ' ')}\"");
            p.Check(text.Length > 0 && text.IndexOf("](", StringComparison.Ordinal) < 0 && text.IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0, "its readme reads as plain text");
            Select(withReadme);
            ScryPanel.ReadmeFolded = false;
            var drawn = ScryPanel.Drawn(PanelPart.Readme);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Readme) > drawn, 3);
            p.Check(ScryPanel.Drawn(PanelPart.Readme) > drawn, "its page shows it under README");
        }

        /// <summary>
        /// Which mod added what: how many things each clue named (Jotunn's registry, a mod's
        /// scripts, its bundles), what is left unnamed kind by kind, that nothing the game has of
        /// its own is put down to a mod, and that a recipe or conversion another mod added for an
        /// item says so on the item.
        /// </summary>
        private static IEnumerator ModClues(Probe p)
        {
            p.Note("named by " + (Knowledge.NamedBy.Count == 0 ? "nothing" : string.Join(", ", Knowledge.NamedBy.OrderByDescending(n => n.Value).Select(n => $"{n.Key}: {Numbers.Count(n.Value)}")))
                   + $"; recipes named {Numbers.Count(Knowledge.RecipesNamed)}, conversions named {Numbers.Count(Knowledge.ConversionsNamed)}");
            var unnamed = X.Catalog.Where(e => e.Origin == Origin.Mod && e.ModName.Length == 0 && e.Kind != Kind.Mod).ToList();
            foreach (var kind in unnamed.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()))
            {
                p.Note($"still unnamed, {Kinds.Label(kind.Key).ToLowerInvariant()}: {Numbers.Count(kind.Count())} ({string.Join(", ", kind.Take(8).Select(e => e.Name))})");
            }
            var wrong = X.Catalog.Where(e => e.Origin == Origin.Vanilla && e.ModName.Length > 0 && e.Kind != Kind.Mod).Select(e => $"{e.Name} ({e.ModName})").ToList();
            p.Check(wrong.Count == 0, "nothing of the game's own is put down to a mod", string.Join(", ", wrong.Take(8)));
            var named = X.Catalog.Where(e => e.Kind == Kind.Location && e.ModName.Length > 0).Select(e => $"{e.Name} ({e.ModName})").ToList();
            if (named.Count > 0) p.Note($"locations named for their mod: {string.Join(", ", named.Take(8))}");

            // A recipe another mod added for an item, told on the item.
            var db = ObjectDB.instance;
            var recipe = db == null ? null : db.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && Knowledge.RecipeMod(r.name).Length > 0
                                                                               && Knowledge.RecipeMod(r.name) != Knowledge.ModName(r.m_item.gameObject.name));
            if (recipe == null)
            {
                p.Note("no mod adds a recipe for another's item through Jotunn");
                yield break;
            }
            var item = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == recipe.m_item.gameObject.name);
            if (item == null) yield break;
            var told = Facts.For(item);
            p.Check(told.Rows.Any(r => r.Title.EndsWith(", added by " + Knowledge.RecipeMod(recipe.name), StringComparison.Ordinal)), $"{item.Name} says {Knowledge.RecipeMod(recipe.name)} added its recipe",
                string.Join("; ", told.Rows.Select(r => r.Title)));
        }
    }
}
