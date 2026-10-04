using System;
using UnityEngine;

namespace Scry
{
    /// <summary>The panel's keys: moving through the list, the search box's own, and closing.</summary>
    internal static partial class ScryPanel
    {
        // ----- Keyboard -----

        private static void Keys(Explorer explorer)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // A Tab also comes as a typed character; in a filter box neither means anything.
            if (e.character == '	' && Typing && !SearchFocused)
            {
                e.Use();
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    // Out of a text box first, handing the keys back to the list and to walking;
                    // from anywhere else the panel closes.
                    if (Typing)
                    {
                        GUIUtility.keyboardControl = 0;
                        Assist.ResetCycle();
                    }
                    else AskClose();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    Step(explorer, -1);
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    Step(explorer, 1);
                    e.Use();
                    break;
                case KeyCode.PageUp:
                    // Over a place's stage they step its floors; elsewhere they page the list.
                    if (Stage.HasFloors && MouseOverStage(e.mousePosition)) Stage.StepCut(false);
                    else Step(explorer, -Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.PageDown:
                    if (Stage.HasFloors && MouseOverStage(e.mousePosition)) Stage.StepCut(true);
                    else Step(explorer, Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // In a filter box Enter plays what the filter shows first; in the search, while
                    // it offers a suggestion for the word being typed, it takes it; else it plays
                    // or shows the selection.
                    if (FocusedFilter == EffectControl) PlayFirstEffect();
                    else if (FocusedFilter == ClipControl) PlayFirstClip();
                    else if (!Assist.TakeEnter(explorer)) Primary(explorer.Selected);
                    e.Use();
                    break;
                case KeyCode.F:
                    if (e.control)
                    {
                        FocusSearch(true);
                        e.Use();
                    }
                    break;
                case KeyCode.Tab:
                    // Tab completes the search; in a filter box it means nothing, rather than Unity
                    // moving the keyboard on to whichever text box comes next.
                    if (Typing && !SearchFocused) e.Use();
                    break;
            }
        }

        private static void Step(Explorer explorer, int rows)
        {
            explorer.Move(rows);

            // Past a folded group rather than into it, one entry at a time the same way.
            var step = rows < 0 ? -1 : 1;
            for (var guard = 0; guard < explorer.Results.Count && InFoldedGroup(explorer); guard++)
            {
                var before = explorer.SelectedIndex;
                explorer.Move(step);
                if (explorer.SelectedIndex == before) break;
            }
            RevealSelected();
        }

        private static bool InFoldedGroup(Explorer explorer)
        {
            var entry = explorer.Selected;
            return entry != null && Grouped(explorer) && FoldedGroups.Contains(FoldKey(explorer, entry.Group));
        }

        /// <summary>
        /// What Enter and a double click do: the thing most wanted of each kind. A sound or an
        /// effect plays, a projectile is fired, a status effect is shown; a creature makes its
        /// first attack; an item is worn or taken off, if it can be; a location or room plays its
        /// music, or stops it; a raid rolls its creatures anew, a boss's fight plays its music; a tree, log, rock or piece
        /// does what the game does when it is felled or broken, or a log or rock falls where it
        /// has nothing else. Anything else is left alone; showing it in the world is a button.
        /// </summary>
        private static void Primary(Entry entry)
        {
            if (entry == null) return;

            switch (entry.Kind)
            {
                case Kind.Sound:
                    Previews.PlaySound(entry);
                    break;
                case Kind.Effect:
                    Previews.PlayEffect(entry, onYou: false);
                    break;
                case Kind.Projectile:
                    Previews.Fire(entry);
                    break;
                case Kind.StatusEffect:
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                    break;
                case Kind.Creature:
                    FirstAttack();
                    break;
                case Kind.Location:
                case Kind.Biome:
                    var said = Previews.PlacesMusic(entry);
                    if (said != null) Say(said);
                    break;
                case Kind.Raid:
                    // A raid rolls its creatures anew; a boss's fight, which brings none, plays its music.
                    if (Stage.IsStaged(entry)) Previews.Rebuild();
                    else
                    {
                        var music = Previews.PlacesMusic(entry);
                        if (music != null) Say(music);
                    }
                    break;
                case Kind.Item:
                    if (entry.Source is GameObject item && PrefabGear.IsWearable(item))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    break;
                default:
                    OwnAction(entry);
                    break;
            }
        }

        /// <summary>
        /// Plays a creature's first attack, as its chip under Animations does, once its clips are
        /// known to be attacks; until then it says so rather than playing something else.
        /// </summary>
        private static void FirstAttack()
        {
            var clips = Previews.Clips();
            var names = Previews.ClipNames();
            var tags = Previews.ClipTags();
            for (var i = 0; i < clips.Count; i++)
            {
                var name = i < names.Count ? names[i] : clips[i].name;
                if (!tags.TryGetValue(name, out var tag) || !ClipWords.IsAttack(tag)) continue;
                Previews.PlayClip(clips[i]);
                Previews.LastClip = clips[i];
                return;
            }
            if (clips.Count > 0 && tags.Count == 0) Say("Its animations are still being worked out; Enter plays its first attack once they are.");
        }

        /// <summary>
        /// What a tree, log, rock or piece does when the game fells or breaks it, as the chip of
        /// that effect list does it; a log or rock with nothing of the kind falls instead.
        /// </summary>
        private static void OwnAction(Entry entry)
        {
            if (!(entry.Source is GameObject prefab) || !Previews.IsModel(entry)) return;
            foreach (var pair in Previews.PrefabLists(prefab))
            {
                if (pair.Value == null || !Falling.IsDestroyedList(prefab, pair.Value)) continue;
                Previews.PlayEffectList(pair.Key, pair.Value);
                return;
            }
            if (Previews.CanLetFall(entry)) Previews.LetFall();
        }
    }
}
