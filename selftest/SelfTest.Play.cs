using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for what play records and how a page under the strict rules reads:
    /// made-up events fed through the real hooks into a book of the test's own, the world and the
    /// character's book left alone; and a made-up character's pages read as a player sees them.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>A hit of the character's on the nearest loaded creature tells how its damage landed, then on its weak spot finds it; killed by it, it is counted; fought, the moment is kept.</summary>
        private static void CreatureHitTells(Probe p, CompendiumBook book, Player player, CompendiumBook real)
        {
            // A hit of the character's on the nearest loaded creature: how its damage landed, then its weak spot.
            var creature = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None).Where(c => c != null && !c.IsPlayer())
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (creature == null) p.Note("no creature is loaded to hit");
            else
            {
                var key = Utils.GetPrefabName(creature.gameObject);
                var hit = new HitData();
                hit.m_damage.m_blunt = 10f;
                hit.m_damage.m_fire = 10f;
                hit.SetAttacker(player);
                TogetherWatch.Hit(creature, hit);
                p.Check(ResistWords.Types.Any(t => book.Proved(key, EntryKeys.Damage(t))), $"a hit on {key} tells how its damage landed");
                var spots = creature.m_weakSpots;
                if (spots != null && spots.Length > 0 && spots[0] != null)
                {
                    hit.m_weakSpot = 0;
                    TogetherWatch.Hit(creature, hit);
                    p.Check(book.Proved(key, EntryKeys.WeakSpot(spots[0].gameObject.name)), $"a hit on its weak spot finds it ({spots[0].gameObject.name})");
                }
                else p.Note($"{key} has no weak spot to hit");

                // Killed by it: counted against it. Fought, the moment is kept whole, as play reads it.
                CompendiumKeeper.KilledBy(creature);
                p.Check(book.DeathsBy(key)?.Count == 1, $"a death dealt by {key} is counted against it");
                book.Now = real.Now;
                CompendiumKeeper.Fought(creature);
                var came = book.Came(key);
                p.Note($"fought {key}: " + (came == null ? "nothing kept" : $"day {Numbers.Count(came.Value.At.Day)}, {came.Value.At.Biome}, {came.Value.At.Time}, weather {came.Value.At.Weather ?? "none"}, in hand {came.Value.At.InHand ?? "nothing"}"));
                p.Check(came != null && came.Value.At.Day >= 0 && came.Value.At.Biome != null && came.Value.At.Time != DayPart.None && came.Value.At.Weather != null,
                    "coming to know it keeps the day, the biome, the time of day and the weather");
            }
        }

        /// <summary>A hit of the character's on the nearest loaded thing that breaks tells how its damage landed, as a creature's does.</summary>
        private static void ResourceHitTells(Probe p, CompendiumBook book, Player player)
        {
            var thing = UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None).Where(d => d != null)
                .OrderBy(d => (d.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (thing == null)
            {
                p.Note("nothing that breaks is loaded to hit");
                return;
            }
            var key = Utils.GetPrefabName(thing.gameObject);
            var hit = new HitData { m_toolTier = 10 };
            hit.m_damage.m_blunt = 10f;
            hit.m_damage.m_fire = 10f;
            hit.SetAttacker(player);
            ResourceHitWatch.Hit(thing, hit, thing.m_damages, thing.m_minToolTier);
            p.Check(ResistWords.Types.Any(t => book.Proved(key, EntryKeys.Damage(t))), $"a hit on {key}, a thing that breaks, tells how its damage landed");
        }

        /// <summary>
        /// One rule for every event: one beyond sight counts only where the character did it, and
        /// once however often it is told; one within reach under the ground, no line from the eye
        /// reaching it, is not seen.
        /// </summary>
        private static void DeathsOutOfSight(Probe p, CompendiumBook book, Player player, long[] me)
        {
            const string Made = "ScrySelfTestCreature";
            var far = player.transform.position + Vector3.right * (Sighting.PersonReach * 2f);
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Died, Made, far));
            p.Check(book.Tallied(Tally.Fell).Kills(Made) == 0, "a death beyond sight that the character had no hand in records nothing");
            var done = WorldEvents.Make(Happened.Died, Made, far, doers: me);
            CompendiumKeeper.Tell(done);
            CompendiumKeeper.Tell(done);
            p.Check(book.Tallied(Tally.Fell).Kills(Made) == 1, "one the character had a hand in counts, once however often it is told");
            const string Buried = "ScrySelfTestBuried";
            var under = player.transform.position + Vector3.down * 12f;
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Died, Buried, under));
            p.Check(book.Tallied(Tally.Fell).Kills(Buried) == 0 && !CompendiumSight.SeesDeed(under.x, under.y, under.z),
                "a death within reach under the ground, no line from the eye to it, records nothing");
        }

        /// <summary>Whether a thing near by is in sight of the eye, as a deed done to it must be to be seen (<see cref="CompendiumSight.SeesDeed"/>).</summary>
        private static bool InSight(Component thing)
        {
            var at = thing.transform.position;
            return CompendiumSight.SeesDeed(at.x, at.y, at.z);
        }

        /// <summary>
        /// A birth near by, of a tame creature that breeds and is not pregnant now: it shows how
        /// long it is pregnant, and the young it brought, which the game names by waking it last,
        /// told to the keeper as the birth names it (nothing is spawned, nor sent to other games),
        /// is known, seen born.
        /// </summary>
        private static void BirthSeen(Probe p, CompendiumBook book, Player player)
        {
            var parent = UnityEngine.Object.FindObjectsByType<Procreation>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= Sighting.PersonReach && !DeedWatch.Pregnant(c) && InSight(c));
            if (parent == null)
            {
                p.Note("no tame creature that breeds is near to see born");
                return;
            }
            var key = Utils.GetPrefabName(parent.gameObject);
            DeedWatch.Procreated(parent, wasPregnant: true);
            p.Check(book.Proved(key, EntryKeys.Did(Deed.Born)), "a birth seen near by shows how long it is pregnant");
            var young = parent.m_offspring != null ? Utils.GetPrefabName(parent.m_offspring) : null;
            if (young == null)
            {
                p.Note($"{key} names no young");
                return;
            }
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Born, key, parent.transform.position, with: young));
            p.Check(book.Level(young) == KnownLevel.Known && book.Came(young)?.Way == CameBy.Born, $"the young a birth near by brought ({young}) is known, seen born", $"{book.Level(young)}, {book.Came(young)?.Way.ToString() ?? "no way kept"}");
        }

        /// <summary>
        /// Made-up events through the real hooks, recorded into a book of the test's own
        /// (<see cref="CompendiumKeeper.RecordInto"/>): a hit of the character's on a loaded
        /// creature, on its weak spot, an armed creature's hit taken, a birth, a regrowth and a
        /// taming near by, a tree breaking by the character's hit with an item come to be, a death
        /// beyond sight done and not done and told twice, a map read. Nothing in the world
        /// changes, and neither the character's book nor the play counts do.
        /// </summary>
        private static IEnumerator CompendiumHooksFed(Probe p)
        {
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(player != null && real != null, "a character is played and its book read")) yield break;
            p.Note("what play recorded " + CompendiumKeeper.Play.Told());
            var version = real.Version;
            var pairs = real.PairsVersion;
            var learned = real.Learned;
            var counted = CompendiumKeeper.Play.Told();
            var book = new CompendiumBook();
            CompendiumKeeper.RecordInto(book);
            var me = new[] { ZNet.GetUID() };

            DeathsOutOfSight(p, book, player, me);

            CreatureHitTells(p, book, player, real);
            ResourceHitTells(p, book, player);

            // An armed creature's hit taken: its attack's damage found out.
            var armed = UnityEngine.Object.FindObjectsByType<Humanoid>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.IsPlayer() && h.GetCurrentWeapon()?.m_dropPrefab != null);
            if (armed == null) p.Note("no armed creature is loaded to be hit by");
            else
            {
                CompendiumKeeper.HitBy(armed);
                var weapon = armed.GetCurrentWeapon().m_dropPrefab.name;
                p.Check(book.FoundOutOf(Utils.GetPrefabName(armed.gameObject)) == weapon, $"a hit taken from {Utils.GetPrefabName(armed.gameObject)} finds out its attack ({weapon})");
            }

            // Deeds near by: a birth (one not pregnant now), a regrowth, a taming.
            var near = Sighting.PersonReach;
            BirthSeen(p, book, player);
            var bush = UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b != null && Vector3.Distance(b.transform.position, player.transform.position) <= near && InSight(b));
            if (bush == null) p.Note("no pickable is near to see grow back");
            else
            {
                DeedWatch.SetPicked(bush, wasPicked: true, picked: false);
                p.Check(book.Proved(Utils.GetPrefabName(bush.gameObject), EntryKeys.Did(Deed.GrewBack)), "a pickable seen growing back shows how long that takes");
            }
            // A taming: one creature near by told wild, then tame; every other as the game has it.
            var tame = UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None)
                .Select(t => t != null ? t.GetComponent<Character>() : null)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= near);
            if (tame == null) p.Note("no tameable creature is near to tame");
            else
            {
                CompendiumKeeper.Tamings(player, c => c != tame && c.IsTamed());
                CompendiumKeeper.Tamings(player, c => c == tame || c.IsTamed());
                p.Check(book.Proved(Utils.GetPrefabName(tame.gameObject), EntryKeys.Did(Deed.Tamed)), "a creature near by seen wild, then tame, shows how long taming takes");
            }
            p.Note("growing up is seen in play only, as a young creature has to be gone for it");

            // A tree breaking by the character's hit with an item come to be meanwhile: counted as what it gave, however far.
            var tree = Nearest<TreeBase>(player);
            var item = Nearest<ItemDrop>(player);
            if (tree == null || item == null) p.Note("no tree and item are loaded to count a break with");
            else
            {
                YieldWatch.Begin(tree, me);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var treeKey = Utils.GetPrefabName(tree.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(treeKey) == 1 && book.Witnessed(treeKey, Utils.GetPrefabName(item.gameObject)), $"{treeKey} breaking is counted with what came of it");
            }

            // A pick of the character's with an item come to be: counted as what it gave, no break.
            var picked = Nearest<Pickable>(player);
            if (picked == null || item == null) p.Note("no pickable and item are loaded to count a pick with");
            else
            {
                YieldWatch.Begin(picked, me);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var pickKey = Utils.GetPrefabName(picked.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(pickKey) == 1 && !book.Proved(pickKey, EntryKeys.Did(Deed.Broke)), $"picking {pickKey} is counted as what it gave, and is no break");

            }

            // A chest the world fills, opened: counted the first time only.
            var chest = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None)
                .Where(c => c != null && c.m_defaultItems?.m_drops != null && c.m_defaultItems.m_drops.Count > 0 && c.GetInventory() != null)
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (chest == null) p.Note("no chest the world fills is loaded to open");
            else
            {
                CompendiumKeeper.Opened(chest);
                CompendiumKeeper.Opened(chest);
                p.Check(book.Tallied(Tally.Held).Kills(Utils.GetPrefabName(chest.gameObject)) == 1, $"{Utils.GetPrefabName(chest.gameObject)} opened is counted the first time only");
            }

            // A spawner spawning near by, a hive collected from.
            var spawner = UnityEngine.Object.FindObjectsByType<SpawnArea>(FindObjectsSortMode.None)
                .FirstOrDefault(a => a != null && Vector3.Distance(a.transform.position, player.transform.position) <= near && InSight(a));
            if (spawner == null) p.Note("no spawner is near to see spawn");
            else
            {
                DeedWatch.Spawned(spawner, spawned: true);
                p.Check(book.Proved(Utils.GetPrefabName(spawner.gameObject), EntryKeys.Did(Deed.Spawned)), "a spawn seen near by shows its pool and pace");
            }
            var hive = Nearest<Beehive>(player);
            if (hive == null) p.Note("no beehive is loaded to collect from");
            else
            {
                DeedWatch.Collected(hive, player, held: true);
                p.Check(book.Proved(Utils.GetPrefabName(hive.gameObject), EntryKeys.Did(Deed.Collected)), "collecting from a beehive shows its pace");
            }

            // A map read keeps what it brings as found in the character's ground, never in its book.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null);
            if (place != null)
            {
                var level = book.Level(place.Key);
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                p.Check(book.Level(place.Key) == level && book.Found("self-test", WorldSource.Explored).Contains(place.Name), $"a map read keeps {place.Name} as found in your ground, the book as it was", $"{book.Level(place.Key)}");
            }

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && real.PairsVersion == pairs && real.Learned == learned, "the character's book was left alone");
            p.Check(CompendiumKeeper.Play.Told() == counted, "and so were the play counts");
            yield return null;
        }

        /// <summary>
        /// A made-up character's pages read as a player sees them under the strict rules: the
        /// troll it fought keeps back its health, its attacks' damage, its weak spot and how many
        /// of its hide fall, says so faintly, and its You box tells what is still to find out;
        /// killed and counted, they show and Learned says what was found out, while what its
        /// stars change waits for a starred one to fall. Its hide's page counts the troll's line,
        /// a boar's page keeps back how long taming takes until one is tamed, and a map read is
        /// counted in the notice. Each page is pictured as it reads.
        /// </summary>
        private static IEnumerator CompendiumStrictRead(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll");
            var hide = Pick(Kind.Item, "TrollHide");
            if (troll == null || hide == null)
            {
                p.Skip("there is no troll or troll hide");
                yield break;
            }
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(hide.Key, KnownLevel.Known);
            book.Witness(troll.Key, hide.Key);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(troll);
            // Its tiles are drawn only in view: the page is brought to them first.
            yield return null;
            yield return GoTo("overview");

            var facts = ScryPanel.Drawn(PanelPart.FactKeptBack);
            var still = ScryPanel.Drawn(PanelPart.StillToFindOut);
            var table = ScryPanel.Drawn(PanelPart.TableKeptBack);

            var tally = ScryPanel.Drawn(PanelPart.TallyCounted);
            yield return Until(() => ScryPanel.Drawn(PanelPart.StillToFindOut) > still, 3);
            p.Check(X.LeavingOut && X.KeepsBack(EntryKeys.Did(Deed.Killed)) && ScryPanel.Drawn(PanelPart.FactKeptBack) > facts, "the troll's health reads ???, what shows it on hover");
            p.Check(ScryPanel.Drawn(PanelPart.StillToFindOut) > still, "its You box tells what is still to find out");
            p.Check(ScryPanel.Drawn(PanelPart.TableKeptBack) > table, "its attacks keep back their damage, ??? in their cells");
            p.Check(ScryPanel.StillForTest.Any(l => l.StartsWith("Hit its weak spot", System.StringComparison.Ordinal)), "its weak spot is left out, the You box saying to hit it", string.Join("; ", ScryPanel.StillForTest));
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == CompendiumWords.Hidden && ScryPanel.Drawn(PanelPart.TallyCounted) > tally,
                "how many hides it drops reads ??? until counted, its hover saying what shows it");
            var before = ScryPanel.FindsForTest;
            p.Check(before.Of > before.Found && (ScryPanel.FindsChipForTest?.Contains(" of ") ?? false), "its title row tells how much of what it keeps back was found out", ScryPanel.FindsChipForTest ?? "no chip");
            yield return PictureOf(p, "overview", "strict Troll, nothing found out");
            ScryPanel.TablesDrawnForTest.Clear();
            ScryPanel.KeepTablesForTest = true;
            yield return PictureOf(p, "fight", "strict Troll's fight, nothing found out");
            ScryPanel.KeepTablesForTest = false;
            // Its damage kept back reads ???, its columns no wider than their headings need.
            var attacks = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
            if (attacks?.AttackCells != null && ScryPanel.TablesDrawnForTest.TryGetValue(attacks.Title, out var drawn))
            {
                var wide = attacks.AttackCells.Where(c => c < drawn.Columns.Length && drawn.Columns[c] > drawn.Headings[c] + 1f).Select(c => $"{attacks.Columns[c]} {Numbers.Fixed(drawn.Columns[c], 0)} for a heading of {Numbers.Fixed(drawn.Headings[c], 0)}").ToList();
                p.Check(wide.Count == 0, "a column of damage kept back is as wide as its heading, not its hidden figures", string.Join("; ", wide));
            }
            else p.Check(false, "the troll's attacks table was drawn in its fight");
            yield return PictureOf(p, "loot", "strict Troll's drops, nothing counted");

            // Killed and its loot counted: its health and its hide's count show, and Learned says what was found out.
            book.FoundOut(troll.Key, EntryKeys.Did(Deed.Killed));
            book.Dropped(troll.Name, new[] { (hide.Name, 2) });
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !X.KeepsBack(EntryKeys.Did(Deed.Killed)), 3);
            p.Check(!X.KeepsBack(EntryKeys.Did(Deed.Killed)), "killed, its health shows");
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == "2 (1 of 1)", "counted, its hide shows how many fell and how often", X.StrictAmount(Tally.Fell, troll.Name, hide.Name));
            p.Check(X.LearnedNote(troll) == "its health found out", "and Learned says what was found out", X.LearnedNote(troll));
            p.Check(X.KeepsBack(EntryKeys.Did(Deed.StarredFell)), "what its stars change waits for a starred one to fall");
            yield return Until(() => ScryPanel.FindsForTest.Found > before.Found, 3);
            p.Check(ScryPanel.FindsForTest.Found > before.Found, "and its title row counts more found out", $"{ScryPanel.FindsChipForTest} after {Numbers.Count(before.Found)} of {Numbers.Count(before.Of)}");
            yield return PictureOf(p, "overview", "strict Troll, killed");
            yield return PictureOf(p, "fight", "strict Troll's fight, killed");
            yield return PictureOf(p, "loot", "strict Troll's drops, counted");

            // The hide's page tells the troll's line with the character's count.
            X.Select(hide);
            var line = Facts.For(hide).Where.FirstOrDefault(s => s.Record?.Giver == troll.Name);
            var counted = X.StrictSourceLine(line.Record);
            p.Check(counted == $"Dropped by {line.Record?.Shown}, 2 (1 of 1 kill)", "the hide's page counts the troll's line", counted);
            // Drawn as the troll's own chip, as under the loose rules, its count after its name.
            var chip = ScryPanel.WhereRowsForTest(X, hide).SelectMany(r => r.Items).FirstOrDefault(i => i.Prefab == troll.Name);
            p.Check(chip.Amount == "2 (1 of 1 kill)", "and draws the troll as a chip with that count", chip.Name + " " + chip.Amount);
            yield return StrictGameShownRead(p, book, view, hide);
            // A plain word of a text is no name kept back, though an effect never met goes by it ("Fire").
            var fires = ScryPanel.AsDrawn(X, "fires Troll throw projectile");
            p.Check(!fires.StartsWith(CompendiumWords.Hidden, System.StringComparison.Ordinal), "a word of a text is no name kept back: \"fires\" stays", fires);
            yield return PictureOf(p, "from", "strict TrollHide, where it comes from");

            // A tame creature's page keeps back how long taming takes until one is tamed.
            var boar = Pick(Kind.Creature, "Boar");
            var tamed = EntryKeys.Did(Deed.Tamed);
            if (boar != null && Facts.For(boar).Proofs.TryGetValue("Tames in", out var proof) && proof == tamed)
            {
                book.Raise(boar.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                X.Select(boar);
                yield return null;
                p.Check(X.KeepsBack(tamed), "a boar's page keeps back how long taming takes");
                yield return PictureOf(p, "taming", "strict Boar's taming, none tamed");
                book.FoundOut(boar.Key, tamed);
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => !X.KeepsBack(tamed), 3);
                p.Check(!X.KeepsBack(tamed), "until one is tamed");
                yield return PictureOf(p, "taming", "strict Boar's taming, one tamed");
            }
            else p.Note("no boar whose page tells how long taming takes");

            // A map read shows its places as Ground you explored says, seen as it starts, marked as that source's.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null && view.Of(e) < KnownLevel.Seen);
            if (place != null)
            {
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                view.InWorld("self-test");
                view.Tell(book, CompendiumRule.Strict);
                p.Check(view.Of(place) == KnownLevel.Seen && view.ByWorld(place) == WorldSource.Explored && book.Level(place.Key) < KnownLevel.Seen,
                    "a place the map brings shows as seen from your explored ground, not written into the book", $"{place.Name}: {view.Of(place)}, book {book.Level(place.Key)}");
            }
            Session.ShowKnownInstead(null);
        }

        /// <summary>
        /// What the game's menus show comes with the thing under the strict rules: a troll hide's
        /// uses name a known thing made of it and a wood wall's building its cost and tool, nothing paired.
        /// </summary>
        private static IEnumerator StrictGameShownRead(Probe p, CompendiumBook book, CompendiumView view, Entry hide)
        {
            // What it is used for is the game's crafting menu: its rows name what was met, no pair asked.
            var uses = Facts.For(hide).UseRows;
            var made = uses.SelectMany(r => r.Items).Select(i => i.Prefab).FirstOrDefault(prefab => !string.IsNullOrEmpty(prefab) && X.Find(prefab) != null);
            if (made != null)
            {
                book.Raise(made, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => !X.LeavesOut(made, gameShown: true), 3);
                p.Check(uses.All(r => r.GameShown) && !X.LeavesOut(made, gameShown: true) && X.LeavesOut(made), $"what the hide is used for names {made}, known and never paired, as the crafting menu shows it");
            }
            else p.Note("nothing in the catalog is made of troll hide");
            // A piece's cost and tool come with the piece, as the hammer's menu shows them.
            var wall = Pick(Kind.Piece, "wood_wall");
            var hammer = Pick(Kind.Item, "Hammer");
            var wood = Pick(Kind.Item, "Wood");
            if (wall != null && hammer != null && wood != null)
            {
                book.Raise(wall.Key, KnownLevel.Known);
                book.Raise(hammer.Key, KnownLevel.Known);
                book.Raise(wood.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                X.Select(wall);
                yield return Until(() => X.Selected == wall && !X.LeavesOut(hammer.Key, gameShown: true), 3);
                var building = Facts.For(wall);
                p.Check(building.GameShown.Contains("Built with") && building.Rows.Any(r => r.GameShown && r.Items.Any(i => i.Prefab == wood.Name)) && !X.LeavesOut(hammer.Key, gameShown: true) && !X.LeavesOut(wood.Name, gameShown: true),
                    "a wood wall's cost and tool come with it under the strict rules, never paired");
                yield return PictureOf(p, "building", "strict wood wall's building, nothing paired");
                X.Select(hide);
                yield return null;
            }
            else p.Note("no wood wall, hammer or wood to try a piece's building with");
        }
    }
}
