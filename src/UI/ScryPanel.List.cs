using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The catalog list, its rows and icons, and the help card over it.</summary>
    internal static partial class ScryPanel
    {
        private static int _rowsInView = 10;

        /// <summary>Where the list is scrolled to.</summary>
        private static Vector2 _listScroll;

        /// <summary>Whether the list scrolls the selected entry into sight at its next repaint.</summary>
        private static bool _reveal;

        /// <summary>A card shown in the list's place: the search's help, the mod report or what is off.</summary>
        private enum ListCard { None, Help, ModReport, Off }

        private static ListCard _card;

        private static void ShowCard(ListCard card) => _card = card;

        /// <summary>Shows a card in the list's place, or puts it away if it is the one showing.</summary>
        private static void ToggleCard(ListCard card) => _card = _card == card ? ListCard.None : card;

        /// <summary>Puts a card away if it is the one showing.</summary>
        private static void CloseCard(ListCard card)
        {
            if (_card == card) _card = ListCard.None;
        }

        /// <summary>Puts away whatever card is showing, for the list.</summary>
        private static void CloseCards() => _card = ListCard.None;

        /// <summary>Scrolls the selected entry into sight at the list's next repaint.</summary>
        private static void RevealSelected() => _reveal = true;

        private static void ListFromTop() => _listScroll = Vector2.zero;

        /// <summary>
        /// The list was filtered anew: it shows from its top, and the search's help and the mod
        /// report give way to it; the notice of what is off stays.
        /// </summary>
        private static void ListFiltered()
        {
            ListFromTop();
            CloseCard(ListCard.Help);
            CloseCard(ListCard.ModReport);
        }

        /// <summary>Went to an entry: the list shows it, its details start at their top, and the search's help gives way.</summary>
        private static void AfterGoing()
        {
            RevealSelected();
            DetailsFromTop();
            CloseCard(ListCard.Help);
        }

        private static readonly string[][] HelpLines =
        {
            new[] { "troll", "Names containing it, in the game's words or the prefab's. Best matches first." },
            new[] { "troll hat", "Every word has to match." },
            new[] { "-ragdoll", "A minus leaves out whatever matches." },
            new[] { "kind:creature", Kinds.HelpLine() },
            new[] { "has:aoe", "Prefabs with a part of that type, such as has:light, has:pickable, has:fireplace." },
            new[] { "biome:swamp", "What spawns or grows in that biome." },
            new[] { "in:crypt", "What is found in a location or dungeon, once they are read with Read all locations (at the top of the panel, or below)." },
            new[] { "mod:epic", "What a mod added, by the start or any part of its name." },
            new[] { "playedby:troll", "The sounds and effects a prefab plays." },
            new[] { "is:boss", SearchFlags.HelpLine() },
            new[] { "station:forge3", "What is made at that station, here what a forge at level 3 can make. station:forge for any level, station:hand for what needs none." },
            new[] { "biome:swamp,plains", "A comma reads as or, in any term." },
            new[] { "-has:ragdoll kind:c", "Terms combine, can be left out with a minus, and can be shortened." },
            new[] { "Tab", "Completes the word being typed with a term or a value the catalog holds, as the list under the search suggests. Tab again for the next, Shift+Tab for the one before; Enter takes the marked one." },
        };

        /// <summary>Whether the search's help is shown, and showing or hiding it, for the self-test.</summary>
        public static bool HelpShown => _card == ListCard.Help;

        public static void ShowHelp(bool shown)
        {
            if (shown) ShowCard(ListCard.Help);
            else CloseCard(ListCard.Help);
        }

        /// <summary>How to search, shown in place of the list while the ? button is on.</summary>
        private static Vector2 _helpScroll;
        private static float _helpHeight;

        private static void HelpCard(Rect rect)
        {
            var body = BeginCard(rect, ref _helpScroll, _helpHeight, "How to search");
            var x = body.X;
            var width = body.Width;
            var y = body.Y;

            // Side by side when there is room, the example above its meaning when not.
            var stacked = width < U(420f);
            var keyW = stacked ? width : Mathf.Min(U(190f), width * 0.42f);
            foreach (var line in HelpLines)
            {
                var boxW = Mathf.Min(keyW, Skin.Width(Skin.Label, line[0]) + U(16f));
                Skin.Box(new Rect(x - U(4f), y - U(1f), boxW, U(24f)), Skin.Raised);
                GUI.Label(new Rect(x + U(4f), y, boxW - U(8f), U(22f)), line[0], Skin.Label);

                var textX = stacked ? x : x + keyW + U(10f);
                var textY = stacked ? y + U(28f) : y + U(2f);
                var textW = stacked ? width : width - keyW - U(10f);
                var height = Skin.Height(Skin.DimWrap, line[1], textW);
                GUI.Label(new Rect(textX, textY, textW, height), line[1], Skin.DimWrap);
                y = Mathf.Max(y + U(24f), textY + height) + U(12f);
            }

            const string more = "The star shows only favourites, Recent what you looked at last. The kind tabs, Game or Mods, and all of the above work together.";
            var moreH = Skin.Height(Skin.DimWrap, more, width);
            GUI.Label(new Rect(x, y, width, moreH), more, Skin.DimWrap);
            if (Event.current.type == EventType.Repaint)
            {
                _helpHeight = y + moreH + U(12f);
                CountDrawn(PanelPart.Help);
            }

            if (EndCard(rect, out var close)) CloseCard(ListCard.Help);

            // in: needs the locations read; offered beside Close until they are.
            ReadLocationsButton(rect, close, "locations-help");
        }

        /// <summary>Whether the search asks what is in a place, with in: (not with a minus, which asks for the rest).</summary>
        private static bool SearchesPlaces(string text) => SearchHelp.Asks(text, "in");

        private static void NoPlacesYet(Rect inner)
        {
            var reading = Locations.Now == Locations.State.Reading;
            var message = LocationWords.NoPlacesYet(reading);
            var textW = Mathf.Min(inner.width - U(24f), U(420f));
            var height = Skin.Height(Skin.CenterDim, message, textW);
            var top = inner.y + Mathf.Max(U(20f), (inner.height - height - U(44f)) / 2f);
            GUI.Label(new Rect(inner.x + (inner.width - textW) / 2f, top, textW, height), message, Skin.CenterDim);

            var label = reading ? "Stop reading" : LocationsButtonText;
            var w = Skin.Width(Skin.Button, label) + U(10f);
            var button = new Rect(inner.x + (inner.width - w) / 2f, top + height + U(12f), w, U(30f));
            if (GUI.Button(button, label, Skin.Button))
            {
                if (reading) StopReadingLocations();
                else StartReadingLocations();
            }
        }

        private static void List(Explorer explorer, Rect rect)
        {
            if (_testDetails)
            {
                TestCard(rect);
                return;
            }
            if (_card == ListCard.Help)
            {
                HelpCard(rect);
                return;
            }
            if (_card == ListCard.ModReport)
            {
                ModReportCard(explorer, rect);
                return;
            }
            if (_card == ListCard.Off)
            {
                OffCard(rect);
                return;
            }

            Skin.Box(rect, Skin.Panel);
            var inner = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - U(12f));
            var results = explorer.Results;
            var rowH = U(34f);
            _rowsInView = Mathf.Max(1, Mathf.FloorToInt(inner.height / rowH));

            if (results.Count == 0)
            {
                // A search for what is in a place finds nothing until the places are read: say so, and offer it.
                if (Locations.Now != Locations.State.Read && SearchesPlaces(explorer.Text))
                {
                    NoPlacesYet(inner);
                    return;
                }
                var message = ListWords.Nothing(explorer.FavouritesOnly && explorer.Favourites.Keys.Count == 0, explorer.RecentOnly && explorer.RecentKeys.Count == 0);
                GUI.Label(inner, message, Skin.CenterDim);
                return;
            }

            // Only what mods added: how many mods, and their report.
            if (explorer.Origin == OriginFilter.Mods)
            {
                inner = ModsBar(explorer, inner);
                _rowsInView = Mathf.Max(1, Mathf.FloorToInt(inner.height / rowH));
            }

            // The picked tab has nothing for the search: say so above every kind's matches.
            if (explorer.ShowingEveryKind && explorer.KindFilter is Kind picked)
            {
                var noteH = U(26f);
                GUI.Label(new Rect(inner.x + U(10f), inner.y, inner.width - U(20f), noteH),
                    ListWords.NothingIn(Kinds.Label(picked), results.Count), Skin.DimLabel);
                inner = new Rect(inner.x, inner.y + noteH, inner.width, inner.height - noteH);
                _rowsInView = Mathf.Max(1, Mathf.FloorToInt(inner.height / rowH));
            }

            var built = Timing.Start();
            var rows = ListRows(explorer);
            Timing.Add("list rows", built);
            if (_reveal && Event.current.type == EventType.Repaint)
            {
                _reveal = false;
                var index = explorer.SelectedIndex;
                if (index >= 0 && index < _rowOfEntry.Count && _rowOfEntry[index] >= 0)
                {
                    var top = _rowOfEntry[index] * rowH;
                    if (top < _listScroll.y) _listScroll.y = top;
                    else if (top + rowH > _listScroll.y + inner.height) _listScroll.y = top + rowH - inner.height;
                }
            }

            var view = new Rect(0f, 0f, inner.width - U(14f), rows.Count * rowH);
            _listScroll = GUI.BeginScrollView(inner, _listScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var first = Mathf.Max(0, Mathf.FloorToInt(_listScroll.y / rowH));
            var last = Mathf.Min(rows.Count - 1, first + _rowsInView + 1);
            var visible = new Rect(0f, _listScroll.y, view.width, inner.height);
            for (var i = first; i <= last; i++)
            {
                var at = new Rect(0f, i * rowH, view.width, rowH);
                var row = rows[i];
                if (row.Entry >= 0) Row(explorer, results[row.Entry], at, row.Entry == explorer.SelectedIndex, visible);
                else Heading(explorer, row, at, visible);
            }

            GUI.EndScrollView();
        }

        /// <summary>A row of the list: an entry (its place in the results), or the heading of the group that follows.</summary>
        private struct ListRow
        {
            public int Entry;
            public string Heading;
            public string Group;
            public bool Folded;
        }

        /// <summary>The groups folded away, by kind and name; they stay folded while the game runs.</summary>
        private static readonly HashSet<string> FoldedGroups = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The selection a folded group was last opened for, so one is opened once, when the selection moves into it.</summary>
        private static Entry _foldChecked;

        private static string FoldKey(Explorer explorer, string group) => explorer.KindFilter + "|" + group;

        /// <summary>Whether the list is one kind's, in its groups; not while every kind is shown for an empty tab.</summary>
        private static bool Grouped(Explorer explorer) => explorer.KindFilter != null && !explorer.ShowingEveryKind;

        /// <summary>A group's heading, which folds the group away or opens it again when clicked.</summary>
        private static void Heading(Explorer explorer, ListRow row, Rect at, Rect visible)
        {
            var hover = at.Contains(Event.current.mousePosition) && visible.Contains(Event.current.mousePosition) && _drag == Drag.None;
            if (hover) Skin.Box(new Rect(at.x + U(2f), at.y + U(1f), at.width - U(4f), at.height - U(2f)), Skin.Hover);
            var text = ListWords.Group(row.Heading, row.Folded);
            GUI.Label(new Rect(at.x + U(10f), at.y + U(8f), at.width - U(20f), at.height - U(8f)), text, Skin.DimLabel);
            if (hover) AskTip("group:" + row.Group, ListWords.GroupTip(row.Folded));
            if (GUI.Button(at, GUIContent.none, GUIStyle.none))
            {
                var key = FoldKey(explorer, row.Group);
                if (!FoldedGroups.Remove(key)) FoldedGroups.Add(key);
                _rowsFor = null;
            }
        }

        private static readonly List<ListRow> _listRows = new List<ListRow>();

        /// <summary>Whether anything in the list has an icon of its own, so the icon column is kept.</summary>
        private static bool _listHasIcons;

        private static bool HasIcon(Entry entry) => entry.Icon is Sprite sprite && sprite != null;
        private static readonly List<int> _rowOfEntry = new List<int>();
        private static IReadOnlyList<Entry> _rowsFor;

        /// <summary>
        /// The list's rows: on a kind's tab, each group (items by their type, resources by how
        /// they are gathered) under a heading with its count, unless Recent lists them newest
        /// first; otherwise the entries alone.
        /// Worked out again only when the results change.
        /// </summary>
        private static List<ListRow> ListRows(Explorer explorer)
        {
            var results = explorer.Results;

            // A selection that lands in a folded group, by a link or a search, opens it; folding
            // the group the selection is in afterwards keeps it folded.
            var selected = explorer.SelectedIndex;
            if (!ReferenceEquals(explorer.Selected, _foldChecked))
            {
                _foldChecked = explorer.Selected;
                if (selected >= 0 && selected < results.Count && Grouped(explorer) && FoldedGroups.Remove(FoldKey(explorer, results[selected].Group))) _rowsFor = null;
            }
            if (ReferenceEquals(results, _rowsFor)) return _listRows;
            _rowsFor = results;
            _listRows.Clear();
            _rowOfEntry.Clear();
            _listHasIcons = false;
            foreach (var entry in results)
            {
                if (!HasIcon(entry)) continue;
                _listHasIcons = true;
                break;
            }

            // Recent lists newest first, not by group, so it has no headings.
            var grouped = Grouped(explorer) && !explorer.RecentOnly && results.Any(e => e.Group.Length > 0);
            for (var i = 0; i < results.Count; i++)
            {
                var folded = false;
                if (grouped)
                {
                    folded = FoldedGroups.Contains(FoldKey(explorer, results[i].Group));
                    if (i == 0 || results[i].Group != results[i - 1].Group)
                    {
                        var count = 1;
                        while (i + count < results.Count && results[i + count].Group == results[i].Group) count++;
                        var name = results[i].Group.Length > 0 ? results[i].Group : "Ungrouped";
                        _listRows.Add(new ListRow { Entry = -1, Heading = PanelWords.Heading(name, count), Group = results[i].Group, Folded = folded });
                    }
                }
                if (folded)
                {
                    _rowOfEntry.Add(-1);
                    continue;
                }
                _rowOfEntry.Add(_listRows.Count);
                _listRows.Add(new ListRow { Entry = i });
            }
            return _listRows;
        }

        private static void Row(Explorer explorer, Entry entry, Rect rect, bool selected, Rect visible)
        {
            var e = Event.current;
            var hover = rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition) && _drag == Drag.None;
            var inner = new Rect(rect.x + U(2f), rect.y + U(1f), rect.width - U(4f), rect.height - U(2f));

            if (selected) Skin.Box(inner, Skin.AccentSoft);
            else if (hover) Skin.Box(inner, Skin.Hover);
            if (selected) Skin.Fill(new Rect(inner.x, inner.y + U(8f), U(3f), inner.height - U(16f)), Skin.Accent);

            // An entry without an icon of its own gets no mark in a kind's tab, where it would be
            // the same on every row, and the names move left when nothing there has an icon; in a
            // list of every kind, a dot of its kind's colour tells the kinds apart.
            var icon = new Rect(inner.x + U(10f), inner.y + (inner.height - U(24f)) / 2f, U(24f), U(24f));
            var oneKind = Grouped(explorer);
            var drawn = Timing.Start();
            if (HasIcon(entry)) DrawIcon(entry, icon);
            else if (!oneKind)
            {
                var dot = U(8f);
                Skin.Icon(new Rect(icon.center.x - dot / 2f, icon.center.y - dot / 2f, dot, dot), Skin.Circle, Skin.KindColor(entry.Kind));
            }
            Timing.Add("list icons", drawn);

            var favourite = explorer.Favourites.Contains(entry);
            var star = new Rect(inner.xMax - U(28f), inner.y + (inner.height - U(18f)) / 2f, U(18f), U(18f));
            if (favourite) Skin.Icon(star, Skin.Star, Skin.Accent);
            else if (hover) Skin.Icon(star, Skin.StarHollow, star.Contains(e.mousePosition) ? Skin.Accent : Skin.Faint);

            var named = Timing.Start();
            var textX = oneKind && !_listHasIcons ? inner.x + U(12f) : icon.xMax + U(10f);
            // A room sits indented under its dungeon in its dungeon's group.
            if ((entry.Indent || explorer.UnderOwner(entry)) && oneKind) textX += U(20f);
            var textW = star.x - U(8f) - textX;
            var primary = entry.ShownName;
            var secondary = entry.Tag ?? (primary == entry.Name ? "" : entry.Name);

            var nameStyle = Skin.RowName;
            var fullW = Skin.Width(nameStyle, primary);
            var nameW = Mathf.Min(textW, fullW);
            Skin.LabelIn(new Rect(textX, inner.y, nameW, inner.height), primary, nameStyle, entry.Empty ? Skin.Faint : Skin.Text);

            var cut = fullW > textW;
            if (secondary.Length > 0)
            {
                var room = textW - nameW - U(8f);
                if (room > U(40f))
                {
                    GUI.Label(new Rect(textX + nameW + U(8f), inner.y + U(1f), room, inner.height), secondary, Skin.RowSub);
                    cut |= Skin.Width(Skin.RowSub, secondary) > room;
                }
                else
                {
                    cut = true;
                }
            }

            // A cut name shows whole on hover, and a faint row says why it is faint.
            if (hover && (cut || entry.Empty) && !star.Contains(e.mousePosition))
            {
                AskTip(entry.Key, Naming.Lines(primary, secondary, entry.Empty ? ListWords.Silent : null));
            }
            Timing.Add("list names", named);

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition))
            {
                if (star.Contains(e.mousePosition))
                {
                    explorer.ToggleFavourite(entry);
                }
                else
                {
                    explorer.Select(entry);
                    if (e.clickCount == 2) Primary(entry);
                }
                e.Use();
            }
        }

        private static void DrawIcon(Entry entry, Rect rect)
        {
            // A sprite packed in a way that has no simple rectangle has the mark below stand in.
            if (entry.Icon is Sprite sprite && sprite != null && sprite.texture != null && Event.current.type == EventType.Repaint
                && SpriteUv(sprite, out var uv, out var r))
            {
                var aspect = r.width / Mathf.Max(1f, r.height);
                var fit = aspect >= 1f
                    ? new Rect(rect.x, rect.y + (rect.height - rect.height / aspect) / 2f, rect.width, rect.height / aspect)
                    : new Rect(rect.x + (rect.width - rect.width * aspect) / 2f, rect.y, rect.width * aspect, rect.height);
                GUI.DrawTextureWithTexCoords(fit, sprite.texture, uv, true);
                return;
            }

            var color = Skin.KindColor(entry.Kind);
            Skin.Box(rect, Skin.Alpha(color, 0.20f));
            Skin.LabelIn(rect, Skin.KindMark(entry.Kind), Skin.Glyph, color);
        }

        /// <summary>Lets go of the list's rows, which hold the world left's entries.</summary>
        private static void ForgetList()
        {
            _rowsFor = null;
            _foldChecked = null;
            _listRows.Clear();
            _rowOfEntry.Clear();
        }
    }
}
