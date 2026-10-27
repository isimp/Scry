using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for which clips a page lists: a death that hurts is an attack too, an
    /// attack the game plays no animation for says so, what nothing a creature carries starts is
    /// left out, and a person lists what it moves and attacks in with what it holds.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// The Writhan: its death's blast is a line of its attacks table, played by the clip it dies
        /// in, which is listed as an attack; its explosion attack plays no animation and says why;
        /// the explosion clip nothing starts is left out. The Seeker Queen's slap starts by a
        /// trigger its animator lacks.
        /// </summary>
        private static IEnumerator DeathAttacksAndUnplayedClips(Probe p)
        {
            var writhan = Pick(Kind.Creature, "Writhan");
            if (writhan == null) p.Skip("there is no Writhan");
            var table = Facts.For(writhan).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
            var death = table?.Lines.FirstOrDefault(l => EntryKeys.PlaysAttack(l.Link, out var item, out _) && EntryKeys.IsDeathAttack(item));
            p.Check(death != null && death.Value.Cells[1].Contains("poison") && death.Value.Cells[2].Contains("dies"), "the Writhan's death is a line of its attacks, with what it deals",
                table != null ? string.Join(" | ", table.Lines.Select(l => string.Join(" ", l.Cells))) : "no table");

            Select(writhan);
            yield return Until(() => CopyOf(writhan) != null, 10);
            yield return Until(() => !Previews.ClipsSorting, 40);
            var dies = Previews.ClipOfAttackItem(EntryKeys.DeathAttack);
            p.Check(dies != null && dies.name == "Death Explosion", "its death's line plays the clip it dies in", dies != null ? dies.name : "no clip");
            var tag = dies != null && Previews.ClipTags().TryGetValue(dies.name, out var said) ? said : null;
            p.Check(ClipWords.IsAttack(tag), "that clip is listed as an attack", tag ?? "no tag");

            var trigger = Previews.AttackTriggerOf("writhan_explode_aoe", false, out var inAnimator);
            p.Check(Previews.ClipOfAttackItem("writhan_explode_aoe") == null && trigger == "attack_explode_test" && inAnimator,
                "its explosion attack plays no animation, its trigger leading nowhere", $"{trigger} in the animator: {inAnimator}");
            var unplayed = Previews.ClipsUnplayed();
            p.Check(unplayed != null && unplayed.Contains("Attack Explosion") && !unplayed.Contains("Attack Bite") && !unplayed.Contains("Death Explosion"),
                "the explosion clip nothing starts is left out, its bite and its death are not", unplayed != null ? string.Join(", ", unplayed) : "not worked out");
            var rows = ScryPanel.ClipRowsForTest("");
            p.Check(rows.All(r => !r.Text.StartsWith("Attack Explosion", System.StringComparison.Ordinal)), "the list leaves it out");
            rows = ScryPanel.ClipRowsForTest("explosion");
            p.Check(rows.Any(r => r.Text == ClipWords.Unplayed("Attack Explosion")), "a filter finds it, saying it is not played", string.Join(" | ", rows.Select(r => r.Text)));
            var groups = Previews.ClipGroupsNow(out _);
            p.Check(groups != null && groups.TryGetValue("Walk", out var walk) && walk == ClipGroup.Moving && groups.TryGetValue("Death Explosion", out var blast) && blast == ClipGroup.Attacks,
                "its clips are grouped: walking is moving, its blast an attack", groups != null ? string.Join(", ", groups.Select(g => g.Key + " " + g.Value)) : "not worked out");
            yield return PictureOf(p, "animations", "the Writhan's clips");

            var queen = Pick(Kind.Creature, "SeekerQueen");
            if (queen != null)
            {
                Select(queen);
                yield return Until(() => CopyOf(queen) != null, 10);
                yield return Until(() => Previews.StageClipsKnown(), 40);
                var slap = Previews.AttackTriggerOf("SeekerQueen_Slap", false, out var has);
                // The game picks one of its slap's numbered triggers at random (Attack.Start): the one shown is one the animator has.
                p.Check(slap != null && slap.StartsWith("attack_slash", System.StringComparison.Ordinal) && has, "the Seeker Queen's slap plays by a trigger of the four its animator has", $"{slap} in the animator: {has}");
            }

            // Kin share one animator: a Charred warrior never draws a bow.
            var charred = Pick(Kind.Creature, "Charred_Melee");
            if (charred != null)
            {
                Select(charred);
                yield return Until(() => CopyOf(charred) != null, 10);
                yield return Until(() => !Previews.ClipsSorting && !Previews.ClipsProvisional, 60);
                var left = Previews.ClipsUnplayed();
                p.Check(left != null && left.Contains("Draw Bow"), "a Charred warrior's list leaves out the bow its kin draw", left != null ? string.Join(", ", left) : "not worked out");
            }
        }

        /// <summary>
        /// The person holding a sword, and as its look shows it: each lists the clips it moves and attacks in with
        /// what it holds, and leaves out those of other gear, which a filter finds saying what they
        /// are held with.
        /// </summary>
        private static IEnumerator PersonClipsByWhatIsHeld(Probe p)
        {
            var sword = Pick(Kind.Item, "SwordIron", "SwordBronze");
            if (sword == null) p.Skip("there is no sword");

            Looks.OnPerson = true;
            var asked = UnityEngine.Time.unscaledTime;
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            yield return Until(() => !Previews.ClipsSorting, 120);
            var listed = UnityEngine.Time.unscaledTime - asked;
            // Its own stance is answered first; the rest of the sweep makes it whole.
            yield return Until(() => !Previews.ClipsSorting && !Previews.ClipsProvisional, 180);
            p.Note($"holding {sword.Name}, its clips listed after {Numbers.Amount(listed, 1)} s, whole after {Numbers.Amount(UnityEngine.Time.unscaledTime - asked, 1)} s");
            var other = Previews.ClipsOtherGear();
            var all = Previews.Clips().Count;
            var shown = ScryPanel.ClipRowsForTest("");
            p.Note($"holding {sword.Name}: {Numbers.Count(shown.Count)} of {Numbers.Count(all)} clips listed");
            p.Check(other != null && !other.ContainsKey("Run New Sword Right") && other.ContainsKey("Run New Pickaxe") && other.ContainsKey("Greatsword Idle"),
                "holding a sword it runs as with a sword, not as with a pickaxe or a greatsword", other != null ? $"{Numbers.Count(other.Count)} with other gear" : "not worked out");
            p.Check(shown.Count < all / 2, "and lists less than half of its clips", $"{Numbers.Count(shown.Count)} of {Numbers.Count(all)}");
            var found = ScryPanel.ClipRowsForTest("pickaxe");
            p.Check(found.Any(r => r.Text == ClipWords.OtherGear("Run New Pickaxe", (int)ItemDrop.ItemData.AnimationState.TwoHandedClub)), "a filter finds a pickaxe's run, saying what it is held with",
                string.Join(" | ", found.Select(r => r.Text)));
            var groups = Previews.ClipGroupsNow(out var stance);
            ClipGroup Of(string clip) => groups != null && groups.TryGetValue(clip, out var g) ? g : (ClipGroup)(-1);
            p.Check(stance == (int)ItemDrop.ItemData.AnimationState.OneHanded && Of("Run New Sword Right") == ClipGroup.Moving && Of("Block idle") == ClipGroup.Blocking
                && Of("player_stagger") == ClipGroup.Hurt && Of("Workbench") == ClipGroup.Working && Of("Player_Sit_Chair") == ClipGroup.Resting,
                "its clips are grouped by what they are for: moving with a sword, blocking, hurt, working, resting", groups != null ? string.Join(", ", groups.GroupBy(g => g.Value).Select(g => $"{g.Key} {Numbers.Count(g.Count())}")) : "not worked out");
            p.Check(groups != null && groups.Values.Contains(ClipGroup.Emotes) && groups.Values.Contains(ClipGroup.Airborne), "emotes and jumping have groups of their own");
            // The sweep steps less than it did: what it finds of moving and blocking with a sword stays as many.
            int Own(ClipGroup group) => groups != null ? groups.Count(g => g.Value == group && !(other?.ContainsKey(g.Key) ?? false)) : 0;
            p.Check(Own(ClipGroup.Moving) >= 13 && Own(ClipGroup.Blocking) >= 15, "with a sword it moves in 13 clips or more and blocks in 15 or more, as the sweep found before",
                $"moving {Numbers.Count(Own(ClipGroup.Moving))}, blocking {Numbers.Count(Own(ClipGroup.Blocking))}");
            // Kept for the next session, and taken up again with nothing watched anew.
            p.Check(ClipStore.Holds("Player|stances"), "what the person plays by stance is kept for the next session", $"{Numbers.Count(ClipStore.Count)} looks kept");
            var probes = TriggerProbe.DoneForTest.Count;
            var again = UnityEngine.Time.unscaledTime;
            Previews.ForgetClipsInMemory();
            yield return null;
            yield return Until(() => !Previews.ClipsSorting && !Previews.ClipsProvisional, 30);
            p.Check(!Previews.ClipsSorting && TriggerProbe.DoneForTest.Count == probes && Previews.ClipsOtherGear()?.ContainsKey("Run New Pickaxe") == true,
                "opened again, the sword's clips are taken from what was kept, no animator watched anew",
                $"after {Numbers.Amount(UnityEngine.Time.unscaledTime - again, 1)} s, {Numbers.Count(TriggerProbe.DoneForTest.Count - probes)} watched anew");
            p.Note("left in Other: " + (groups != null ? string.Join(", ", groups.Where(g => g.Value == ClipGroup.Other && !(other?.ContainsKey(g.Key) ?? false)).Select(g => g.Key)) : "none"));
            yield return PictureOf(p, "animations", "the person's clips holding a sword");

            var person = Pick(Kind.Creature, "Player");
            if (person != null)
            {
                Looks.OnPerson = false;
                Select(person);
                yield return Until(() => CopyOf(person) != null, 10);
                yield return Until(() => !Previews.ClipsSorting, 120);
                var bare = Previews.ClipsOtherGear();
                // The person as shown holds what its look gives it (a torch): it moves as with that, the sword's run another's.
                var shownGroups = Previews.ClipGroupsNow(out var shownStance);
                var moving = shownGroups != null ? shownGroups.Where(g => g.Value == ClipGroup.Moving).Select(g => g.Key).ToList() : new System.Collections.Generic.List<string>();
                // Moving holds every stance's moving clips; those of what it holds are its own, the rest another gear's.
                var ownMoving = bare != null ? moving.Where(c => !bare.ContainsKey(c)).ToList() : moving;
                p.Check(bare != null && ownMoving.Count > 0 && bare.ContainsKey("Run New Sword Right"), "the person as shown moves as with what it holds, a sword's run another's",
                    bare != null ? $"stance {Numbers.Count(shownStance)}, {Numbers.Count(ownMoving.Count)} of {Numbers.Count(moving.Count)} moving clips its own: {string.Join(", ", ownMoving.Take(6))}; the sword's run {(bare.ContainsKey("Run New Sword Right") ? "another's" : "its own")}" : "not worked out");
                p.Note("what finding clips took: " + string.Join("; ", TriggerProbe.DoneForTest.Select(d => $"{d.Key} {Numbers.Count(d.Ms)} ms over {Numbers.Count(d.Frames)} frames")));
                yield return PictureOf(p, "animations", "the person's clips as its look shows it");
            }
        }
    }
}
