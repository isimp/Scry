using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry: its card, stage, title, actions, outfit and stage buttons.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>What of the scrolled side can be seen, in its own terms, so rows of chips out of sight are only counted, not drawn.</summary>
        private static Rect _sideVisible;

        private static bool OutOfSight(Rect rect) => rect.yMax < _sideVisible.yMin || rect.yMin > _sideVisible.yMax;

        /// <summary>How many chips of a list show before the rest are left to a "more" chip.</summary>
        private const int FirstChips = 18;

        /// <summary>Long lists opened to show all, by key; all fold again when another entry is shown.</summary>
        private static readonly HashSet<string> OpenLists = new HashSet<string>();

        /// <summary>How many of a list to show (<see cref="Shortlist"/>).</summary>
        private static int ShownOf(string key, int total, int first = FirstChips) => Shortlist.Shown(total, first, OpenLists.Contains(key));

        /// <summary>
        /// The chip at the end of a long list, placed as its own chips are: "N more" shows the
        /// rest, "Show fewer" folds it again. Every long list in the panel ends with it.
        /// </summary>
        private static void MoreChip(string key, int total, int first, float width, float rowH, float gap, ref float x, ref float y)
        {
            if (!Shortlist.Long(total, first)) return;
            var open = OpenLists.Contains(key);
            var text = open ? "Show fewer" : $"{Shortlist.Hidden(total, first, false)} more";
            var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(16f));
            if (x + w > width && x > 0f)
            {
                x = 0f;
                y += rowH + gap;
            }
            var rect = new Rect(x, y, w, rowH);
            if (!OutOfSight(rect) && GUI.Button(rect, text, Skin.Chip))
            {
                if (open) OpenLists.Remove(key);
                else OpenLists.Add(key);
            }
            x += w + gap;
        }

        /// <summary>Room kept at the end of the name's second line for the link that folds every section.</summary>
        private static float _foldAllW;

        private static bool AnyOpen()
        {
            foreach (var key in Foldable) if (!Folded.Contains(key)) return true;
            return false;
        }

        /// <summary>Folds or opens every section, always in the same place whatever is selected.</summary>
        private static void FoldAllLink(Rect link, string text, bool anyOpen)
        {
            LinkLabel(link, text, Skin.FaintLabel, Skin.Faint);
            if (link.Contains(Event.current.mousePosition)) AskTip("fold-all", anyOpen ? "Fold every section away" : "Open every section");
            if (GUI.Button(link, GUIContent.none, GUIStyle.none)) FoldAll(anyOpen);
        }

        /// <summary>The entry the side was last shown for; another starts at its top.</summary>
        private static Entry _sideFor;

        private static void Side(Explorer explorer, Rect rect, bool withStage)
        {
            var entry = explorer.Selected;
            if (!ReferenceEquals(entry, _sideFor))
            {
                _sideFor = entry;
                _sideScroll = Vector2.zero;
                OpenLists.Clear();
            }

            // What a world's catalog points at is gone once the world is: nothing to show then.
            if (entry != null && entry.Source is UnityEngine.Object source && source == null)
            {
                explorer.Select(null);
                entry = null;
            }
            if (entry == null)
            {
                Skin.Box(rect, Skin.Panel);
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(40f), rect.width - U(60f), U(80f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(30f)), "Pick something from the list", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(32f), middle.width, U(44f)),
                    "Type to search, use the arrow keys to move, and Enter to play or show it.", Skin.CenterDim);
                return;
            }

            var top = rect.y;
            if (withStage)
            {
                // A sound or a status effect with nothing to show on a person gets a card only as
                // tall as what it holds, so its details start higher; a model gets the stage.
                var card = CardHeight(entry);
                _stageBaseH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
                var stageH = card > 0f ? card : Mathf.Round(Mathf.Clamp(_stageBaseH * _stageScale, U(120f), rect.height * 0.85f));
                Section("side stage", 0f, _ => { StageArea(explorer, entry, new Rect(rect.x, rect.y, rect.width, stageH)); return 0f; });
                if (card <= 0f) StageHandle(new Rect(rect.x, rect.y + stageH, rect.width, U(12f)));
                top += stageH + U(12f);
            }

            // The name stays in sight while what is below it scrolls, so a long section scrolled
            // to never leaves the selection unnamed.
            var titleArea = new Rect(rect.x, top, rect.width - U(14f), U(68f));
            var anyOpen = AnyOpen();
            var foldText = anyOpen ? "fold all" : "open all";
            _foldAllW = Skin.Width(Skin.FaintLabel, foldText) + U(16f);
            GUI.BeginGroup(titleArea);
            var titleH = Section("side title", 0f, at => Title(explorer, entry, titleArea.width, at));
            FoldAllLink(new Rect(titleArea.width - _foldAllW + U(12f), U(34f), _foldAllW - U(12f), U(22f)), foldText, anyOpen);
            GUI.EndGroup();
            top += titleH;

            var below = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            _sideVisible = new Rect(0f, _sideScroll.y, content.width, below.height);

            var cw = content.width;
            var y = 0f;
            // Without a stage a sound says what it is here; a status effect's card would only repeat In the game.
            if (!withStage && entry.Kind == Kind.Sound) y = Section("side card", y, at => CompactCard(entry, cw, at));
            y = Section("side actions", y, at => Actions(entry, cw, at, withStage));
            if (Looks.IsWorn(entry)) y = Section("side wearing", y, at => Wearing(explorer, cw, at));
            if (entry.Kind == Kind.Sound)
            {
                // With a stage the timeline is on the sound's card.
                if (!withStage) y = Section("side timeline", y, at => Timeline(cw, at));
                y = Section("side variants", y, at => Variants(entry, cw, at));
            }

            // How it plays comes right under what can be done with it, then what it is in the game:
            // an item's stats and recipe are what most look for, and come before a creature's
            // hundreds of clips.
            y = Section("side adjust", y, at => Adjust(explorer, entry, cw, at, withStage));
            y = Section("side facts", y, at => FactsSection(explorer, entry, cw, at));
            y = Section("side runes", y, at => RunesSection(entry, cw, at));
            y = Section("side readme", y, at => ReadmeSection(entry, cw, at));
            y = Section("side animations", y, at => Animations(explorer, entry, cw, at, withStage));
            y = Section("side effects", y, at => Effects(explorer, entry, cw, at, withStage));
            y = Section("side plays in", y, at => PlaysInSection(explorer, entry, cw, at));
            y = Section("side links", y, at => LinksSection(explorer, entry, cw, at));
            y = Section("side command", y, at => Command(explorer, entry, cw, at));
            y = Section("side details", y, at => Details(explorer, entry, cw, at));
            // The height as this draw found it, so the scroll range is right from the next event on,
            // and a scroll left past the end by content that shrank comes back to the end.
            _sideHeight = y + U(8f);
            GUI.EndScrollView();
            _sideScroll.y = Mathf.Clamp(_sideScroll.y, 0f, Mathf.Max(0f, _sideHeight - below.height));
        }

        /// <summary>
        /// Draws one section of the side, timed. A section that fails is left out, told once in the
        /// log, and the others still draw: a mod's odd prefab costs one section, not the panel.
        /// No section opens a group or scroll view of its own, so leaving one halfway is safe.
        /// </summary>
        private static float Section(string part, float y, System.Func<float, float> draw)
        {
            var started = Timing.Start();
            try
            {
                return draw(y);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Faults.Tell("the panel's " + part, ex);
                GUI.enabled = true;
                GUI.color = Color.white;
                return y;
            }
            finally
            {
                Timing.Add(part, started);
            }
        }

        /// <summary>
        /// The strip under the stage: dragged, it makes the stage taller or shorter; double-clicked,
        /// it puts the usual height back.
        /// </summary>
        private static void StageHandle(Rect strip)
        {
            var e = Event.current;
            var hover = strip.Contains(e.mousePosition) || _drag == Drag.StageSize;
            var bar = new Rect(strip.center.x - U(24f), strip.y + U(4f), U(48f), U(4f));
            Skin.Fill(bar, hover ? Skin.Dim : Skin.Outline);
            if (strip.Contains(e.mousePosition)) AskTip("stage-size", "Drag to make the stage taller or shorter, double-click for its usual height");

            if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
            {
                if (e.clickCount == 2)
                {
                    _stageScale = 1f;
                    SaveRects();
                }
                else
                {
                    _drag = Drag.StageSize;
                }
                e.Use();
            }
        }

        /// <summary>The room of the stage's example the left button went down on, gone to if it comes up without a drag.</summary>
        private static string _stageRoomDown;

        /// <summary>How far the mouse has moved since the left button went down on the stage.</summary>
        private static float _orbitMoved;

        /// <summary>What the stage says while a dungeon's or camp's example is read and built; null once it stands.</summary>
        private static string ExampleProgress(Entry entry)
        {
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon == null || !ExampleLayouts.Of(entry)) return null;
            if (ExampleLayouts.Example == null) return DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total);
            var total = Stage.ExampleRoomsTotal;
            return total > 0 && Stage.ExampleRoomsShown < total ? DungeonWords.Building(Stage.ExampleRoomsShown, total) : null;
        }

        private static void StageArea(Explorer explorer, Entry entry, Rect rect)
        {
            var e = Event.current;
            Skin.Box(rect, Skin.Stage);

            if (Stage.IsStaged(entry))
            {
                var inner = new Rect(rect.x + U(3f), rect.y + U(3f), rect.width - U(6f), rect.height - U(6f));
                if (e.type == EventType.Repaint) Stage.Request((int)inner.width, (int)inner.height);

                if (Stage.Subject != null && Stage.Texture != null)
                {
                    var drawn = Timing.Start();
                    if (e.type == EventType.Repaint) GUI.DrawTexture(inner, Stage.Texture, ScaleMode.StretchToFill, false);
                    Timing.Add("stage texture", drawn);
                }
                else if (entry.Kind != Kind.Effect)
                {
                    // A location or room is shown once its model has loaded and its copy is made.
                    var note = entry.Source is PlaceSource place ? LocationWords.StageNote(Stage.Building ? PlaceLoad.Loading : PlaceAssets.State(place)) : null;
                    GUI.Label(inner, note ?? "This one could not be previewed.", Skin.CenterDim);
                }

                // The example's plan in the corner, which takes the mouse where it is.
                var plan = Stage.Subject != null ? PlanOverlay(explorer, entry, inner) : Rect.zero;

                // A room of the example under the mouse is named, and a click on it that is not a
                // drag goes to its entry.
                string roomKey = null;
                _stageRoom = null;
                var still = _drag == Drag.None || (_drag == Drag.Orbit && _orbitMoved < U(5f));
                if (Stage.ExampleRoomsShown > 0 && still && inner.Contains(e.mousePosition) && !plan.Contains(e.mousePosition))
                {
                    var point = new Vector2((e.mousePosition.x - inner.x) / inner.width, 1f - (e.mousePosition.y - inner.y) / inner.height);
                    var room = Stage.ExampleRoomAt(point);
                    if (room != null)
                    {
                        var key = EntryKeys.For(Kind.Location, room.Room.Name);
                        var known = InCatalog(explorer, key);
                        var name = ShownName(explorer, key, LocationWords.RoomName(room.Room.Name));
                        AskTip("stageroom:" + room.Room.Name, known ? name + "\nClick to go to it" : name);
                        if (known) roomKey = key;
                        _stageRoom = room.Room.Name;
                    }
                }
                if (e.type == EventType.MouseUp && e.button == 0 && _drag == Drag.Orbit && _stageRoomDown != null && _orbitMoved < U(5f))
                {
                    var go = _stageRoomDown;
                    _stageRoomDown = null;
                    Go(explorer, go);
                }

                // The camera's buttons show only while the mouse is on the stage, as its hint does,
                // so the model is not framed by controls while it is looked at.
                var over = rect.Contains(e.mousePosition) || _drag == Drag.Orbit;
                var viewsW = over ? ViewButtons(inner) : 0f;
                var textW = inner.width - U(24f) - viewsW;
                if (over)
                {
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        Stage.Cutting ? StageHintCut : StageHint, Skin.FaintLabel, 9f);
                }
                else if (ExampleProgress(entry) is string progress)
                {
                    // Not measured: the text changes as it goes, and each would be kept.
                    GUI.Label(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)), progress, Skin.DimLabel);
                }
                else if (Stage.Subject != null && Stage.ShowsGrid)
                {
                    // On the grid, how big the model is, in the same metres as its squares.
                    var size = Stage.SubjectSize;
                    string M(float v) => v.ToString(v < 10f ? "0.0" : "0", CultureInfo.InvariantCulture);
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        $"Squares of 1 m, lines every 5 m  \u00B7  {M(size.y)} m tall, {M(size.x)} × {M(size.z)} m", Skin.DimLabel, 9f);
                }

                // The stage's own buttons and its floor ruler come before its dragging, which would otherwise take their clicks.
                NoteStage(rect);
                StageButtons(entry, rect);
                FloorRuler(rect);

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount == 2) Stage.ResetView();
                    _stageRoomDown = e.clickCount == 2 ? null : roomKey;
                    _orbitMoved = 0f;
                    _drag = Drag.Orbit;
                    Stage.Dragging = true;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
                {
                    _drag = Drag.Pan;
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
                {
                    // With Shift the wheel moves the cut of a place opened, down as it scrolls down.
                    if (e.shift && Stage.Cutting) Stage.CutBy(-e.delta.y * 0.25f);
                    else Stage.ZoomBy(e.delta.y);
                    e.Use();
                }
            }
            else if (entry.Kind == Kind.Sound)
            {
                var drawn = Timing.Start();
                SoundCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.StatusEffect)
            {
                var drawn = Timing.Start();
                StatusCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Mod)
            {
                var drawn = Timing.Start();
                ModCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Biome)
            {
                var drawn = Timing.Start();
                BiomeCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Raid)
            {
                var drawn = Timing.Start();
                RaidCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else
            {
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(34f), rect.width - U(60f), U(70f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(28f)), "Nothing to see or hear", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(30f), middle.width, U(40f)),
                    "It has no model, particles, light or sound. Often a spawner or a controller.", Skin.CenterDim);
            }

            KindBadge(entry, new Vector2(rect.x + U(10f), rect.y + U(10f)));
        }

        /// <summary>The kind, in its colour, as a small pill.</summary>
        private static float KindBadge(Entry entry, Vector2 at)
        {
            var color = Skin.KindColor(entry.Kind);
            var label = entry.Kind == Kind.StatusEffect ? "Status effect" : Kinds.Label(entry.Kind).TrimEnd('s');
            var width = Skin.Width(Skin.Glyph, label) + U(20f);
            var badge = new Rect(at.x, at.y, width, U(22f));
            _badgeWidth = width;
            Skin.PillBox(badge, new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 0.95f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = Color.Lerp(color, Color.white, 0.25f);
            GUI.Label(badge, label, style);
            style.normal.textColor = was;
            return badge.xMax;
        }

        /// <summary>In compact view, what the sound card would say: its clips and length.</summary>
        private static float CompactCard(Entry entry, float width, float y)
        {
            var text = SoundFacts(entry);
            var height = Skin.Height(Skin.DimWrap, text, width);
            GUI.Label(new Rect(0f, y, width, height), text, Skin.DimWrap);
            return y + height + U(12f);
        }

        private static readonly Dictionary<Entry, string> SoundFactCache = new Dictionary<Entry, string>();

        private static string SoundFacts(Entry entry)
        {
            if (!SoundFactCache.TryGetValue(entry, out var facts))
            {
                facts = DescribeSound(entry.Source as GameObject);
                SoundFactCache[entry] = facts;
            }
            return facts;
        }

        /// <summary>The card's height for an entry shown as a card on the stage, or 0 for one the stage shows.</summary>
        private static float CardHeight(Entry entry)
        {
            if (entry == null || Stage.IsStaged(entry)) return 0f;
            if (entry.Kind == Kind.Sound) return U(118f);
            if (entry.Kind == Kind.StatusEffect) return U(150f);
            if (entry.Kind == Kind.Mod && entry.Icon is Sprite) return U(150f);
            if (entry.Kind == Kind.Raid || entry.Kind == Kind.Mod || entry.Kind == Kind.Biome) return U(118f);
            return 0f;
        }

        private static readonly Dictionary<Entry, (string Lasts, string Brings)> RaidCardCache = new Dictionary<Entry, (string, string)>();

        /// <summary>A raid has nothing to show on the stage: how long it lasts and what it brings, the rest under In the game.</summary>
        /// <summary>A biome has nothing to show on the stage: its weathers, music and what is there are under In the game.</summary>
        private static void BiomeCard(Entry entry, Rect rect)
        {
            var weathers = entry.Source is BiomeSource biome ? BiomeWords.Weathers(biome.Weathers).Count : 0;
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), weathers == 1 ? "One weather" : $"{weathers} weathers", Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(68f), rect.width - U(40f), U(40f)), "Enter plays its music", Skin.CenterDim);
        }

        private static void RaidCard(Entry entry, Rect rect)
        {
            if (!RaidCardCache.TryGetValue(entry, out var card))
            {
                var raid = entry.Source as RandomEvent;
                var brings = raid?.m_spawn?.Where(s => s?.m_prefab != null).Select(s => ShownNameOf(s.m_prefab)).Distinct().ToList() ?? new List<string>();
                card = (raid != null ? "Lasts " + Naming.Duration(raid.m_duration) : "", brings.Count > 0 ? "Brings " + string.Join(", ", brings) : "Brings nothing");
                RaidCardCache[entry] = card;
            }
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), card.Lasts, Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(68f), rect.width - U(40f), U(40f)), card.Brings, Skin.CenterDim);
        }

        /// <summary>A mod's card in the stage's place: its version and id, and what it adds.</summary>
        private static void ModCard(Entry entry, Rect rect)
        {
            if (!(entry.Source is ModSource mod)) return;
            var summary = Session.Explorer != null ? Report(Session.Explorer).FirstOrDefault(m => m.Mod == mod.Name) : null;
            var adds = summary != null ? ModReportWords.Counts(summary) : "adds nothing of its own";
            // Its package's icon above, where it has one.
            var top = rect.y + U(40f);
            if (entry.Icon is Sprite icon && icon != null)
            {
                var size = U(56f);
                DrawSprite(icon, new Rect(rect.center.x - size / 2f, rect.y + U(34f), size, size));
                top = rect.y + U(96f);
            }
            GUI.Label(new Rect(rect.x + U(20f), top, rect.width - U(40f), U(24f)), ModWords.Card(mod), Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), top + U(28f), rect.width - U(40f), U(40f)), char.ToUpperInvariant(adds[0]) + adds.Substring(1), Skin.CenterDim);
        }

        /// <summary>A creature's name as the game shows it, else its prefab's.</summary>
        private static string ShownNameOf(GameObject prefab)
        {
            var shown = CatalogBuilder.Localize(prefab.GetComponent<Character>()?.m_name);
            return shown.Length > 0 ? shown : prefab.name;
        }

        /// <summary>What the sound is (its clips and length), and where it has got to, which can be moved.</summary>
        private static void SoundCard(Entry entry, Rect rect)
        {
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), SoundFacts(entry), Skin.Center);
            var line = new Rect(rect.x + U(20f), rect.y + U(74f), rect.width - U(40f), U(30f));
            GUI.BeginGroup(line);
            Timeline(line.width, 0f);
            GUI.EndGroup();
        }

        private static string DescribeSound(GameObject prefab)
        {
            if (prefab == null) return "";

            var clips = new List<AudioClip>();
            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true))
            {
                if (sfx.m_audioClips != null) clips.AddRange(sfx.m_audioClips.Where(c => c != null));
            }
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null && !clips.Contains(source.clip)) clips.Add(source.clip);
            }

            var loops = prefab.GetComponentsInChildren<AudioSource>(true).Any(s => s.loop);
            if (clips.Count == 0) return loops ? "Loops" : "No clips found";

            var longest = clips.Max(c => c.length);
            var what = clips.Count == 1 ? "1 clip" : $"{clips.Count} clips, one picked at random";
            return $"{what}, {longest.ToString("0.0", CultureInfo.InvariantCulture)} s{(loops ? ", loops" : "")}";
        }

        private static readonly Dictionary<Entry, List<KeyValuePair<string, EffectList>>> StatusListCache =
            new Dictionary<Entry, List<KeyValuePair<string, EffectList>>>();

        private static List<KeyValuePair<string, EffectList>> StatusLists(Entry entry)
        {
            if (!StatusListCache.TryGetValue(entry, out var lists))
            {
                lists = Previews.StatusLists(entry.Source as StatusEffect);
                StatusListCache[entry] = lists;
            }
            return lists;
        }

        private static string StatusFacts(StatusEffect effect)
        {
            var parts = new List<string>();
            parts.Add(effect.m_ttl > 0f ? "Lasts " + Naming.Duration(effect.m_ttl) : "No time limit of its own");
            return string.Join("    ", parts);
        }

        private static void StatusCard(Entry entry, Rect rect)
        {
            // Its icon and how long it lasts; what it does is the first thing under In the game.
            var effect = entry.Source as StatusEffect;
            var iconSize = U(64f);
            var icon = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + U(38f), iconSize, iconSize);
            if (effect != null && effect.m_icon != null) DrawIcon(entry, icon);
            else DrawIcon(new Entry { Kind = Kind.StatusEffect }, icon);

            if (effect == null) return;
            GUI.Label(new Rect(rect.x + U(20f), icon.yMax + U(10f), rect.width - U(40f), U(22f)), StatusFacts(effect), Skin.CenterDim);
        }

        private static float Title(Explorer explorer, Entry entry, float width, float y)
        {
            var primary = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            var copyW = U(104f);
            var nameRect = new Rect(0f, y, width - copyW - U(46f), U(30f));
            var fits = FitLabel(nameRect, primary, Skin.Big, 13f);
            if (!fits && nameRect.Contains(Event.current.mousePosition)) AskTip("title", primary);

            var favourite = explorer.Favourites.Contains(entry);
            var starRect = new Rect(width - U(28f), y + U(4f), U(22f), U(22f));
            Skin.Icon(starRect, favourite ? Skin.Star : Skin.StarHollow, favourite ? Skin.Accent : Skin.Dim);
            if (GUI.Button(starRect, GUIContent.none, GUIStyle.none)) explorer.ToggleFavourite(entry);
            if (starRect.Contains(Event.current.mousePosition)) AskTip("star", favourite ? "Remove from favourites" : "Add to favourites");

            if (GUI.Button(new Rect(width - copyW - U(38f), y + U(2f), copyW, U(26f)), "Copy name", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = entry.Name;
                Session.Say($"Copied \"{entry.Name}\".");
            }
            y += U(34f);

            // Kind in colour, then the prefab name (when the game shows another) and where it comes from.
            var x = _compact ? KindBadge(entry, new Vector2(0f, y)) + U(10f) : 0f;
            var origin = OriginText(entry);
            var sub = entry.Name == primary ? origin : entry.Name + (origin.Length > 0 ? "   ·   " + origin : "");
            // The second line is kept even when empty: the link to fold every section sits at its end.
            {
                var subRect = new Rect(x, y, width - x - _foldAllW, U(22f));
                if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
                // Where a mod added it, the line goes to the mod's own page, or else shows everything it added.
                if (entry.Origin == Origin.Mod && entry.ModName.Length > 0 && entry.Kind != Kind.Mod)
                {
                    var page = EntryKeys.For(Kind.Mod, entry.ModName);
                    var known = InCatalog(explorer, page);
                    var guess = UnsureWords.IsSureClue(entry.ModClue) ? "" : UnsureWords.ModClue(entry.ModClue) + "\n";
                    if (subRect.Contains(Event.current.mousePosition)) AskTip("mod" + guess, guess + (known ? "Go to " + entry.ModName + ": what it adds and changes" : "Show everything " + entry.ModName + " added"));
                    if (GUI.Button(subRect, GUIContent.none, GUIStyle.none))
                    {
                        if (known) Go(explorer, page);
                        else SearchFor(explorer, "mod:" + entry.ModName.Replace(" ", "").ToLowerInvariant());
                    }
                }
                y += U(26f);
            }

            return y + U(8f);
        }

        /// <summary>
        /// Draws a label, shrinking its text step by step until it fits, down to a smallest size.
        /// Returns false when even that was too wide and the text is cut.
        /// </summary>
        private static bool FitLabel(Rect rect, string text, GUIStyle style, float smallest)
        {
            var original = style.fontSize;
            var floor = Mathf.Max(1, Mathf.RoundToInt(smallest * _s));
            var fits = Skin.Width(style, text) <= rect.width;
            while (!fits && style.fontSize > floor)
            {
                style.fontSize -= 1;
                fits = Skin.Width(style, text) <= rect.width;
            }
            GUI.Label(rect, text, style);
            style.fontSize = original;
            return fits;
        }

        private static float Actions(Entry entry, float width, float y, bool withStage)
        {
            var x = 0f;
            var rowH = U(32f);
            var any = false;

            bool Button(string text, GUIStyle style) => Shown(text, style, true);

            // A button that is always there, greyed out while it can do nothing, so the row
            // does not shift as it comes and goes.
            bool Shown(string text, GUIStyle style, bool can)
            {
                any = true;
                var w = Skin.Width(style, text) + U(12f);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var enabled = GUI.enabled;
                GUI.enabled = enabled && can;
                var clicked = GUI.Button(new Rect(x, y, w, rowH), text, style);
                GUI.enabled = enabled;
                x += w + U(8f);
                return clicked && can;
            }

            string note = null;
            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button(Variants(entry).Count > 1 ? "Play a random one" : "Play", Skin.Primary)) Previews.PlaySound(entry);
                    var sounding = Previews.SoundPlaying;
                    if (Shown(sounding && Previews.SoundPaused ? "Resume" : "Pause", Skin.Button, sounding)) Previews.PauseSound(!Previews.SoundPaused);
                    if (Shown("Stop", Skin.Button, sounding)) Previews.StopSound();
                    if (Button("Repeat", Previews.LoopSounds ? Skin.On : Skin.Button)) Previews.LoopSounds = !Previews.LoopSounds;
                    break;

                case Kind.Effect:
                    var there = Previews.Playing.IsPlaying("there:" + entry.Name);
                    if (Button("Play where you look", there ? Skin.On : Skin.Primary))
                    {
                        if (there) Previews.Stop("there:" + entry.Name);
                        else Previews.PlayEffect(entry, onYou: false);
                    }
                    var onYou = Previews.Playing.IsPlaying("on you:" + entry.Name);
                    if (Button("Play on you", onYou ? Skin.On : Skin.Button))
                    {
                        if (onYou) Previews.Stop("on you:" + entry.Name);
                        else Previews.PlayEffect(entry, onYou: true);
                    }
                    if (!_compact)
                    {
                        if (Button("Replay", Skin.Button)) Previews.Replay();
                        if (Button("Repeat", Previews.LoopEffects ? Skin.On : Skin.Button)) Previews.LoopEffects = !Previews.LoopEffects;
                    }
                    break;

                case Kind.StatusEffect:
                    note = StatusActions(entry, Button);
                    break;

                case Kind.Raid:
                    // A raid's wave, rolled again on a new copy.
                    if (withStage && Stage.IsStaged(entry))
                    {
                        if (Shown("Roll again", Skin.Primary, Stage.Subject != null)) Previews.Rebuild();
                        if (RaidCrowd.LastFor == entry && Stage.Subject != null) note = RaidWords.Wave(RaidCrowd.LastWave, WaveName);
                    }
                    break;

                case Kind.Biome:
                    // Everything there, as the search finds it.
                    if (Button("Everything here", Skin.Primary) && Session.Explorer != null) SearchFor(Session.Explorer, "biome:" + entry.Name.ToLowerInvariant());
                    break;

                case Kind.Location:
                    // What the game leaves to chance, rolled again on a new copy; only where something is.
                    if (withStage && entry.Source is PlaceSource place && place.Contents != null && place.Contents.LeftToChance
                        && Shown("Roll again", Skin.Primary, Stage.Subject != null))
                    {
                        Previews.Rebuild();
                    }
                    // A dungeon's or camp's example, laid out anew, with what it holds or how far reading it has got.
                    if (withStage && entry.Source is PlaceSource laid && !laid.IsRoom && laid.Contents?.Dungeon != null)
                    {
                        var example = ExampleOf(entry);
                        if (Shown("Another example", Skin.Button, example != null)) ExampleLayouts.Another();
                        note = example != null
                            ? DungeonWords.Example(example, ExampleLayouts.Failed) + (example.Rooms.Count > 0 ? ". One way it can come out; each world lays out its own." : ".")
                            : ExampleLayouts.Of(entry) ? DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total) : null;
                    }
                    break;

                default:
                    if (entry.Kind == Kind.Projectile && Button("Fire where you look", Skin.Primary)) Previews.Fire(entry);
                    // Wearing it and keeping it on are one idea, side by side under the stage in
                    // both views; the stage's own chips only change how it is seen.
                    if (entry.Kind == Kind.Item && entry.Source is GameObject wearable && Gear.IsWearable(wearable)
                        && Button("Wear it", Looks.OnPerson ? Skin.On : Skin.Button))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    if (Looks.IsWorn(entry))
                    {
                        var kept = Looks.Outfit.Contains(entry.Name);
                        if (Button("Keep it on", kept ? Skin.On : Skin.Button))
                        {
                            if (kept) Looks.Outfit.TakeOff(entry.Name);
                            else Looks.Outfit.Keep(entry.Name, Gear.SlotOf((GameObject)entry.Source));
                            Previews.Rebuild();
                        }
                    }
                    var fallen = Previews.Playing.IsPlaying("ragdoll");
                    if (Previews.RagdollOf(entry) != null && Shown("Ragdoll", fallen ? Skin.On : Skin.Button, Stage.Subject != null))
                    {
                        if (fallen) Previews.Stop("ragdoll");
                        else Previews.Ragdoll();
                    }
                    var loose = Previews.Playing.IsPlaying("let fall");
                    if (Previews.CanLetFall(entry) && Shown("Let it fall", loose ? Skin.On : Skin.Button, Stage.Subject != null))
                    {
                        if (loose) Previews.Stop("let fall");
                        else Previews.LetFall();
                    }
                    if (Previews.IsModel(entry))
                    {
                        if (Button("Show in the world", Previews.InWorld ? Skin.On : Skin.Button)) Previews.ToggleWorld();
                        if (Previews.InWorld)
                        {
                            if (Button("Move to where you look", Skin.Button)) Previews.PlaceHere();
                            if (Button("Pin", Skin.Button))
                            {
                                Previews.Pin();
                                Session.Say("Pinned. It stays where it is until you press Clear.");
                            }
                        }
                    }
                    break;
            }


            if (any) y += rowH;

            if (note == null && (Previews.InWorld || Previews.PinnedCount > 0))
            {
                note = "Only you see it, and it is gone when you leave the world. Hold the right mouse button outside the panel to look around, or close the panel with F7: previews stay until you clear them.";
            }
            if (note != null)
            {
                var height = Skin.Height(Skin.DimWrap, note, width);
                GUI.Label(new Rect(0f, y + U(8f), width, height), note, Skin.DimWrap);
                y += height + U(8f);
            }

            return y + U(14f);
        }

        private static readonly Dictionary<string, string> WaveNames = new Dictionary<string, string>();

        /// <summary>A creature of a raid's wave by the name the game shows for it.</summary>
        private static string WaveName(string prefab)
        {
            if (!WaveNames.TryGetValue(prefab, out var name))
            {
                var shown = Session.Explorer?.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == prefab)?.DisplayName;
                name = string.IsNullOrEmpty(shown) ? prefab : shown;
                WaveNames[prefab] = name;
            }
            return name;
        }

        /// <summary>
        /// A status effect's buttons: show its start visuals on you and take them off again, and
        /// play any other list it has once. Returns the note to show under them.
        /// </summary>
        private static string StatusActions(Entry entry, Func<string, GUIStyle, bool> button)
        {
            var effect = entry.Source as StatusEffect;
            var lists = StatusLists(entry);
            if (effect == null || lists.Count == 0) return "It has no visuals or sounds of its own.";

            var hasStart = lists.Any(l => l.Value == effect.m_startEffects);
            if (hasStart)
            {
                // One button that lights while it is on you, as every switch in the panel does.
                if (button("Show it on you", Previews.StatusShowing ? Skin.On : Skin.Primary))
                {
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                }
            }

            foreach (var list in lists)
            {
                if (list.Value == effect.m_startEffects) continue;
                var style = Previews.Playing.IsPlaying(list.Value) ? Skin.On : hasStart ? Skin.Button : Skin.Primary;
                if (button("Play " + list.Key.ToLowerInvariant(), style))
                {
                    if (Previews.Playing.IsPlaying(list.Value)) Previews.Stop(list.Value);
                    else Previews.PlayOnYou(list.Value);
                }
            }

            return "Only the look. The effect itself is never applied to you.";
        }

        /// <summary>
        /// What the person keeps on while other items are tried on over it. Each piece goes to its
        /// item when clicked, and comes off with its cross.
        /// </summary>
        private static float Wearing(Explorer explorer, float width, float y)
        {
            var kept = Looks.Outfit.Keys;
            if (kept.Count == 0) return y;

            y = SectionHeading("KEPT ON", width, y, () =>
            {
                foreach (var key in kept.ToList()) Looks.Outfit.TakeOff(key);
                Previews.Rebuild();
            }, "kept");
            if (IsFolded("kept")) return y;

            var x = 0f;
            var chipH = U(30f);
            foreach (var key in kept.ToList())
            {
                var prefab = Looks.Prefab(key);
                var shared = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
                if (name.Length == 0) name = key;

                var w = Mathf.Min(width, Skin.Width(Skin.Chip, name) + U(58f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += chipH + U(5f);
                }
                var chip = new Rect(x, y, w, chipH);
                var cross = new Rect(chip.xMax - U(26f), chip.y, U(24f), chipH);
                var hover = chip.Contains(Event.current.mousePosition);
                Skin.PillBox(chip, LinkFill(Kind.Item, hover));
                var icon = PrefabIcon(key);
                if (icon != null) DrawSprite(icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                var small = Skin.Small;
                var smallWas = small.normal.textColor;
                small.normal.textColor = LinkText(Kind.Item, hover);
                GUI.Label(new Rect(chip.x + U(32f), chip.y, chip.width - U(62f), chip.height), name, small);
                small.normal.textColor = smallWas;
                GUI.Label(cross, "\u00d7", Skin.Cross);

                if (GUI.Button(cross, GUIContent.none, GUIStyle.none))
                {
                    Looks.Outfit.TakeOff(key);
                    Previews.Rebuild();
                }
                else if (GUI.Button(new Rect(chip.x, chip.y, chip.width - U(26f), chip.height), GUIContent.none, GUIStyle.none) && explorer.Jump(key))
                {
                    _reveal = true;
                    _sideScroll = Vector2.zero;
                }
                if (hover) AskTip("kept:" + key, cross.Contains(Event.current.mousePosition) ? "Take it off" : "Go to " + name);
                x += w + U(6f);
            }

            return y + chipH + U(16f);
        }

        private static string OriginText(Entry entry)
        {
            if (entry.Kind == Kind.Mod) return "a mod loaded";
            if (entry.Origin == Origin.Vanilla) return "from the game";
            if (entry.Origin != Origin.Mod) return "";
            if (entry.ModName.Length == 0) return "added by a mod";
            return UnsureWords.IsSureClue(entry.ModClue) ? "added by " + entry.ModName : UnsureWords.Marked("added by " + entry.ModName);
        }

        private static readonly Dictionary<(string, string), string> ChipLabels = new Dictionary<(string, string), string>();

        /// <summary>A chip's text that says what it is ("Light: Studio"), made once for each choice.</summary>
        private static string Labelled(string prefix, string[] names, int index)
        {
            var name = names[index];
            if (!ChipLabels.TryGetValue((prefix, name), out var label)) ChipLabels[(prefix, name)] = label = prefix + name;
            return label;
        }

        /// <summary>
        /// Front, side and top views and a fit, in the stage's bottom right corner. Picking a view
        /// holds the model still, so it stays in that view. Returns the width they take.
        /// </summary>
        private static float ViewButtons(Rect inner)
        {
            var h = U(22f);
            var y = inner.yMax - U(8f) - h;
            var x = inner.xMax - U(8f);
            var views = new[] { ("Fit", "Frame it whole again"), ("Top", "Look down on it"), ("Side", "Look at it from the side"), ("Front", "Look at it from the front") };
            foreach (var (name, tip) in views)
            {
                var w = Skin.Width(Skin.Chip, name) + U(2f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(4f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("view:" + name, tip);
                if (!GUI.Button(chip, name, Skin.Chip)) continue;

                Stage.View(name);
                if (name != "Fit" && Stage.Spin)
                {
                    Stage.Spin = false;
                    SaveRects();
                }
            }
            return inner.xMax - U(8f) - x;
        }

        /// <summary>The person for size, the lighting and the backdrop, in the stage's top right corner.</summary>
        private const string StageHint = "Drag to turn, right-drag to move, scroll to zoom, double-click to reset";
        private const string StageHintCut = "Drag to turn, right-drag to move, scroll to zoom, Page Up/Down or the ruler for floors, Shift-scroll to move the cut";

        private static void StageButtons(Entry entry, Rect rect)
        {
            var h = U(24f);
            var y = rect.y + U(10f);
            var x = rect.xMax - U(10f);

            // Everything the row will hold, measured first, so it can move clear of the kind badge.
            var backdrop = Labelled("Backdrop: ", Stage.BackdropNames, Stage.BackdropIndex);
            var lighting = Labelled("Light: ", Stage.LightingNames, Stage.LightingIndex);
            var texts = new List<string> { backdrop, lighting, "Spin" };
            if (!Looks.IsWorn(entry)) texts.Add("Person");
            if (Stage.HasFloors)
            {
                texts.Add(Stage.CutLabel);
                texts.Add("\u25B2");
                texts.Add("\u25BC");
            }
            if (Stage.HasInside) texts.Add(Stage.Inside ? "Inside" : "Outside");
            if (ExampleOf(entry) != null) texts.Add("Plan");
            var total = texts.Sum(t => Skin.Width(Skin.Chip, t) + U(10f));
            if (x - total < rect.x + U(10f) + _badgeWidth + U(10f)) y += h + U(8f);

            bool Chip(string text, bool on, string tip, bool can = true)
            {
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Skin.Width(style, text) + U(4f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(6f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("stage:" + tip, tip);
                var enabled = GUI.enabled;
                GUI.enabled = enabled && can;
                var clicked = GUI.Button(chip, text, style);
                GUI.enabled = enabled;
                return clicked && can;
            }

            if (Chip("Spin", Stage.Spin, Stage.Spin ? "Turning; click to hold it still" : "Held still; click to turn it"))
            {
                Stage.Spin = !Stage.Spin;
                SaveRects();
            }
            if (Chip(backdrop, false, "Click for the next backdrop"))
            {
                Stage.BackdropIndex = (Stage.BackdropIndex + 1) % Stage.BackdropNames.Length;
                SaveRects();
            }
            if (Chip(lighting, false, "Click for the next lighting"))
            {
                Stage.LightingIndex = (Stage.LightingIndex + 1) % Stage.LightingNames.Length;
                SaveRects();
            }
            if (!Looks.IsWorn(entry) && Chip("Person", Stage.ShowPerson, "A person beside it, to judge its size"))
            {
                Stage.ShowPerson = !Stage.ShowPerson;
                SaveRects();
            }
            if (ExampleOf(entry) != null && Chip("Plan", !PlanFolded, PlanFolded ? "Shows the example's plan in the stage's corner" : "Puts the example's plan away"))
            {
                PlanFolded = !PlanFolded;
            }
            if (Stage.HasInside && Chip(Stage.Inside ? "Inside" : "Outside", Stage.Inside,
                    Stage.Inside ? "The example dungeon, laid out as the game lays out a new one; click for its entrance outside" : "Its entrance; click for the example dungeon inside"))
            {
                Stage.Inside = !Stage.Inside;
            }
            if (Stage.HasFloors)
            {
                if (Chip(Stage.CutLabel, Stage.Cutting, Stage.Cutting ? "Cut open above head height over a floor; click to put the roof back on" : "Click to take the roof off, cutting away what is above head height over a floor"))
                {
                    Stage.ToggleRoof();
                }
                var floors = Stage.FloorHeights.Count;
                if (Chip("\u25BC", false, "A floor down (Page Down over the stage)", !Stage.Cutting || Stage.CutLevel < floors - 1)) Stage.StepCut(true);
                if (Chip("\u25B2", false, "A floor up, then the roof back on (Page Up over the stage)", Stage.Cutting)) Stage.StepCut(false);
            }
        }
    }
}
