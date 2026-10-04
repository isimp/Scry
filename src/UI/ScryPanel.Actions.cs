using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>What can be done with the selected entry: its action buttons, a status effect's, what a person wears, and where it comes from.</summary>
    internal static partial class ScryPanel
    {
        private static float Actions(Entry entry, float width, float y, bool withStage)
        {
            var flow = new ChipFlow(0f, width, y, U(32f), U(8f), U(6f));

            bool Button(string text, GUIStyle style, string tip) => Shown(text, style, true, tip);

            // A button that is always there, greyed out while it can do nothing, so the row
            // does not shift as it comes and goes. Each says on hover what it does.
            bool Shown(string text, GUIStyle style, bool can, string tip)
            {
                var w = Skin.Width(style, text) + U(12f);
                var at = flow.Place(w);
                var rect = new Rect(at.X, at.Y, w, flow.RowHeight);
                var enabled = GUI.enabled;
                GUI.enabled = enabled && can;
                var clicked = GUI.Button(rect, text, style);
                GUI.enabled = enabled;
                if (rect.Contains(Event.current.mousePosition)) AskTip("action:" + text, tip);
                return clicked && can;
            }

            string note = null;
            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button(SoundWords.Play(Variants(entry).Count), Skin.Primary, ActionWords.PlaySound(Variants(entry).Count))) Previews.PlaySound(entry);
                    var sounding = Previews.SoundPlaying;
                    if (Shown(PanelWords.Pause(sounding && Previews.SoundPaused), Skin.Button, sounding, ActionWords.PauseSound(Previews.SoundPaused))) Previews.PauseSound(!Previews.SoundPaused);
                    if (Shown("Stop", Skin.Button, sounding, ActionWords.StopSound)) Previews.StopSound();
                    break;

                case Kind.Effect:
                    var there = Previews.Playing.IsPlaying(PlayKey.There(entry.Name));
                    if (Button("Play where you look", there ? Skin.On : Skin.Primary, ActionWords.PlayThere(there)))
                    {
                        if (there) Previews.Stop(PlayKey.There(entry.Name));
                        else Previews.PlayEffect(entry, onYou: false);
                    }
                    var onYou = Previews.Playing.IsPlaying(PlayKey.OnYou(entry.Name));
                    if (Button("Play on you", onYou ? Skin.On : Skin.Button, ActionWords.PlayOnYou(onYou)))
                    {
                        if (onYou) Previews.Stop(PlayKey.OnYou(entry.Name));
                        else Previews.PlayEffect(entry, onYou: true);
                    }
                    if (!_compact)
                    {
                        if (Button("Replay", Skin.Button, ActionWords.Replay)) Previews.Replay();
                    }
                    break;

                case Kind.StatusEffect:
                    note = StatusActions(entry, Button);
                    break;

                case Kind.Raid:
                    // What a raid's roll brought, rolled again on a new copy.
                    if (withStage && Stage.IsStaged(entry))
                    {
                        if (Shown("Roll again", Skin.Primary, Stage.Subject != null, ActionWords.RollRaid)) Previews.Rebuild();
                        if (RaidCrowd.LastFor == entry && Stage.Subject != null) note = RaidWords.FirstRoll(RaidCrowd.LastRoll, RolledName);
                    }
                    break;

                case Kind.Biome:
                    // Everything there, as the search finds it.
                    if (Button("Everything here", Skin.Primary, ActionWords.EverythingHere) && _explorer != null) SearchFor(_explorer, SearchHelp.Term("biome", entry.Name));
                    break;

                case Kind.Location:
                    // What the game leaves to chance, rolled again on a new copy; only where something is.
                    if (withStage && entry.Source is PlaceSource place && place.Contents != null && place.Contents.LeftToChance
                        && Shown("Roll again", Skin.Primary, Stage.Subject != null, ActionWords.RollPlace))
                    {
                        Previews.Rebuild();
                    }
                    // A dungeon's or camp's example, laid out anew, with what it holds or how far reading it has got.
                    if (withStage && entry.Source is PlaceSource laid && !laid.IsRoom && laid.Contents?.Dungeon != null)
                    {
                        var example = ExampleOf(entry);
                        if (Shown("Another layout", Skin.Button, example != null, ActionWords.AnotherLayout)) ExampleLayouts.Another();
                        note = example != null
                            ? DungeonWords.ExampleNote(DungeonWords.Example(example, ExampleLayouts.Failed), example.Rooms.Count > 0)
                            : ExampleLayouts.Of(entry) ? DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total) : null;
                    }
                    break;

                default:
                    if (entry.Kind == Kind.Projectile && Button("Fire where you look", Skin.Primary, ActionWords.Fire)) Previews.Fire(entry);
                    // Wearing it and keeping it on are one idea, side by side under the stage in
                    // both views; the stage's own chips only change how it is seen.
                    if (entry.Kind == Kind.Item && entry.Source is GameObject wearable && PrefabGear.IsWearable(wearable)
                        && Button("Wear it", Looks.OnPerson ? Skin.On : Skin.Button, ActionWords.Wear(Looks.OnPerson)))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    if (Looks.IsWorn(entry))
                    {
                        var kept = Looks.Outfit.Contains(entry.Name);
                        if (Button("Keep it on", kept ? Skin.On : Skin.Button, ActionWords.Keep(kept)))
                        {
                            if (kept) Looks.Outfit.TakeOff(entry.Name);
                            else Looks.Outfit.Keep(entry.Name, PrefabGear.SlotOf((GameObject)entry.Source));
                            Previews.Rebuild();
                        }
                    }
                    var fallen = Previews.Playing.IsPlaying(PlayKey.Ragdoll);
                    if (Previews.RagdollOf(entry) != null && Shown("Ragdoll", fallen ? Skin.On : Skin.Button, Stage.Subject != null, ActionWords.Ragdoll(fallen)))
                    {
                        if (fallen) Previews.Stop(PlayKey.Ragdoll);
                        else Previews.Ragdoll();
                    }
                    var loose = Previews.Playing.IsPlaying(PlayKey.LetFall);
                    if (Previews.CanLetFall(entry) && Shown("Let it fall", loose ? Skin.On : Skin.Button, Stage.Subject != null, ActionWords.LetFall(loose)))
                    {
                        if (loose) Previews.Stop(PlayKey.LetFall);
                        else Previews.LetFall();
                    }
                    if (Previews.IsModel(entry))
                    {
                        if (Button("Show in the world", Previews.InWorld ? Skin.On : Skin.Button, ActionWords.InWorld(Previews.InWorld))) Previews.ToggleWorld();
                        if (Previews.InWorld)
                        {
                            if (Button("Move to where you look", Skin.Button, ActionWords.MoveHere)) Previews.PlaceHere();
                            if (Button("Pin", Skin.Button, ActionWords.Pin))
                            {
                                Previews.Pin();
                                Say("Pinned. It stays where it is until you press Clear.");
                            }
                        }
                    }
                    break;
            }


            y = flow.Below;

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
        /// play any other list it has once. Returns the note to show under them.
        /// </summary>
        private static string StatusActions(Entry entry, Func<string, GUIStyle, string, bool> button)
        {
            var effect = entry.Source as StatusEffect;
            var lists = StatusLists(entry);
            if (effect == null || lists.Count == 0) return "It has no visuals or sounds of its own.";

            var hasStart = lists.Any(l => l.Value == effect.m_startEffects);
            if (hasStart)
            {
                // One button that lights while it is on you, as every switch in the panel does.
                if (button("Show it on you", Previews.StatusShowing ? Skin.On : Skin.Primary, ActionWords.ShowStatus(Previews.StatusShowing)))
                {
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                }
            }

            foreach (var list in lists)
            {
                if (list.Value == effect.m_startEffects) continue;
                var playing = Previews.Playing.IsPlaying(list.Value);
                var style = playing ? Skin.On : hasStart ? Skin.Button : Skin.Primary;
                if (button(PanelWords.PlayList(list.Key), style, ActionWords.PlayStatusList(playing)))
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
