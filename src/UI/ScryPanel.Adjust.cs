using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The Adjust section (loudness, size, stars, wear, look, gear rows), just under what can be
    /// done with the selection, and the Animations section, which can hold hundreds of clips and
    /// so comes after what the entry is in the game.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// Whether the model can be adjusted: without the stage only while its copy is in the
        /// world. How loud previews play can always be, for sounds as for everything else.
        /// </summary>
        private static bool Adjustable(Entry entry, bool withStage, out bool staged)
        {
            staged = Stage.IsStaged(entry);
            var projectile = entry.Kind == Kind.Projectile;
            var modelInWorld = Previews.InWorld && Previews.IsModel(entry);
            return (withStage || modelInWorld || projectile) && (staged || projectile);
        }

        private static float Animations(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var model = Adjustable(entry, withStage, out var staged);
            var clips = model && staged && Kinds.OffersClips(entry.Kind) ? Previews.Clips() : NoClips;
            if (clips.Count == 0) return y;
            CountDrawn(PanelPart.Animations);
            y = Clips(explorer, clips, explorer.Modifiers, width, U(_compact ? 100f : 120f), y);
            return IsFolded("animations") ? y : y + U(10f);
        }

        private static float Adjust(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var modifiers = explorer.Modifiers;
            var projectile = entry.Kind == Kind.Projectile;
            var model = Adjustable(entry, withStage, out var staged);

            // Volume and size are the header's (ScryPanel.Slide); the section shows only with something else to adjust.
            // Under the strict rules only the stars the creature was seen with up close are offered, and it stands at no other.
            var starsOffered = model && entry.Kind == Kind.Creature && modifiers.MaxLevel > 1 ? StarsOffered(entry, modifiers) : NoLevels;
            if (starsOffered.Count > 0 && !starsOffered.Contains(modifiers.Level)) modifiers.Level = _explorer != null ? _explorer.StarLevelFor(entry.Key, modifiers.Level) : 1;
            var stars = starsOffered.Count > 1;

            // Looks that hinge on a thing not met, as a saddle, are left out, the first look shown.
            var looksHidden = entry.Source is GameObject hinged && _explorer != null && _explorer.HidesHinged(PrefabLooks.HingeOf(hinged));
            _looksHidden = looksHidden;
            if (looksHidden && modifiers.Look != 0) modifiers.Look = 0;
            // A gear look whose items were not all met is left out too, the first shown taking its place.
            var shown = LooksShown(entry, modifiers);
            if (!looksHidden && shown.Count > 0 && !shown.Contains(modifiers.Look)) modifiers.Look = shown[0];
            // A look row with one look left gives nothing to choose, and the section none to show.
            var looks = model && modifiers.LookAvailable && !looksHidden && shown.Count > 1;
            if (!stars && !(model && modifiers.WearAvailable) && !looks && !projectile) return y;

            // Reset is offered only while the section is open and something differs from the prefab.
            var open = !IsFolded("adjust");
            var changed = !modifiers.IsDefault || (entry.Source is GameObject rolled && Gear.LoadoutChanged(rolled));
            y = SectionHeading("ADJUST", width, y, open && changed ? () =>
            {
                modifiers.Reset();
                if (entry.Source is GameObject reset) Gear.ResetLoadout(reset);
                Previews.Rebuild();
            } : (Action)null, "adjust");
            var labelW = U(_compact ? 100f : 120f);

            if (open && stars)
            {
                // The names follow the stars offered, which the explorer keeps as one list while nothing changes.
                if (!ReferenceEquals(_starNamesOf, starsOffered))
                {
                    _starNames = new List<string>();
                    foreach (var level in starsOffered) _starNames.Add(CombatWords.StarChoice(level - 1));
                    _starNamesOf = starsOffered;
                }
                var names = _starNames;
                var chosen = Segments("Stars", names, starsOffered.IndexOf(modifiers.Level), width, labelW, ref y);
                if (chosen >= 0) modifiers.Level = starsOffered[chosen];
            }

            if (open && model && modifiers.WearAvailable)
            {
                var chosen = Segments("Wear", new List<string> { "New", "Worn", "Broken" }, (int)modifiers.Wear, width, labelW, ref y);
                if (chosen >= 0) modifiers.Wear = (Wear)chosen;
            }

            if (open && looks)
            {
                LookNamesShown.Clear();
                foreach (var look in shown) LookNamesShown.Add(modifiers.LookNames[look]);
                var chosen = Segments("Look", LookNamesShown, shown.IndexOf(modifiers.Look), width, labelW, ref y);
                if (chosen >= 0) modifiers.Look = shown[chosen];
                if (modifiers.Look > 0 && entry.Source is GameObject creature && PrefabLooks.IsGear(creature)) LoadoutRows(creature, modifiers.Look, width, labelW, ref y);
            }

            if (open && projectile)
            {
                Previews.ProjectileSpeed = SliderRow("Speed", ProjectileWords.Speed(Previews.ProjectileSpeed), Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            // Space after it only when open: a folded heading already leaves the same gap as every other.
            return open ? y + U(10f) : y;
        }

        private static readonly List<AnimationClip> NoClips = new List<AnimationClip>();

        private static readonly List<int> NoLevels = new List<int>();

        /// <summary>The levels the stage offers the creature shown at (<see cref="Explorer.StarsOffered"/>), every one without the compendium.</summary>
        private static List<int> StarsOffered(Entry entry, Modifiers modifiers)
        {
            if (_explorer != null) return _explorer.StarsOffered(entry);
            var all = new List<int>();
            for (var level = 1; level <= modifiers.MaxLevel; level++) all.Add(level);
            return all;
        }

        /// <summary>The levels the stage last offered the creature shown, for the self-test.</summary>
        [ForTest]
        public static List<int> StarsOfferedForTest => _explorer != null && _explorer.Selected != null ? _explorer.StarsOffered(_explorer.Selected) : NoLevels;

        private static bool _looksHidden;

        private static readonly List<string> LookNamesShown = new List<string>();

        /// <summary>The looks of what is shown the stage offers, worked out again only as what the character knows or the entry changes (<see cref="Explorer.LooksShown"/>).</summary>
        private static List<int> LooksShown(Entry entry, Modifiers modifiers)
        {
            var count = modifiers.LookNames?.Length ?? 0;
            var stamp = _explorer?.KnowStamp ?? -1;
            if (ReferenceEquals(entry, _looksOf) && stamp == _looksStamp && count == _looksCount) return _looksShown;
            _looksOf = entry;
            _looksStamp = stamp;
            _looksCount = count;
            var gear = entry.Source is GameObject creature && PrefabLooks.IsGear(creature) ? creature : null;
            _looksShown = _explorer != null && gear != null ? _explorer.LooksShown(count, look => PrefabGear.SetItems(gear, look)) : Enumerable.Range(0, count).ToList();
            return _looksShown;
        }

        private static Entry _looksOf;
        private static int _looksStamp = -1, _looksCount = -1;
        private static List<int> _looksShown = new List<int>();

        /// <summary>Lets go of the entry the looks were last worked out for, with the world.</summary>
        private static void ForgetLooks()
        {
            _looksOf = null;
            _looksStamp = -1;
            _looksCount = -1;
            _looksShown = new List<int>();
            LookNamesShown.Clear();
        }

        /// <summary>How many looks the stage offers what is shown, its gear's looks of unmet items left out, for the self-test.</summary>
        [ForTest]
        public static int LooksOfferedForTest => _looksShown.Count;

        /// <summary>Whether the looks of what is shown were last left out, as hinging on a thing not met, for the self-test.</summary>
        [ForTest]
        public static bool LooksHiddenForTest => _looksHidden;
        /// <summary>
        /// The weapon, shield and armour a creature can roll, one row each, and the extras it may
        /// be given, each put on or taken off. Only rows with a choice are shown. With the
        /// compendium on, a weapon is named as the creature's attack table names its attacks, any
        /// other item never met reads a smudge, and a row whose every item reads so is left out, as it
        /// gives nothing to choose by.
        /// </summary>
        private static void LoadoutRows(GameObject creature, int look, float width, float labelW, ref float y)
        {
            var loadout = Gear.LoadoutOf(creature);
            loadout.Carrying(PrefabGear.SetWeapons(creature, look));
            var rows = new[] { Loadout.Row.Holding, Loadout.Row.Weapon, Loadout.Row.Shield, Loadout.Row.Armour };
            var labels = new[] { "Holding", "Weapon", "Shield", "Armour" };
            LoadoutNames.Clear();
            // The rows' lists are kept by creature, look and what is known, as the rows draw every frame.
            var know = _explorer?.KnowStamp ?? 0;
            if (LoadoutLists.Count > 64) LoadoutLists.Clear();
            for (var i = 0; i < rows.Length; i++)
            {
                if (!loadout.Offered(rows[i])) continue;
                var options = loadout.Options(rows[i]);
                if (!LoadoutLists.TryGetValue((creature.name, look, know, i), out var lists))
                {
                    lists = LoadoutRowLists(creature.name, rows[i], options);
                    LoadoutLists[(creature.name, look, know, i)] = lists;
                }
                var (shown, names, hidden, offered) = lists;
                // A row left with nothing to choose is left out.
                if (shown.Count < options.Count && offered == 0) continue;
                if (shown.Count < options.Count && !shown.Contains(loadout.Chosen(rows[i])))
                {
                    loadout.Choose(rows[i], shown[0]);
                    Previews.Rebuild();
                }
                if (hidden > 0 && hidden == offered) continue;
                LoadoutNames.Add((labels[i], names));

                // A shield the weapon leaves no hand for is shown put away, and still chosen for later.
                var held = loadout.Held(rows[i]);
                var was = GUI.color;
                if (!held) GUI.color = Skin.Alpha(was, was.a * 0.4f);
                var top = y;
                var chosen = Segments(labels[i], names, shown.IndexOf(loadout.Chosen(rows[i])), width, labelW, ref y);
                GUI.color = was;
                if (!held && new Rect(0f, top, width, y - top).Contains(Event.current.mousePosition))
                {
                    AskTip("shield-away", "Not worn: the weapon takes both hands. Pick a one-handed weapon to wear it.");
                }
                if (chosen < 0 || shown[chosen] == loadout.Chosen(rows[i])) continue;
                loadout.Choose(rows[i], shown[chosen]);
                Previews.Rebuild();
            }

            // Under the strict rules an extra the creature was not seen carrying up close is not offered either.
            if (!ExtraLists.TryGetValue((creature.name, know), out var extraLists))
            {
                if (ExtraLists.Count > 64) ExtraLists.Clear();
                var shownNow = Enumerable.Range(0, loadout.Extras.Count).Where(i => _explorer == null || _explorer.WearsShown(creature.name, loadout.Extras[i].Name)).ToList();
                var extrasNow = shownNow.Select(i => _explorer != null && _explorer.NotMet(loadout.Extras[i].Name) ? CompendiumWords.Hidden : ItemName(loadout.Extras[i].Name)).ToList();
                extraLists = (shownNow, extrasNow);
                ExtraLists[(creature.name, know)] = extraLists;
            }
            var (shownExtras, extras) = extraLists;
            if (shownExtras.Count == 0) return;
            var clicked = Toggles("Extras", extras, i => loadout.ExtraOn(shownExtras[i]), width, labelW, ref y);
            if (clicked < 0) return;
            loadout.ToggleExtra(shownExtras[clicked]);
            Previews.Rebuild();
        }

        /// <summary>
        /// A loadout row's lists: which options are shown (under the strict rules those the creature was
        /// seen carrying up close, as the stage would not put the others on), their names, how many
        /// read as a smudge (an item never met, its prefab not told either, but a weapon, whose
        /// attacks the creature's table names), and how many options there are besides none.
        /// </summary>
        private static (List<int> Shown, List<string> Names, int Hidden, int Offered) LoadoutRowLists(string creature, Loadout.Row row, IReadOnlyList<string> options)
        {
            var shown = Enumerable.Range(0, options.Count).Where(n => string.IsNullOrEmpty(options[n]) || _explorer == null || _explorer.WearsShown(creature, options[n])).ToList();
            var names = shown.Select(n => ItemName(options[n])).ToList();
            var hidden = 0;
            for (var n = 0; n < names.Count; n++)
            {
                if (row != Loadout.Row.Weapon && !string.IsNullOrEmpty(options[shown[n]]) && _explorer != null && _explorer.NotMet(options[shown[n]]))
                {
                    names[n] = CompendiumWords.Hidden;
                    hidden++;
                }
                else if (names.Count(other => other == names[n]) > 1) names[n] = Naming.WithPrefab(names[n], options[shown[n]]);
            }
            return (shown, names, hidden, shown.Count(n => !string.IsNullOrEmpty(options[n])));
        }

        /// <summary>The loadout rows' lists by creature, look, what is known and row, and the extras' by creature and what is known.</summary>
        private static readonly Dictionary<(string Creature, int Look, int Know, int Row), (List<int> Shown, List<string> Names, int Hidden, int Offered)> LoadoutLists =
            new Dictionary<(string, int, int, int), (List<int>, List<string>, int, int)>();
        private static readonly Dictionary<(string Creature, int Know), (List<int> Shown, List<string> Names)> ExtraLists = new Dictionary<(string, int), (List<int>, List<string>)>();

        /// <summary>The stars' names, and the list of stars offered they were made for.</summary>
        private static List<int> _starNamesOf;
        private static List<string> _starNames;

        /// <summary>The gear rows last drawn, by their labels, and the names their choices showed, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<(string Row, List<string> Names)> LoadoutNamesForTest => LoadoutNames;

        private static readonly List<(string Row, List<string> Names)> LoadoutNames = new List<(string, List<string>)>();

        /// <summary>An item's name as the game shows it, or "Nothing" for an empty choice.</summary>
        private static string ItemName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return "Nothing";
            var shared = GamePrefabs.Item(prefabName).OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            var shown = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return shown.Length > 0 ? shown : prefabName;
        }
    }
}
