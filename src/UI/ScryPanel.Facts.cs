using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>What the game knows of the entry, the command to spawn it, and its details.</summary>
    internal static partial class ScryPanel
    {
        private static float FactsSection(Explorer explorer, Entry entry, float width, float y)
        {
            var built = Timing.Start();
            var facts = Facts.For(entry);
            Timing.Add("facts built", built);
            var places = entry.Kind != Kind.StatusEffect && (Locations.Now != Locations.State.Read || entry.FoundIn.Length > 0);
            if (facts.IsEmpty && !places) return y;

            y = SectionHeading("IN THE GAME", width, y, null, "facts");
            if (IsFolded("facts")) return y;

            if (facts.Description.Length > 0)
            {
                var height = Skin.Height(Skin.Wrap, facts.Description, width);
                GUI.Label(new Rect(0f, y, width, height), facts.Description, Skin.Wrap);
                y += height + U(10f);
            }

            // A two-column table: what it is on the left, its value on the right.
            var labelW = Mathf.Min(U(130f), width * 0.36f);
            foreach (var pair in facts.Pairs)
            {
                var valueW = width - labelW - U(10f);
                var labelH = Skin.Height(Skin.DimWrap, pair.Key, labelW);
                var height = Mathf.Max(U(20f), Mathf.Max(labelH, Skin.Height(Skin.Wrap, pair.Value, valueW)));
                GUI.Label(new Rect(0f, y, labelW, labelH), pair.Key, Skin.DimWrap);
                var valueRect = new Rect(labelW + U(10f), y, valueW, height);
                // A link only to what is in the catalog: a creature's own attack items are not.
                if (facts.Links.TryGetValue(pair.Key, out var link) && (link.StartsWith("se:", StringComparison.Ordinal) || InCatalog(explorer, link)))
                {
                    var linkW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, pair.Value) + U(4f));
                    var linkRect = new Rect(valueRect.x, valueRect.y, linkW, height);
                    LinkLabel(linkRect, pair.Value, Skin.Wrap, LinkText(KindOfKey(explorer, link), false));
                    if (linkRect.Contains(Event.current.mousePosition)) AskTip("link:" + link, "Go to " + ShownName(explorer, link, pair.Value));
                    if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none)) Go(explorer, link);
                }
                else
                {
                    GUI.Label(valueRect, pair.Value, Skin.Wrap);
                }
                y += height + U(6f);
            }

            foreach (var row in facts.Rows) y = FactRow(explorer, row, width, y);

            if (facts.Where.Count > 0)
            {
                y += U(6f);
                GUI.Label(new Rect(0f, y, width, U(20f)), facts.WhereTitle, Skin.DimLabel);
                y += U(24f);
                const int firstLines = 8;
                var lines = ShownOf("where", facts.Where.Count, firstLines);
                foreach (var source in facts.Where.Take(lines))
                {
                    // A line naming a prefab in the catalog is a chip that goes there; the rest is text.
                    if (string.IsNullOrEmpty(source.Prefab) || !InCatalog(explorer, source.Prefab))
                    {
                        var height = Skin.Height(Skin.Wrap, source.Text, width);
                        GUI.Label(new Rect(0f, y, width, height), source.Text, Skin.Wrap);
                        y += height + U(4f);
                        continue;
                    }

                    var icon = PrefabIcon(source.Prefab);
                    var textX = icon != null ? U(34f) : U(12f);
                    var textW = width - textX - U(10f);
                    var wrapped = SmallWrapped();
                    var chipH = Mathf.Max(U(30f), Skin.Height(wrapped, source.Text, textW) + U(10f));
                    var chip = new Rect(0f, y, width, chipH);
                    var hover = chip.Contains(Event.current.mousePosition);
                    var kind = KindOf(explorer, source.Prefab);
                    Skin.Box(chip, LinkFill(kind, hover));
                    if (icon != null) DrawSprite(icon, new Rect(U(6f), y + (chipH - U(22f)) / 2f, U(22f), U(22f)));
                    wrapped.normal.textColor = LinkText(kind, hover);
                    GUI.Label(new Rect(textX, y, textW, chipH), source.Text, wrapped);
                    if (hover) AskTip("src:" + source.Prefab, "Go to " + ShownName(explorer, source.Prefab, source.Prefab));
                    if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(source.Prefab))
                    {
                        _reveal = true;
                        _sideScroll = Vector2.zero;
                        _help = false;
                    }
                    y += chipH + U(5f);
                }
                var lx = 0f;
                MoreChip("where", facts.Where.Count, firstLines, width, U(26f), U(5f), ref lx, ref y);
                if (lx > 0f) y += U(26f) + U(5f);
            }

            if (places)
            {
                y += U(6f);
                y = FoundIn(explorer, entry, width, y);
            }

            // What it is used for, under a heading of its own; a long row (wood builds a hundred
            // pieces) shows its first few until asked for the rest.
            if (facts.UseRows.Count > 0)
            {
                y += U(6f);
                GUI.Label(new Rect(0f, y, width, U(20f)), "What it is used for", Skin.DimLabel);
                y += U(22f);
                foreach (var row in facts.UseRows) y = FactRow(explorer, row, width, y);
            }

            return y + U(14f);
        }

        /// <summary>
        /// A titled row of chips, each an item with its amount that goes to it when clicked; the
        /// title goes to the station it names. A long row shows its first chips and one for the rest.
        /// </summary>
        private static float FactRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            var key = "facts:" + row.Title;
            var count = ShownOf(key, row.Items.Count);
            y += U(6f);
            if (!string.IsNullOrEmpty(row.TitleLink) && InCatalog(explorer, row.TitleLink))
            {
                var titleW = Mathf.Min(width, Skin.Width(Skin.DimLabel, row.Title) + U(4f));
                var titleRect = new Rect(0f, y, titleW, U(20f));
                LinkLabel(titleRect, row.Title, Skin.DimLabel, LinkText(KindOfKey(explorer, row.TitleLink), false));
                if (titleRect.Contains(Event.current.mousePosition)) AskTip("station:" + row.TitleLink, "Go to " + ShownName(explorer, row.TitleLink, row.TitleLink));
                if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none)) Go(explorer, row.TitleLink);
            }
            else
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), row.Title, Skin.DimLabel);
            }
            y += U(24f);

            var x = 0f;
            var chipH = U(30f);
            for (var i = 0; i < count; i++)
            {
                var item = row.Items[i];
                var text = string.IsNullOrEmpty(item.Amount) ? item.Name : $"{item.Amount}  {item.Name}";
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(30f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += chipH + U(5f);
                }
                var chip = new Rect(x, y, w, chipH);
                if (OutOfSight(chip))
                {
                    x += w + U(6f);
                    continue;
                }
                var hover = chip.Contains(Event.current.mousePosition);
                var goes = !string.IsNullOrEmpty(item.Prefab) && InCatalog(explorer, item.Prefab);
                var kind = goes ? KindOf(explorer, item.Prefab) : null;
                Skin.PillBox(chip, goes ? LinkFill(kind, hover) : Skin.Raised);
                if (item.Icon != null) DrawSprite(item.Icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                var small = Skin.Small;
                var smallWas = small.normal.textColor;
                if (goes) small.normal.textColor = LinkText(kind, hover);
                GUI.Label(new Rect(chip.x + U(32f), chip.y, chip.width - U(36f), chip.height), text, small);
                small.normal.textColor = smallWas;

                // Clicking an ingredient or a drop goes to it.
                if (!string.IsNullOrEmpty(item.Prefab))
                {
                    if (hover) AskTip("goto:" + item.Prefab, $"Go to {item.Name}");
                    if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(item.Prefab))
                    {
                        _reveal = true;
                        _sideScroll = Vector2.zero;
                        _help = false;
                    }
                }
                x += w + U(6f);
            }
            MoreChip(key, row.Items.Count, FirstChips, width, chipH, U(5f), ref x, ref y);
            y += chipH + U(6f);

            return y;
        }

        private static int _commandAmount = 1;
        private static int _commandQuality = 1;
        private static Entry _commandFor;

        /// <summary>
        /// The game's own spawn command for the selection, ready to paste into the console: a
        /// creature at the level set above, an item given straight into the inventory.
        /// </summary>
        private static float Command(Explorer explorer, Entry entry, float width, float y)
        {
            if (entry != _commandFor)
            {
                _commandFor = entry;
                _commandAmount = 1;
                _commandQuality = 1;
            }

            var isItem = entry.Kind == Kind.Item;
            var level = isItem ? _commandQuality : entry.Kind == Kind.Creature ? explorer.Modifiers.Level : 1;
            var command = SpawnCommand.For(entry, _commandAmount, level, give: isItem);
            if (command == null) return y;

            y = SectionHeading(isItem ? "GIVE COMMAND" : "SPAWN COMMAND", width, y, null, "command");
            if (IsFolded("command")) return y;
            var rowH = U(28f);

            if (isItem)
            {
                GUI.Label(new Rect(0f, y, U(80f), rowH), "Amount", Skin.DimLabel);
                var x = U(80f);
                foreach (var step in new[] { -10, -1, 1, 10 })
                {
                    var text = step > 0 ? "+" + step : step.ToString(CultureInfo.InvariantCulture);
                    if (step == 1)
                    {
                        GUI.Label(new Rect(x, y, U(48f), rowH), _commandAmount.ToString(CultureInfo.InvariantCulture), Skin.Center);
                        x += U(52f);
                    }
                    if (GUI.Button(new Rect(x, y, U(40f), rowH), text, Skin.Segment)) _commandAmount = Mathf.Clamp(_commandAmount + step, 1, 999);
                    x += U(44f);
                }
                y += rowH + U(8f);

                var drop = (entry.Source as GameObject)?.GetComponent<ItemDrop>();
                var maxQuality = Mathf.Min(SpawnCommand.MaxItemQuality, drop?.m_itemData?.m_shared?.m_maxQuality ?? 1);
                if (maxQuality > 1)
                {
                    var names = Enumerable.Range(1, maxQuality).Select(q => q.ToString(CultureInfo.InvariantCulture)).ToList();
                    var chosen = Segments("Quality", names, _commandQuality - 1, width, U(80f), ref y);
                    if (chosen >= 0) _commandQuality = chosen + 1;
                }
            }

            var copyW = U(70f);
            var box = new Rect(0f, y, width - copyW - U(8f), U(30f));
            Skin.Box(box, new Color(0.055f, 0.060f, 0.073f, 1f), Skin.Outline);
            var commandRect = new Rect(box.x + U(10f), box.y, box.width - U(14f), box.height);
            if (!FitLabel(commandRect, command, Skin.Label, 10f) && commandRect.Contains(Event.current.mousePosition)) AskTip("command", command);
            if (GUI.Button(new Rect(box.xMax + U(8f), y, copyW, U(30f)), "Copy", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = command;
                Session.Say($"Copied \"{command}\". Paste it into the console (F5).");
            }
            y += U(36f);

            const string note = "Runs in the console with devcommands on, which the game only allows the host or a single-player world.";
            var height = Skin.Height(Skin.DimWrap, note, width);
            GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
            return y + height + U(14f);
        }

        private static readonly Dictionary<Entry, string> ComponentLists = new Dictionary<Entry, string>();
        private static Entry _usersFor;
        private static List<(string Key, string Text, string Tip, Action Click)> _users = new List<(string, string, string, Action)>();
        private static string _usersTitle = "";

        private static float Details(Explorer explorer, Entry entry, float width, float y)
        {
            y = SectionHeading("DETAILS", width, y, null, "details");
            if (IsFolded("details")) return y;

            var lines = new List<string> { "Prefab name: " + entry.Name };
            lines.Add("Origin: " + (entry.Origin == Origin.Vanilla ? "the game" : entry.Origin == Origin.Mod ? (entry.ModName.Length > 0 ? entry.ModName : "a mod, not named") : "unknown"));

            if (entry.ExtraLevels > 0) lines.Add($"Star looks: {entry.ExtraLevels}");



            if (entry.Source is GameObject prefab)
            {
                if (!ComponentLists.TryGetValue(entry, out var components))
                {
                    components = Components(prefab);
                    ComponentLists[entry] = components;
                }
                lines.Add("Made of: " + components);
            }

            foreach (var line in lines)
            {
                var height = Skin.Height(Skin.DimWrap, line, width);
                GUI.Label(new Rect(0f, y, width, height), line, Skin.DimWrap);
                y += height + U(4f);
            }

            if (entry.Biomes.Length > 0)
            {
                y = ChipRow("Biomes (search)", entry.Biomes.Select(b => new KeyValuePair<string, Action>(Knowledge.BiomeName(b), () => SearchFor(explorer, "biome:" + b.ToLowerInvariant()))), width, y);
            }
            if (entry.UsedBy.Count > 0 && EffectLinks.For(entry.Name).Count == 0)
            {
                // Every one, the first few until asked for the rest, as every long row shows; found
                // once for the entry shown, not for every event drawn.
                if (!ReferenceEquals(_usersFor, entry))
                {
                    _usersFor = entry;
                    _users = entry.UsedBy.Where(u => InCatalog(explorer, u))
                        .Select(n => (n, ShownName(explorer, n, n), (string)null, (Action)(() => Go(explorer, n)))).ToList();
                    _usersTitle = $"Played by ({_users.Count})";
                }
                if (_users.Count > 0) y = LinkItems(explorer, _usersTitle, _users, width, y);
            }

            // For finding out why part of a model does not show: every part the preview draws, in the
            // log. Offered only with the preview notes on, as it is there for looking into previews.
            if (Stage.Subject != null && Plugin.LogPreviews)
            {
                y += U(4f);
                var text = "Write its parts to the log";
                var w = Skin.Width(Skin.Chip, text) + U(8f);
                if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), U(26f)), text, Skin.Chip)) Session.Say(Stage.Dump());
                y += U(32f);
            }

            return y + U(6f);
        }

        /// <summary>
        /// Where it is found in the world's locations and dungeons: a button that reads them (they
        /// are read only when asked, since loading them takes a while), how far the reading has got,
        /// then the places, each a search for everything found there.
        /// </summary>
        private static float FoundIn(Explorer explorer, Entry entry, float width, float y)
        {
            switch (Locations.Now)
            {
                case Locations.State.Read:
                    if (entry.FoundIn.Length == 0) return y;
                    return ChipRow("Found in (search)", entry.FoundIn.Select(p => new KeyValuePair<string, Action>(p, () => SearchFor(explorer, "in:" + Places.NameOf(p).Replace(" ", "").ToLowerInvariant()))), width, y);

                case Locations.State.Reading:
                    // Not measured: the text changes every frame, and each would be kept.
                    const string stop = "Stop";
                    var stopW = Skin.Width(Skin.Chip, stop) + U(12f);
                    GUI.Label(new Rect(0f, y, width - stopW - U(8f), U(24f)), $"Reading locations and dungeons: {Locations.Done} of {Locations.Total}", Skin.DimLabel);
                    if (GUI.Button(new Rect(width - stopW, y, stopW, U(24f)), stop, Skin.Chip)) StopReadingLocations();
                    return y + U(30f);

                default:
                    const string text = "Find it in locations and dungeons";
                    var w = Skin.Width(Skin.Chip, text) + U(8f);
                    if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), U(26f)), text, Skin.Chip)) StartReadingLocations();
                    y += U(30f);
                    const string note = "Reads each of this world's locations and dungeon rooms once, in the background, over a few minutes.";
                    var height = Skin.Height(Skin.DimWrap, note, width);
                    GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
                    return y + height + U(6f);
            }
        }

        private const string LocationsButtonText = "Find in locations";
        private const string LocationsButtonTip = "Reads where things are found in this world's locations and dungeons, in the background, over a few minutes";

        /// <summary>Stops reading the locations, from any of the buttons that offer it, and says so.</summary>
        private static void StopReadingLocations()
        {
            var said = Locations.Stop();
            Session.Say(char.ToUpperInvariant(said[0]) + said.Substring(1));
        }

        /// <summary>Starts reading the locations, from any of the buttons that offer it, and says so.</summary>
        private static void StartReadingLocations()
        {
            var said = Locations.Start();
            Session.Say(char.ToUpperInvariant(said[0]) + said.Substring(1));
        }

        private static string Components(GameObject prefab)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                var name = component.GetType().Name;
                counts.TryGetValue(name, out var n);
                counts[name] = n + 1;
            }
            return string.Join(", ", counts.Select(p => p.Value > 1 ? $"{p.Key} ×{p.Value}" : p.Key));
        }

        private static GUIStyle _smallWrapped;
        private static GUIStyle _smallWrappedFrom;

        /// <summary>The small text style wrapping, made once for each time the styles are made, not for every chip drawn.</summary>
        private static GUIStyle SmallWrapped()
        {
            if (_smallWrapped == null || !ReferenceEquals(_smallWrappedFrom, Skin.Small))
            {
                _smallWrappedFrom = Skin.Small;
                _smallWrapped = new GUIStyle(Skin.Small) { wordWrap = true };
            }
            return _smallWrapped;
        }
    }
}
