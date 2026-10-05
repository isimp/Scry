using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for previews played: a creature's attacks, clips and sounds with
    /// their controls, effects looping, on you and with a status, wearing an item, felling a
    /// tree, a model in the world, a projectile in flight, and clearing the world.
    /// </summary>
    internal static partial class SelfTest
    {
        // Each waits a frame after selecting, for the selection to be shown: showing it stops what
        // plays, and plays a sound by itself only when PlayOnSelect is on.

        private static IEnumerator CreatureAttacks(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null, 10);
            p.Check(CopyOf(troll) != null, "the creature stands on the stage");

            // Which clips are attacks is worked out by watching them, a little each frame.
            yield return Until(() => Previews.ClipTags().Values.Any(t => t.StartsWith("attack", StringComparison.Ordinal)), 20);
            var tags = Previews.ClipTags();
            var clips = Previews.Clips();
            var names = Previews.ClipNames();
            p.Note($"{Numbers.Count(clips.Count)} clips, {Numbers.Count(tags.Count(t => t.Value.StartsWith("attack", StringComparison.Ordinal)))} of them attacks");
            AnimationClip attack = null;
            for (var i = 0; i < clips.Count && attack == null; i++)
            {
                var name = i < names.Count ? names[i] : clips[i].name;
                if (tags.TryGetValue(name, out var tag) && tag.StartsWith("attack", StringComparison.Ordinal)) attack = clips[i];
            }
            if (!p.Check(attack != null, "its attacks are known")) yield break;

            Previews.PlayClip(attack);
            yield return Until(() => Previews.PlayingClip() != null, 3);
            p.Check(Previews.PlayingClip() == attack, "the attack plays", $"playing {Previews.PlayingClip().OrNull()?.name ?? "nothing"}");
            yield return new Wait(1.5);
            Previews.StopClip();
        }

        /// <summary>A creature's clip plays, pauses, seeks, loops and stops.</summary>
        private static IEnumerator ClipControls(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null && Previews.Clips().Count > 0, 10);
            var clips = Previews.Clips();
            if (!p.Check(clips.Count > 0, "its clips are known", $"{Numbers.Count(clips.Count)}")) yield break;
            var clip = clips.OrderByDescending(c => c.length).First();
            Previews.PlayClip(clip);
            yield return new Wait(0.2);
            p.Check(Previews.PlayingClip() == clip, $"{clip.name} plays");
            Previews.PauseClip(true);
            yield return null;
            p.Check(Previews.ClipPaused, "it pauses");
            Previews.SeekClip(clip.length / 2f);
            yield return null;
            p.Check(Previews.ClipPosition(out var time, out var length) && Mathf.Abs(time - length / 2f) < length * 0.15f + 0.05f, "it seeks to its middle", $"{Numbers.Fixed(time, 2)} of {Numbers.Fixed(length, 2)} s");
            Previews.PauseClip(false);
            var loops = Previews.Repeat;
            Previews.Repeat = !loops;
            p.Check(Previews.Repeat != loops, "repeating can be switched");
            Previews.Repeat = loops;
            Previews.StopClip();
            yield return null;
            p.Check(Previews.PlayingClip() == null, "and it stops");
        }

        private static IEnumerator SoundPlays(Probe p)
        {
            var sound = Pick(Kind.Sound, "sfx_troll_idle", "sfx_greydwarf_idle");
            if (sound == null) p.Skip("there is no sound");
            p.Note(sound.Name);
            Select(sound);
            yield return null;
            Previews.PlaySound(sound);
            yield return Until(() => Previews.SoundPlaying, 2);
            p.Check(Previews.SoundPlaying, "it plays");
            Previews.StopSound();
            p.Check(!Previews.SoundPlaying, "it stops");
        }

        /// <summary>A sound with several variants plays the one chosen.</summary>
        private static IEnumerator SoundVariant(Probe p)
        {
            Entry sound = null;
            List<AudioClip> variants = null;
            foreach (var entry in X.Catalog.Where(e => e.Kind == Kind.Sound && e.Origin == Origin.Vanilla && e.Name.StartsWith("sfx_", StringComparison.Ordinal)).Take(200))
            {
                var clips = entry.Source is GameObject prefab ? Previews.SoundVariants(prefab) : null;
                if (clips == null || clips.Count < 2) continue;
                sound = entry;
                variants = clips;
                break;
            }
            if (sound == null) p.Skip("no sound of the game's has two variants");
            p.Note($"{sound.Name}: {Numbers.Count(variants.Count)} variants");
            Select(sound);
            yield return null;
            var chosen = variants[variants.Count - 1];
            Previews.PlaySound(sound, chosen);
            yield return Until(() => Previews.SoundPlaying, 2);
            p.Check(Previews.SoundPlaying, "it plays");
            yield return new Wait(0.3);
            p.Check(Previews.SoundClipNow() == chosen, "the variant chosen is the one heard", Previews.SoundClipNow().OrNull()?.name ?? "none");
            Previews.StopSound();
            p.Check(!Previews.SoundPlaying, "and it stops");
        }

        /// <summary>A sound pauses, seeks and goes on.</summary>
        private static IEnumerator SoundControls(Probe p)
        {
            Entry sound = null;
            foreach (var entry in X.Catalog.Where(e => e.Kind == Kind.Sound && e.Origin == Origin.Vanilla && e.Source is GameObject).Take(400))
            {
                var clips = Previews.SoundVariants((GameObject)entry.Source);
                if (clips.Count > 0 && clips.All(c => c != null && c.length > 2f))
                {
                    sound = entry;
                    break;
                }
            }
            if (sound == null) p.Skip("no sound of the game's is longer than two seconds");
            p.Note(sound.Name);
            Select(sound);
            yield return null;
            Previews.PlaySound(sound);
            yield return Until(() => Previews.SoundPlaying && Previews.SoundPosition(out _, out _), 2);
            Previews.PauseSound(true);
            yield return null;
            p.Check(Previews.SoundPaused, "it pauses");
            Previews.SeekSound(1f);
            yield return null;
            p.Check(Previews.SoundPosition(out var time, out _) && Mathf.Abs(time - 1f) < 0.2f, "it seeks", $"{Numbers.Fixed(time, 2)} s");
            p.Note("plays through " + Previews.SoundSourceTold());
            Previews.PauseSound(false);
            yield return new Wait(0.3);
            p.Check(Previews.SoundPosition(out var later, out _) && later > time, "and goes on from there", $"{Numbers.Fixed(later, 2)} s");
            yield return new Wait(1.0);
            Previews.SoundPosition(out var after, out _);
            p.Note($"a second on it is at {Numbers.Fixed(after, 2)} s; frame by frame after going on: {string.Join("; ", Previews.SeekTrail)}");
            Previews.StopSound();
        }

        /// <summary>An effect on you plays on while looping is on, and stops when asked.</summary>
        private static IEnumerator EffectLoops(Probe p)
        {
            var effect = Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench");
            if (effect == null) p.Skip("there is no effect");
            // On the stage: the full view, not the world.
            if (ScryPanel.Compact) ScryPanel.Compact = false;
            if (Previews.InWorld) Previews.ToggleWorld();
            var shows = Stage.Shows;
            var made = Stage.CopiesMade;
            Select(effect);
            yield return null;
            var loops = Previews.Repeat;
            Previews.Repeat = true;
            yield return Until(() => CopyOf(effect) != null, 5);
            var first = CopyOf(effect);
            yield return Until(() => CopyOf(effect) != null && CopyOf(effect) != first, 8);
            var again = first != null && CopyOf(effect) != null && CopyOf(effect) != first;
            // What still plays, when it has not played out: the stage waits for every particle and sound.
            var still = again || first == null ? "" : string.Join(", ",
                first.GetComponentsInChildren<ParticleSystem>(false).Where(s => s.IsAlive(false)).Select(s => $"{s.name} (loops {s.main.loop}, lasts {Numbers.Amount(s.main.duration, 1)} s)")
                    .Concat(first.GetComponentsInChildren<AudioSource>(false).Where(s => s.isPlaying).Select(s => $"{s.name} sound (loops {s.loop})")));
            // Any second copy the stage made is the effect played again, however short it lives.
            again |= Stage.CopiesMade - made >= 2;
            var twins = X.Catalog.Count(e => e.Key == effect.Key);
            var why = first == null
                ? $"{Numbers.Count(twins)} entries of its key; the stage showed {(Stage.Showing != null ? Stage.Showing.Name : "nothing")}{(Stage.IsStaged(effect) ? "" : ", having nothing of it to show")}, asked {Numbers.Count(Stage.Shows - shows)} times, a copy made {Numbers.Count(Stage.CopiesMade - made)} times, none seen; on you {Looks.OnPerson}, in the world {Previews.InWorld}, compact {ScryPanel.Compact}, panel {(Session.IsOpen ? "open" : "closed")}"
                : still.Length > 0 ? "still playing: " + still : null;
            p.Check(again, "with Repeat on, the stage plays it again once it has played out", why);
            Previews.Repeat = false;
            var last = CopyOf(effect);
            yield return Until(() => Stage.Finished, 8);
            yield return new Wait(1.0);
            p.Check(CopyOf(effect) == last, "with Repeat off, it is not played again");
            var key = PlayKey.OnYou(effect.Name);
            Previews.PlayEffect(effect, onYou: true);
            yield return null;
            p.Check(Previews.Playing.IsPlaying(key), "played on you, it plays");
            Previews.Stop(key);
            yield return null;
            p.Check(!Previews.Playing.IsPlaying(key), "and stops when asked");
            Previews.Repeat = loops;
        }

        private static IEnumerator EffectOnYou(Probe p)
        {
            var effect = Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench");
            if (effect == null) p.Skip("there is no effect");
            p.Note(effect.Name);
            Select(effect);
            yield return null;
            var key = PlayKey.OnYou(effect.Name);
            Previews.PlayEffect(effect, onYou: true);
            yield return Until(() => Previews.Playing.IsPlaying(key), 2);
            p.Check(Previews.Playing.IsPlaying(key), "it plays on you");
            Previews.Stop(key);
            p.Check(!Previews.Playing.IsPlaying(key), "it stops");
        }

        private static IEnumerator StatusOnYou(Probe p)
        {
            var status = StatusOnPerson();
            if (status == null) p.Skip("there is no status effect with a look of its own");
            p.Note(status.Name);
            Select(status);
            yield return null;
            Previews.ShowStatus(status);
            yield return Until(() => Previews.StatusShowing, 3);
            p.Check(Previews.StatusShowing, "its look is on you");
            Previews.StopStatus(false);
            p.Check(!Previews.StatusShowing, "and comes off");
        }

        /// <summary>An item put on shows on the person, and on its own again once taken off.</summary>
        private static IEnumerator WearIt(Probe p)
        {
            var armour = X.Catalog.FirstOrDefault(e => e.Name == "ArmorBronzeChest" && e.Source is GameObject g && PrefabGear.IsWearable(g))
                         ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Origin == Origin.Vanilla && e.Source is GameObject g && PrefabGear.IsWearable(g));
            if (armour == null) p.Skip("there is nothing wearable");
            var was = Looks.OnPerson;
            Looks.OnPerson = true;
            Select(armour);
            yield return Until(() => CopyOf(armour) != null, 10);
            var worn = CopyOf(armour);
            var wornParts = Renderers(worn);
            p.Check(worn != null && worn.GetComponentInChildren<Animator>(true) != null, $"{armour.Name} is shown worn by a person", $"{Numbers.Count(wornParts)} parts");
            Looks.OnPerson = false;
            Previews.Rebuild();
            yield return Until(() => CopyOf(armour) != null && CopyOf(armour) != worn, 10);
            var alone = CopyOf(armour);
            p.Check(alone != null && Renderers(alone) < wornParts, "and on its own once taken off", $"{Numbers.Count(Renderers(alone))} parts");
            Looks.OnPerson = was;
        }

        /// <summary>A tree felled on the stage plays what it leaves, which goes again in time.</summary>
        private static IEnumerator TreeFalls(Probe p)
        {
            var tree = Pick(Kind.Resource, "Beech1", "Birch1", "Oak1");
            if (tree == null) p.Skip("there is no tree");
            Select(tree);
            yield return Until(() => CopyOf(tree) != null, 10);
            var list = Previews.PrefabLists((GameObject)tree.Source).FirstOrDefault(pair => pair.Value != null && Falling.IsDestroyedList((GameObject)tree.Source, pair.Value));
            if (!p.Check(list.Value != null, "it has what it does when felled")) yield break;
            var thuds = Thud.Played;
            var felled = Time.unscaledTime;
            Previews.PlayEffectList(list.Key, list.Value);
            yield return Until(() => Stage.PlayedCount > 0, 2);
            p.Check(Stage.PlayedCount > 0, $"felling it plays {list.Key}", $"{Numbers.Count(Stage.PlayedCount)} things");

            // Its log strikes the ground as the game's does, with its impact's sound (ImpactEffect).
            var log = ((GameObject)tree.Source).GetComponent<TreeBase>().OrNull()?.m_logPrefab;
            var impact = log != null ? log.GetComponentInChildren<ImpactEffect>(true) : null;
            if (impact == null) p.Note("its log has no impact of its own");
            else
            {
                p.Note($"its log strikes with {string.Join(", ", EffectSlots.NamesPlayed(impact.m_hitEffect))}, from {Numbers.Amount(impact.m_minVelocity, 1)} m/s");
                // The thump is the log toppling off its stump onto the ground, not its first touch.
                bool Toppled() => Thud.Contacts.Any(c => c.Struck && c.At - felled >= 0.5f);
                yield return Until(Toppled, 8);
                // A strike noted in a contact plays in the copy's next update, which may come after this one in the frame.
                yield return Until(() => Thud.Played > thuds, 1);
                var contacts = string.Join(", ", Thud.Contacts.Where(c => c.At >= felled).Select(c => $"{Numbers.Amount(c.Speed, 1)} m/s after {Numbers.Fixed(c.At - felled, 1)} s{(c.Struck ? ", heard" : "")}"));
                p.Note("its log touched: " + (contacts.Length > 0 ? contacts : "nothing"));
                p.Check(Thud.Played > thuds && Toppled(), "its log is heard striking the ground as it topples off its stump", $"{Numbers.Count(Thud.Played - thuds)} strikes");
            }
            yield return Until(() => Stage.PlayedCount == 0, 20);
            p.Check(Stage.PlayedCount == 0, "and what it left goes again");
        }

        private static IEnumerator ModelInWorld(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something is already playing or standing in the world, and is left alone");
            var sword = Pick(Kind.Item, "SwordIron", "AxeBronze");
            if (sword == null) p.Skip("there is no sword or axe");
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            Previews.ToggleWorld();
            yield return Until(() => Previews.AnythingInWorld, 3);
            p.Check(Previews.InWorld && Previews.AnythingInWorld, "a copy stands where you look");
            Previews.ToggleWorld();
            yield return null;
            p.Check(!Previews.InWorld && !Previews.AnythingInWorld, "and is gone again");
        }

        private static IEnumerator ProjectileFlies(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something is already playing or standing in the world, and is left alone");
            var arrow = Pick(Kind.Projectile, "bow_projectile", "bow_projectile_fire");
            if (arrow == null) p.Skip("there is no projectile");
            Select(arrow);
            yield return Until(() => CopyOf(arrow) != null, 10);
            Previews.Fire(arrow);
            p.Check(Previews.AnythingInWorld, "it flies");
            yield return Until(() => !Previews.AnythingInWorld, 14);
            p.Check(!Previews.AnythingInWorld, "and is gone once it has landed");
        }

        /// <summary>Clear world takes away what stands in the world, pinned or not.</summary>
        private static IEnumerator ClearWorld(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something of yours is already in the world, and is left alone");
            var sword = Pick(Kind.Item, "SwordIron", "AxeBronze");
            if (sword == null) p.Skip("there is no sword or axe");
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            Previews.ToggleWorld();
            yield return Until(() => Previews.AnythingInWorld, 3);
            Previews.Pin();
            yield return null;
            p.Check(Previews.PinnedCount > 0 && Previews.OutLines > 0, "a copy stands pinned in the world", $"{Numbers.Count(Previews.PinnedCount)} pinned, {Numbers.Count(Previews.OutLines)} lines out");
            Previews.ClearWorld();
            yield return null;
            p.Check(!Previews.AnythingInWorld && Previews.PinnedCount == 0 && Previews.OutLines == 0, "Clear world takes all of it away");
        }
    }
}
