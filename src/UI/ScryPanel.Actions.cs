using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>What can be done with the selected entry: its action buttons in the toolbar under its name, what they note at the page's top, a status effect's, what a person wears, and where it comes from.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>How an action button looks: plain, on as the panel's switches are while on, or leading, the accent, for what sends something into the world.</summary>
        private enum ActionLook
        {
            Plain,
            On,
            Leading,
        }

        /// <summary>The widths of the entry's action buttons as the measuring pass found them, and where the toolbar puts each.</summary>
        private static readonly List<float> ActionWidths = new List<float>();
        private static readonly List<(float X, int Line)> ActionPlaces = new List<(float, int)>();

        /// <summary>Where the toolbar's first line stands while its actions are drawn.</summary>
        private static Vector2 _toolbarAt;

        /// <summary>What the actions note at the page's top, as their last pass said.</summary>
        private static string _actionNote;

        /// <summary>The faint caption after the actions in the toolbar, as their last pass said; none for most.</summary>
        private static string _actionCaption;

        /// <summary>Measures the entry's action buttons for the toolbar to lay out (<see cref="ActionWidths"/>).</summary>
        private static void MeasureActions(Entry entry, bool withStage) => _actionNote = ActionButtons(entry, withStage, draw: false, out _actionCaption);

        /// <summary>Draws the entry's action buttons where the toolbar laid them out, its first line at a point.</summary>
        private static void DrawActions(Entry entry, bool withStage, Vector2 at)
        {
            _toolbarAt = at;
            _actionNote = ActionButtons(entry, withStage, draw: true, out _actionCaption);
        }

        /// <summary>How tall a line of the toolbar is, and the gap between its lines.</summary>
        private static float ToolbarRow => U(24f);
        private static float ToolbarLineGap => U(4f);

        /// <summary>
        /// The entry's action buttons, in the order they stand: measured (their widths into
        /// <see cref="ActionWidths"/>), or drawn at the places the toolbar gave them
        /// (<see cref="ActionPlaces"/>), the pieces of a line joined into one. A button that can do
        /// nothing for now stays, greyed, so the group does not shift; one that needs the stage is
        /// left out without one. Returns the note to show at the page's top, and gives the faint
        /// caption to stand after the buttons.
        /// </summary>
        private static string ActionButtons(Entry entry, bool withStage, bool draw, out string caption)
        {
            caption = null;
            if (!draw) ActionWidths.Clear();
            var index = 0;

            bool Button(string text, ActionLook look, string tip) => Shown(text, look, true, tip);

            bool Shown(string text, ActionLook look, bool can, string tip)
            {
                var w = Skin.Width(Skin.SmallCenter, text) + U(22f);
                if (!draw)
                {
                    ActionWidths.Add(w);
                    return false;
                }
                var i = index++;
                if (i >= ActionPlaces.Count) return false;
                var (x, line) = ActionPlaces[i];
                var rect = new Rect(_toolbarAt.x + x, _toolbarAt.y + line * (ToolbarRow + ToolbarLineGap), w, ToolbarRow);
                var first = i == 0 || ActionPlaces[i - 1].Line != line;
                var last = i == ActionPlaces.Count - 1 || ActionPlaces[i + 1].Line != line;
                var hover = can && rect.Contains(Event.current.mousePosition);
                var fill = look == ActionLook.Leading ? (hover ? Skin.Shade(Skin.Accent, 1.08f) : Skin.Accent)
                    : look == ActionLook.On ? Skin.Alpha(Skin.Accent, hover ? 0.28f : 0.2f)
                    : hover ? Skin.RaisedHover : Skin.Raised;
                Skin.JoinedPiece(rect, fill, first, last);
                var ink = look == ActionLook.Leading ? Skin.OnAccent : look == ActionLook.On ? Skin.Accent : Skin.Text;
                Skin.LabelIn(rect, text, Skin.SmallCenter, can ? ink : Skin.Alpha(ink, 0.4f));
                if (rect.Contains(Event.current.mousePosition)) AskTip("action:" + text, tip);
                if (Event.current.type == EventType.Repaint) ToolbarPartsForTest.Add(("action", rect));
                return can && GUI.Button(rect, GUIContent.none, GUIStyle.none);
            }

            string note = null;
            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button(SoundWords.Play(Variants(entry).Count), ActionLook.Leading, ActionWords.PlaySound(Variants(entry).Count))) Previews.PlaySound(entry);
                    var sounding = Previews.SoundPlaying;
                    if (Shown(PanelWords.Pause(sounding && Previews.SoundPaused), ActionLook.Plain, sounding, ActionWords.PauseSound(Previews.SoundPaused))) Previews.PauseSound(!Previews.SoundPaused);
                    if (Shown("Stop", ActionLook.Plain, sounding, ActionWords.StopSound)) Previews.StopSound();
                    break;

                case Kind.Effect:
                    var there = Previews.Playing.IsPlaying(PlayKey.There(entry.Name));
                    if (Button("Play where you look", there ? ActionLook.On : ActionLook.Leading, ActionWords.PlayThere(there)))
                    {
                        if (there) Previews.Stop(PlayKey.There(entry.Name));
                        else Previews.PlayEffect(entry, onYou: false);
                    }
                    var onYou = Previews.Playing.IsPlaying(PlayKey.OnYou(entry.Name));
                    if (Button("Play on you", onYou ? ActionLook.On : ActionLook.Plain, ActionWords.PlayOnYou(onYou)))
                    {
                        if (onYou) Previews.Stop(PlayKey.OnYou(entry.Name));
                        else Previews.PlayEffect(entry, onYou: true);
                    }
                    if (withStage && Button("Replay", ActionLook.Plain, ActionWords.Replay)) Previews.Replay();
                    break;

                case Kind.StatusEffect:
                    caption = StatusActions(entry, Button);
                    break;

                case Kind.Raid:
                    // What a raid's roll brought, rolled again on a new copy.
                    if (withStage && Stage.IsStaged(entry))
                    {
                        if (Shown("Roll again", ActionLook.Leading, Stage.Subject != null, ActionWords.RollRaid)) Previews.Rebuild();
                        if (RaidCrowd.LastFor == entry && Stage.Subject != null) note = RaidWords.FirstRoll(RaidCrowd.LastRoll, RolledName, RaidCrowd.LastNotMet);
                    }
                    break;

                case Kind.Biome:
                    // Everything there, as the search finds it.
                    if (Button("Find everything here", ActionLook.Leading, ActionWords.EverythingHere) && _explorer != null) SearchFor(_explorer, SearchHelp.Term("biome", entry.Name));
                    break;

                case Kind.Location:
                    // What the game leaves to chance, rolled again on a new copy; only where something is, and
                    // not on the world's own dungeon, shown by its layout alone.
                    if (withStage && entry.Source is PlaceSource place && place.Contents != null && place.Contents.LeftToChance
                        && !(ExampleLayouts.ShowsWorlds && ExampleLayouts.Of(entry))
                        && Shown("Roll again", ActionLook.Leading, Stage.Subject != null, ActionWords.RollPlace))
                    {
                        Previews.Rebuild();
                    }
                    // A dungeon's or camp's example, laid out anew, with what it holds or how far reading it has got.
                    if (withStage && entry.Source is PlaceSource laid && !laid.IsRoom && laid.Contents?.Dungeon != null)
                    {
                        var example = ExampleOf(entry);
                        if (Shown("Another example", ActionLook.Plain, example != null, ActionWords.AnotherLayout)) ExampleLayouts.Another();
                        note = example != null
                            ? ExampleLayouts.ShowsWorlds ? DungeonWords.WorldsNote(DungeonWords.Example(example, ExampleLayouts.Failed), ExampleLayouts.Unread,
                                ExampleLayouts.Fogged ? PlanBoxWords.Walked(ExampleLayouts.Walked.Walked, ExampleLayouts.Walked.Rooms) : null)
                            : DungeonWords.ExampleNote(DungeonWords.Example(example, ExampleLayouts.Failed), example.Rooms.Count > 0)
                            : ExampleLayouts.Of(entry) ? DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total) : null;
                    }
                    break;

                default:
                    if (entry.Kind == Kind.Projectile && Button("Fire where you look", ActionLook.Leading, ActionWords.Fire)) Previews.Fire(entry);
                    // Wearing it and keeping it on are one idea, side by side in both views; the
                    // stage's own chips only change how it is seen.
                    if (entry.Kind == Kind.Item && entry.Source is GameObject wearable && PrefabGear.IsWearable(wearable)
                        && Button("Wear it", Looks.OnPerson ? ActionLook.On : ActionLook.Plain, ActionWords.Wear(Looks.OnPerson)))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    if (Looks.IsWorn(entry))
                    {
                        var kept = Looks.Outfit.Contains(entry.Name);
                        if (Button("Keep it on", kept ? ActionLook.On : ActionLook.Plain, ActionWords.Keep(kept)))
                        {
                            if (kept) Looks.Outfit.TakeOff(entry.Name);
                            else Looks.Outfit.Keep(entry.Name, PrefabGear.SlotOf((GameObject)entry.Source));
                            Previews.Rebuild();
                        }
                    }
                    // Falling as a ragdoll, or rolling loose, needs the stage's copy.
                    var fallen = Previews.Playing.IsPlaying(PlayKey.Ragdoll);
                    if (withStage && Previews.RagdollOf(entry) != null && Shown("Ragdoll", fallen ? ActionLook.On : ActionLook.Plain, Stage.Subject != null, ActionWords.Ragdoll(fallen)))
                    {
                        if (fallen) Previews.Stop(PlayKey.Ragdoll);
                        else Previews.Ragdoll();
                    }
                    var loose = Previews.Playing.IsPlaying(PlayKey.LetFall);
                    if (withStage && Previews.CanLetFall(entry) && Shown("Let it fall", loose ? ActionLook.On : ActionLook.Plain, Stage.Subject != null, ActionWords.LetFall(loose)))
                    {
                        if (loose) Previews.Stop(PlayKey.LetFall);
                        else Previews.LetFall();
                    }
                    if (Previews.IsModel(entry))
                    {
                        if (Button("Show in the world", Previews.InWorld ? ActionLook.On : ActionLook.Plain, ActionWords.InWorld(Previews.InWorld))) Previews.ToggleWorld();
                        // Shown in the world, at the group's end, so nothing before them moves.
                        if (Previews.InWorld)
                        {
                            if (Button("Move to where you look", ActionLook.Plain, ActionWords.MoveHere)) Previews.PlaceHere();
                            if (Button("Pin", ActionLook.Plain, ActionWords.Pin))
                            {
                                Previews.Pin();
                                Say("Pinned. It stays where it is until you press Clear world.");
                            }
                        }
                    }
                    break;
            }
            return note;
        }

        /// <summary>What the actions note at the page's top: what their last pass said, that a copy in the world is yours alone, and a dungeon's maps kept.</summary>
        private static float ActionNotes(Entry entry, float width, float y, bool withStage)
        {
            var note = _actionNote;
            if (note == null && (Previews.InWorld || Previews.PinnedCount > 0))
            {
                note = "Only you see it, and it is gone when you leave the world. Hold the right mouse button outside the panel to look around, or close the panel with F7: previews stay until you clear them.";
            }
            if (note != null)
            {
                var height = Skin.Height(Skin.DimWrap, note, width);
                GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
                y += height + U(8f);
            }
            // A dungeon's maps kept, each with its buttons.
            if (withStage && entry.Kind == Kind.Location) y = MapLines(entry, width, y);
            return y > 0f ? y + U(6f) : y;
        }


        private static readonly Dictionary<string, string> RolledNames = new Dictionary<string, string>();

        /// <summary>A creature a raid rolled, by the name the game shows for it.</summary>
        private static string RolledName(string prefab)
        {
            if (!RolledNames.TryGetValue(prefab, out var name))
            {
                var found = WorldCatalog.Find(prefab);
                name = found != null && found.Kind == Kind.Creature ? found.ShownName : prefab;
                RolledNames[prefab] = name;
            }
            return name;
        }

        /// <summary>
        /// A status effect's buttons: show its start visuals on you and take them off again, and
        /// play any other list it has once. Returns the faint caption to stand after them, that it
        /// is only the look, or where they would be, that it has none.
        /// </summary>
        private static string StatusActions(Entry entry, Func<string, ActionLook, string, bool> button)
        {
            var effect = entry.Source as StatusEffect;
            var lists = StatusLists(entry);
            if (effect == null || lists.Count == 0) return StatusEffectWords.NoLook;

            var hasStart = lists.Any(l => l.Value == effect.m_startEffects);
            if (hasStart)
            {
                // One button that lights while it is on you, as every switch in the panel does.
                if (button("Show it on you", Previews.StatusShowing ? ActionLook.On : ActionLook.Leading, ActionWords.ShowStatus(Previews.StatusShowing)))
                {
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                }
            }

            foreach (var list in lists)
            {
                if (list.Value == effect.m_startEffects) continue;
                var playing = Previews.Playing.IsPlaying(list.Value);
                var look = playing ? ActionLook.On : hasStart ? ActionLook.Plain : ActionLook.Leading;
                if (button(PanelWords.PlayList(list.Key), look, ActionWords.PlayStatusList(playing)))
                {
                    if (Previews.Playing.IsPlaying(list.Value)) Previews.Stop(list.Value);
                    else Previews.PlayOnYou(list.Value);
                }
            }

            return StatusEffectWords.OnlyTheLook;
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

            var flow = new ChipFlow(0f, width, y, U(30f), U(6f), U(5f));
            foreach (var key in kept.ToList())
            {
                var prefab = GamePrefabs.Item(key);
                var shared = prefab != null ? prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
                var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
                if (name.Length == 0) name = key;

                var w = Mathf.Min(width, Skin.Width(Skin.Chip, name) + U(58f));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                var cross = new Rect(chip.xMax - U(26f), chip.y, U(24f), chip.height);
                var hover = chip.Contains(Event.current.mousePosition);
                Skin.PillBox(chip, LinkFill(Kind.Item, hover));
                var icon = PrefabIcon(key);
                if (icon != null) DrawSprite(icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                Skin.LabelIn(new Rect(chip.x + U(32f), chip.y, chip.width - U(62f), chip.height), name, Skin.Small, LinkText(Kind.Item, hover));
                GUI.Label(cross, "\u00d7", Skin.Cross);

                if (GUI.Button(cross, GUIContent.none, GUIStyle.none))
                {
                    Looks.Outfit.TakeOff(key);
                    Previews.Rebuild();
                }
                else if (GUI.Button(new Rect(chip.x, chip.y, chip.width - U(26f), chip.height), GUIContent.none, GUIStyle.none) && explorer.Jump(key))
                {
                    AfterGoing();
                }
                if (hover) AskTip("kept:" + key, cross.Contains(Event.current.mousePosition) ? "Take it off" : PanelWords.GoTo(name));
            }

            return flow.RowBottom + U(16f);
        }

    }
}
