using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        /// <summary>The entry the side was last shown for; another starts at its top.</summary>
        private static Entry _sideFor;

        private static void Side(Explorer explorer, Rect rect, bool withStage)
        {
            var entry = explorer.Selected;
            if (!ReferenceEquals(entry, _sideFor))
            {
                _sideFor = entry;
                _sideScroll = Vector2.zero;
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
                _stageBaseH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
                var stageH = Mathf.Round(Mathf.Clamp(_stageBaseH * _stageScale, U(120f), rect.height * 0.85f));
                Section("side stage", 0f, _ => { StageArea(entry, new Rect(rect.x, rect.y, rect.width, stageH)); return 0f; });
                StageHandle(new Rect(rect.x, rect.y + stageH, rect.width, U(12f)));
                top += stageH + U(12f);
            }

            var below = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            _sideVisible = new Rect(0f, _sideScroll.y, content.width, below.height);

            var cw = content.width;
            var y = 0f;
            _foldAllShown = false;
            y = Section("side title", y, at => Title(explorer, entry, cw, at));
            if (!withStage && (entry.Kind == Kind.Sound || entry.Kind == Kind.StatusEffect)) y = Section("side card", y, at => CompactCard(entry, cw, at));
            y = Section("side actions", y, at => Actions(entry, cw, at));
            if (Looks.IsWorn(entry)) y = Section("side wearing", y, at => Wearing(explorer, cw, at));
            if (entry.Kind == Kind.Sound)
            {
                y = Section("side timeline", y, at => Timeline(cw, at));
                y = Section("side variants", y, at => Variants(entry, cw, at));
            }
            y = Section("side adjust", y, at => Adjust(explorer, entry, cw, at, withStage));
            y = Section("side effects", y, at => Effects(explorer, entry, cw, at, withStage));
            y = Section("side plays in", y, at => PlaysInSection(explorer, entry, cw, at));
            y = Section("side links", y, at => LinksSection(explorer, entry, cw, at));
            y = Section("side facts", y, at => FactsSection(explorer, entry, cw, at));
            y = Section("side command", y, at => Command(explorer, entry, cw, at));
            y = Section("side details", y, at => Details(explorer, entry, cw, at));
            if (Event.current.type == EventType.Repaint) _sideHeight = y + U(8f);

            GUI.EndScrollView();
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

        private static void StageArea(Entry entry, Rect rect)
        {
            var e = Event.current;
            Skin.Box(rect, Skin.Stage);

            if (Stage.IsStaged(entry))
            {
                var inner = new Rect(rect.x + U(3f), rect.y + U(3f), rect.width - U(6f), rect.height - U(6f));
                if (e.type == EventType.Repaint) Stage.Request((int)inner.width, (int)inner.height);

                if (Stage.Subject != null && Stage.Texture != null)
                {
                    if (e.type == EventType.Repaint) GUI.DrawTexture(inner, Stage.Texture, ScaleMode.StretchToFill, false);
                }
                else if (entry.Kind != Kind.Effect)
                {
                    GUI.Label(inner, "This one could not be previewed.", Skin.CenterDim);
                }

                var viewsW = ViewButtons(inner);
                var textW = inner.width - U(24f) - viewsW;
                if (rect.Contains(e.mousePosition) || _drag == Drag.Orbit)
                {
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        "Drag to turn, right-drag to move, scroll to zoom, double-click to reset", Skin.FaintLabel, 9f);
                }
                else if (Stage.Subject != null && Stage.ShowsGrid)
                {
                    // On the grid, how big the model is, in the same metres as its squares.
                    var size = Stage.SubjectSize;
                    string M(float v) => v.ToString(v < 10f ? "0.0" : "0", CultureInfo.InvariantCulture);
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        $"Squares of 1 m, lines every 5 m  \u00B7  {M(size.y)} m tall, {M(size.x)} × {M(size.z)} m", Skin.DimLabel, 9f);
                }

                // The stage's own buttons come before its dragging, which would otherwise take their clicks.
                StageButtons(entry, rect);

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount == 2) Stage.ResetView();
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
                    Stage.ZoomBy(e.delta.y);
                    e.Use();
                }
            }
            else if (entry.Kind == Kind.Sound)
            {
                SoundCard(entry, rect);
            }
            else if (entry.Kind == Kind.StatusEffect)
            {
                StatusCard(entry, rect);
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

        /// <summary>In compact view, the facts the stage card would show, as lines of text.</summary>
        private static float CompactCard(Entry entry, float width, float y)
        {
            string text;
            if (entry.Kind == Kind.Sound)
            {
                text = SoundFacts(entry);
            }
            else
            {
                var effect = entry.Source as StatusEffect;
                if (effect == null) return y;
                var tooltip = CatalogBuilder.Localize(effect.m_tooltip);
                text = StatusFacts(effect) + (tooltip.Length > 0 ? "\n" + tooltip : "");
            }

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

        private static void SoundCard(Entry entry, Rect rect)
        {
            // Bars that move while the sound plays.
            var bars = 24;
            var barW = U(6f);
            var gap = U(5f);
            var total = bars * barW + (bars - 1) * gap;
            var x = rect.x + (rect.width - total) / 2f;
            var mid = rect.y + rect.height * 0.42f;
            var playing = Previews.SoundPlaying && !Previews.SoundPaused;
            var accent = Skin.KindColor(Kind.Sound);

            for (var i = 0; i < bars; i++)
            {
                var shape = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(i * 0.9f + 0.6f)) * Mathf.Sin((i + 1f) / (bars + 1f) * Mathf.PI);
                var motion = playing ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f + i * 0.7f) : 0.35f;
                var height = U(90f) * shape * motion + U(6f);
                var bar = new Rect(x + i * (barW + gap), mid - height / 2f, barW, height);
                Skin.PillBox(bar, playing ? accent : new Color(accent.r, accent.g, accent.b, 0.35f));
            }

            GUI.Label(new Rect(rect.x + U(20f), mid + U(64f), rect.width - U(40f), U(26f)), SoundFacts(entry), Skin.CenterDim);
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
            if (!string.IsNullOrEmpty(effect.m_category)) parts.Add("Category: " + effect.m_category);
            parts.Add(effect.m_ttl > 0f ? "Lasts " + Duration(effect.m_ttl) : "No time limit of its own");
            return string.Join("    ", parts);
        }

        private static void StatusCard(Entry entry, Rect rect)
        {
            var effect = entry.Source as StatusEffect;
            var iconSize = U(84f);
            var icon = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + U(34f), iconSize, iconSize);
            if (effect != null && effect.m_icon != null) DrawIcon(entry, icon);
            else DrawIcon(new Entry { Kind = Kind.StatusEffect }, icon);

            if (effect == null) return;
            var y = icon.yMax + U(12f);
            GUI.Label(new Rect(rect.x + U(20f), y, rect.width - U(40f), U(22f)), StatusFacts(effect), Skin.CenterDim);

            var tooltip = CatalogBuilder.Localize(effect.m_tooltip);
            if (tooltip.Length > 0)
            {
                GUI.Label(new Rect(rect.x + U(30f), y + U(28f), rect.width - U(60f), rect.yMax - y - U(34f)), tooltip, Skin.CenterDim);
            }
        }

        private static string Duration(float seconds)
        {
            if (seconds >= 120f) return $"{Mathf.RoundToInt(seconds / 60f)} min";
            return $"{Mathf.RoundToInt(seconds)} s";
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
            if (sub.Length > 0 || _compact)
            {
                var subRect = new Rect(x, y, width - x, U(22f));
                if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
                if (entry.Origin == Origin.Mod && entry.ModName.Length > 0)
                {
                    if (subRect.Contains(Event.current.mousePosition)) AskTip("mod", "Show everything " + entry.ModName + " added");
                    if (GUI.Button(subRect, GUIContent.none, GUIStyle.none)) SearchFor(explorer, "mod:" + entry.ModName.Split(' ')[0].ToLowerInvariant());
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

        private static float Actions(Entry entry, float width, float y)
        {
            var x = 0f;
            var rowH = U(32f);
            var any = false;

            bool Button(string text, GUIStyle style)
            {
                any = true;
                var w = Skin.Width(style, text) + U(12f);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var clicked = GUI.Button(new Rect(x, y, w, rowH), text, style);
                x += w + U(8f);
                return clicked;
            }

            string note = null;
            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button(Variants(entry).Count > 1 ? "Play a random one" : "Play", Skin.Primary)) Previews.PlaySound(entry);
                    if (Previews.SoundPlaying)
                    {
                        if (Button(Previews.SoundPaused ? "Resume" : "Pause", Skin.Button)) Previews.PauseSound(!Previews.SoundPaused);
                        if (Button("Stop", Skin.Button)) Previews.StopSound();
                    }
                    if (Button(Previews.LoopSounds ? "Repeat on" : "Repeat off", Previews.LoopSounds ? Skin.On : Skin.Button)) Previews.LoopSounds = !Previews.LoopSounds;
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
                        if (Button(Previews.LoopEffects ? "Repeat on" : "Repeat off", Previews.LoopEffects ? Skin.On : Skin.Button)) Previews.LoopEffects = !Previews.LoopEffects;
                    }
                    break;

                case Kind.StatusEffect:
                    note = StatusActions(entry, Button);
                    break;

                default:
                    if (entry.Kind == Kind.Projectile && Button("Fire where you look", Skin.Primary)) Previews.Fire(entry);
                    if (Looks.IsWorn(entry))
                    {
                        var kept = Looks.Outfit.Contains(entry.Name);
                        if (Button(kept ? "Kept on" : "Keep it on", kept ? Skin.On : Skin.Button))
                        {
                            if (kept) Looks.Outfit.TakeOff(entry.Name);
                            else Looks.Outfit.Keep(entry.Name, Gear.SlotOf((GameObject)entry.Source));
                            Previews.Rebuild();
                        }
                    }
                    if (_compact && entry.Kind == Kind.Item && entry.Source is GameObject wearable && Gear.IsWearable(wearable)
                        && Button(Looks.OnPerson ? "Worn by a person" : "Wear it", Looks.OnPerson ? Skin.On : Skin.Button))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    var fallen = Previews.Playing.IsPlaying("ragdoll");
                    if (Previews.RagdollOf(entry) != null && Stage.Subject != null && Button("Ragdoll", fallen ? Skin.On : Skin.Button))
                    {
                        if (fallen) Previews.Stop("ragdoll");
                        else Previews.Ragdoll();
                    }
                    var loose = Previews.Playing.IsPlaying("let fall");
                    if (Previews.CanLetFall(entry) && Stage.Subject != null && Button("Let it fall", loose ? Skin.On : Skin.Button))
                    {
                        if (loose) Previews.Stop("let fall");
                        else Previews.LetFall();
                    }
                    if (Previews.IsModel(entry))
                    {
                        if (Button(Previews.InWorld ? "Showing in the world" : "Show in the world", Previews.InWorld ? Skin.On : (entry.Kind == Kind.Projectile ? Skin.Button : Skin.Primary))) Previews.ToggleWorld();
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
                if (Previews.StatusShowing)
                {
                    if (button("Take it off you", Skin.Primary)) Previews.StopStatus(true);
                }
                else if (button("Show it on you", Skin.Primary))
                {
                    Previews.ShowStatus(entry);
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
            if (entry.Origin == Origin.Vanilla) return "from the game";
            if (entry.Origin != Origin.Mod) return "";
            return entry.ModName.Length > 0 ? "added by " + entry.ModName : "added by a mod";
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
        private static void StageButtons(Entry entry, Rect rect)
        {
            var h = U(24f);
            var y = rect.y + U(10f);
            var x = rect.xMax - U(10f);

            // Everything the row will hold, measured first, so it can move clear of the kind badge.
            var wearable = entry.Kind == Kind.Item && entry.Source is GameObject wornItem && Gear.IsWearable(wornItem);
            var texts = new List<string> { Stage.BackdropNames[Stage.BackdropIndex], Stage.LightingNames[Stage.LightingIndex], "Spin" };
            if (wearable) texts.Add("Worn");
            if (!Looks.IsWorn(entry)) texts.Add("Person");
            var total = texts.Sum(t => Skin.Width(Skin.Chip, t) + U(10f));
            if (x - total < rect.x + U(10f) + _badgeWidth + U(10f)) y += h + U(8f);

            bool Chip(string text, bool on, string tip)
            {
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Skin.Width(style, text) + U(4f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(6f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("stage:" + tip, tip);
                return GUI.Button(chip, text, style);
            }

            if (Chip("Spin", Stage.Spin, Stage.Spin ? "Turning; click to hold it still" : "Held still; click to turn it"))
            {
                Stage.Spin = !Stage.Spin;
                SaveRects();
            }
            if (Chip(Stage.BackdropNames[Stage.BackdropIndex], false, "Backdrop: click for the next"))
            {
                Stage.BackdropIndex = (Stage.BackdropIndex + 1) % Stage.BackdropNames.Length;
                SaveRects();
            }
            if (Chip(Stage.LightingNames[Stage.LightingIndex], false, "Lighting: click for the next"))
            {
                Stage.LightingIndex = (Stage.LightingIndex + 1) % Stage.LightingNames.Length;
                SaveRects();
            }
            if (wearable && Chip("Worn", Looks.OnPerson, "Show it worn by a person"))
            {
                Looks.OnPerson = !Looks.OnPerson;
                Previews.Rebuild();
                SaveRects();
            }
            if (!Looks.IsWorn(entry) && Chip("Person", Stage.ShowPerson, "A person beside it, to judge its size"))
            {
                Stage.ShowPerson = !Stage.ShowPerson;
                SaveRects();
            }
        }
    }
}
