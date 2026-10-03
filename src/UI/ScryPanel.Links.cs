using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>Chips that go to other entries, coloured by their kind, and the Linked section.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>Goes to the entry a key names: a prefab by its name, or a status effect or raid in its namespace (<see cref="EntryKeys"/>).</summary>
        private static void Go(Explorer explorer, string target)
        {
            if (!explorer.Jump(target)) return;
            _reveal = true;
            _sideScroll = Vector2.zero;
            _help = false;
        }

        /// <summary>Puts a search in the box, with every other filter cleared so it shows all it finds.</summary>
        private static void SearchFor(Explorer explorer, string text)
        {
            _modReport = false;
            explorer.SearchEverything(text);
            _listScroll = Vector2.zero;
            _reveal = true;
            _help = false;
        }

        /// <summary>Text drawn as a link: accent coloured, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style) => LinkLabel(rect, text, style, Skin.Accent);

        /// <summary>Text that can be clicked: in its colour, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style, Color colour)
        {
            var was = style.normal.textColor;
            style.normal.textColor = rect.Contains(Event.current.mousePosition) ? Color.Lerp(colour, Color.white, 0.35f) : colour;
            GUI.Label(rect, text, style);
            style.normal.textColor = was;
        }

        /// <summary>
        /// A chip that goes to an entry rather than doing something: tinted in the colour of the
        /// kind of entry it goes to, with an arrow. Brighter while what it names is playing. One
        /// that goes nowhere (the entry itself, or something not in the catalog) is plain.
        /// </summary>
        private static bool LinkChip(Rect rect, string text, Kind? kind, bool lit, bool go)
        {
            var hover = go && rect.Contains(Event.current.mousePosition);
            Skin.PillBox(rect, go || lit ? LinkFill(kind, hover, lit) : new Color(0.2f, 0.2f, 0.22f, 0.45f));

            var style = Skin.Small;
            var was = style.normal.textColor;
            var alignment = style.alignment;
            style.normal.textColor = go || lit ? LinkText(kind, hover || lit) : Skin.Dim;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, go ? GoText(text) : text, style);
            style.normal.textColor = was;
            style.alignment = alignment;

            return go && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>The fill of anything that goes to an entry: a dark shade of its kind's colour.</summary>
        private static Color LinkFill(Kind? kind, bool hover, bool lit = false)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            var fill = lit ? 0.55f : hover ? 0.36f : 0.22f;
            return new Color(colour.r * fill, colour.g * fill, colour.b * fill, 0.95f);
        }

        /// <summary>The text of anything that goes to an entry: its kind's colour, lighter.</summary>
        private static Color LinkText(Kind? kind, bool hover)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            return Color.Lerp(colour, Color.white, hover ? 0.6f : 0.35f);
        }

        /// <summary>
        /// A chip's width. A name not measured before is measured within the frame's share, and
        /// guessed from its length past it, so opening a long list measures its names over a few
        /// frames rather than all in one.
        /// </summary>
        private static float LinkChipWidth(string text, bool go) => Skin.WidthSoon(Skin.Small, go ? GoText(text) : text) + U(16f);

        private static readonly Dictionary<string, string> GoTexts = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>A chip's text with the arrow of one that goes somewhere, made once for each name.</summary>
        private static string GoText(string text)
        {
            if (GoTexts.TryGetValue(text, out var shown)) return shown;
            if (GoTexts.Count > 20000) GoTexts.Clear();
            return GoTexts[text] = text + "  \u203A";
        }

        /// <summary>The kind of the entry a key goes to (a prefab's name, or a key in a namespace), when it is in the catalog.</summary>
        private static Kind? KindOf(Explorer explorer, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_kindsFor != explorer)
            {
                _kindsFor = explorer;
                KindByKey.Clear();
                foreach (var e in explorer.Catalog) if (!KindByKey.ContainsKey(e.Key)) KindByKey[e.Key] = e.Kind;
            }
            return KindByKey.TryGetValue(key, out var kind) ? kind : (Kind?)null;
        }

        private static readonly Dictionary<string, Kind> KindByKey = new Dictionary<string, Kind>();
        private static Explorer _kindsFor;

        /// <summary>A small heading and a wrapping row of link chips, each with its own text, tip and doing.</summary>
        private static float LinkItems(Explorer explorer, string title, IEnumerable<(string Key, string Text, string Tip, Action Click)> items, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            y += U(24f);
            var x = 0f;
            var rowH = U(26f);
            var all = items as IList<(string Key, string Text, string Tip, Action Click)> ?? items.ToList();
            var key = "links:" + title;
            var count = ShownOf(key, all.Count);
            for (var i = 0; i < count; i++)
            {
                var item = all[i];
                var w = Mathf.Min(width, LinkChipWidth(item.Text, true));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (OutOfSight(chip))
                {
                    x += w + U(5f);
                    continue;
                }
                if (LinkChip(chip, item.Text, KindOf(explorer, item.Key), false, true)) item.Click();
                if (chip.Contains(Event.current.mousePosition)) AskTip("link:" + title + item.Key + item.Text, item.Tip ?? "Go to " + item.Text);
                x += w + U(5f);
            }
            MoreChip(key, all.Count, FirstChips, width, rowH, U(5f), ref x, ref y);
            return y + rowH + U(10f);
        }

        /// <summary>
        /// Everything else the entry is linked to, a row per heading: what it leaves behind or is
        /// left by, what it carries, its footsteps, the sounds its animations name, what it
        /// spawns, its set, its ammo, its status effects. A sound an animation names goes to the
        /// creature and plays that animation there.
        /// </summary>
        private static float LinksSection(Explorer explorer, Entry entry, float width, float y)
        {
            // Made once for the entry shown, not for every event the panel draws.
            if (!ReferenceEquals(_linksFor, entry) || !ReferenceEquals(_linksIn, explorer))
            {
                _linksFor = entry;
                _linksIn = explorer;
                LinkRows.Clear();
                MakeLinkRows(explorer, entry);
                _linkedHeading = $"LINKED  {Numbers.Count(LinkRows.Sum(r => r.Items.Count))}";
            }
            if (LinkRows.Count == 0) return y;

            y = SectionHeading(_linkedHeading, width, y, null, "links");
            if (IsFolded("links")) return y;

            foreach (var (title, items) in LinkRows) y = LinkItems(explorer, title, items, width, y);
            return y + U(4f);
        }

        private static Entry _linksFor;
        private static Explorer _linksIn;
        /// <summary>The section's heading with how many things it links, made with its rows.</summary>
        private static string _linkedHeading = "LINKED";

        private static readonly List<(string Title, List<(string Key, string Text, string Tip, Action Click)> Items)> LinkRows =
            new List<(string, List<(string, string, string, Action)>)>();

        /// <summary>The rows of the LINKED section, each chip with its text, tip and what it does.</summary>
        private static void MakeLinkRows(Explorer explorer, Entry entry)
        {
            void Plain(string title, IEnumerable<string> names)
            {
                LinkRows.Add((title, names.Select(n => (n, ShownName(explorer, n, n), (string)null, (Action)(() => Go(explorer, n)))).ToList()));
            }
            if (entry.LeftBy.Count > 0) Plain("Left behind by", entry.LeftBy);
            if (entry.LeavesBehind.Count > 0) Plain("Leaves behind", entry.LeavesBehind);

            foreach (var group in entry.LinkGroups())
            {
                var links = group.Value;
                var title = group.Key;

                if (group.Key == Relations.PlayedByAnimation)
                {
                    // One chip per animation, which goes to the creature and plays it.
                    var items = links.SelectMany(l => l.Notes.Count > 0 ? l.Notes.Select(n => (l.Target, n)) : new[] { (l.Target, "") })
                        .Select(p =>
                        {
                            var shown = ShownName(explorer, p.Target, p.Target);
                            var text = p.Item2.Length > 0 ? shown + " \u00b7 " + p.Item2 : shown;
                            var tip = p.Item2.Length > 0 ? $"Go to {shown} and play its {p.Item2} animation" : "Go to " + shown;
                            return (p.Target, text, tip, (Action)(() =>
                            {
                                if (p.Item2.Length > 0) Previews.PlayClipOnShow(p.Item2);
                                Go(explorer, p.Target);
                            }));
                        });
                    LinkRows.Add((title, items.ToList()));
                    continue;
                }

                // How an item gives an effect is told in its facts' words; the notes keep the field's for grouping.
                var giver = group.Key == Relations.GivenBy;
                LinkRows.Add((title, links.Select(l =>
                {
                    var shown = ShownName(explorer, l.Target, l.Target);
                    var notes = giver ? l.Notes.Select(Groups.GiverWords).ToList() : l.Notes;
                    var note = notes.Count == 1 && notes[0].Length > 0 && notes[0].Length <= 28 ? " \u00b7 " + notes[0] : "";
                    var tip = "Go to " + shown + (notes.Count > 0 ? "\n" + string.Join("\n", notes.Take(12)) : "");
                    return (l.Target, shown + note, tip, (Action)(() => Go(explorer, l.Target)));
                }).ToList()));
            }
        }

        /// <summary>A small heading and a wrapping row of chips, each doing its own thing when clicked.</summary>
        private static float ChipRow(string title, IEnumerable<KeyValuePair<string, Action>> chips, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            y += U(24f);
            var x = 0f;
            var rowH = U(26f);
            var all = chips.ToList();
            var key = "chips:" + title;
            foreach (var chip in all.Take(ShownOf(key, all.Count)))
            {
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, chip.Key) + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var rect = new Rect(x, y, w, rowH);
                if (!OutOfSight(rect) && GUI.Button(rect, chip.Key, Skin.Chip)) chip.Value();
                x += w + U(5f);
            }
            MoreChip(key, all.Count, FirstChips, width, rowH, U(5f), ref x, ref y);
            return y + rowH + U(10f);
        }

        private static readonly Dictionary<string, Sprite> PrefabIcons = new Dictionary<string, Sprite>();
        private static HashSet<string> _catalogNames;
        private static Explorer _namesFor;

        /// <summary>
        /// Builds the lookups the side pane uses over the whole catalog (names in it, their kinds,
        /// the names the game shows, the status effects) one a frame after the catalog is read,
        /// so the first entry selected does not build them all at once.
        /// </summary>
        public static void Prepare(Explorer explorer)
        {
            if (explorer == null) return;

            // Reading the locations adds values to search by (in:): the terms are made again on a
            // worker thread started here, and taken up here once done, rather than on a keystroke.
            if (ReferenceEquals(explorer, _prepared))
            {
                if (!Assist.HasTermsFor(explorer) && Locations.Now != Locations.State.Reading) Assist.TermsFor(explorer);
                return;
            }
            if (!ReferenceEquals(explorer, _preparing))
            {
                _preparing = explorer;
                _prepareStep = 0;
            }
            switch (_prepareStep++)
            {
                case 0: InCatalog(explorer, ""); break;
                case 1: KindOf(explorer, "-"); break;
                case 2: ShownName(explorer, "-", ""); break;
                case 3: Assist.TermsFor(explorer); break;
                default: _prepared = explorer; break;
            }
        }

        private static Explorer _preparing;
        private static Explorer _prepared;
        private static int _prepareStep;

        /// <summary>Whether the entry a key names (a prefab's name, or a key in a namespace) is in the catalog to go to.</summary>
        private static bool InCatalog(Explorer explorer, string key)
        {
            if (_namesFor != explorer || _catalogNames == null)
            {
                _namesFor = explorer;
                _catalogNames = new HashSet<string>(explorer.Catalog.Select(e => e.Key));
                PrefabIcons.Clear();
            }
            return key != null && _catalogNames.Contains(key);
        }

        /// <summary>The icon of an item or piece prefab by name, or null.</summary>
        private static Sprite PrefabIcon(string prefab)
        {
            if (PrefabIcons.TryGetValue(prefab, out var known)) return known;
            Sprite icon = null;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (go != null)
            {
                var icons = go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
                if (icons != null && icons.Length > 0) icon = icons[0];
                else icon = go.GetComponent<Piece>()?.m_icon;
            }
            PrefabIcons[prefab] = icon;
            return icon;
        }

        private static void DrawSprite(Sprite sprite, Rect rect)
        {
            if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint) return;
            if (!SpriteUv(sprite, out var uv, out _)) return;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        }

        /// <summary>Sprites with no simple rectangle, found once rather than failing on every repaint.</summary>
        private static readonly HashSet<Sprite> Unpacked = new HashSet<Sprite>();

        /// <summary>
        /// Where a sprite sits on its texture, and its rectangle there; false for one packed in a
        /// way that has no simple rectangle, which is then drawn without its icon.
        /// </summary>
        private static bool SpriteUv(Sprite sprite, out Rect uv, out Rect rect)
        {
            uv = rect = default;
            if (Unpacked.Contains(sprite)) return false;
            try
            {
                var t = sprite.texture;
                rect = sprite.textureRect;
                uv = new Rect(rect.x / t.width, rect.y / t.height, rect.width / t.width, rect.height / t.height);
                return true;
            }
            catch
            {
                Unpacked.Add(sprite);
                return false;
            }
        }
    }
}
