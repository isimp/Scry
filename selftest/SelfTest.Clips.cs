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
            yield return PictureOf(p, "animations", "the Writhan's clips");

            var queen = Pick(Kind.Creature, "SeekerQueen");
            if (queen != null)
            {
                Select(queen);
                yield return Until(() => CopyOf(queen) != null, 10);
                yield return Until(() => Previews.StageClipsKnown(), 40);
                var slap = Previews.AttackTriggerOf("SeekerQueen_Slap", false, out var has);
                p.Check(slap == "attack_slash0" && !has, "the Seeker Queen's slap starts by a trigger its animator lacks", $"{slap} in the animator: {has}");
            }

            // Kin share one animator: a Charred warrior never draws a bow.
            var charred = Pick(Kind.Creature, "Charred_Melee");
            if (charred != null)
            {
                Select(charred);
                yield return Until(() => CopyOf(charred) != null, 10);
                yield return Until(() => !Previews.ClipsSorting, 60);
                var left = Previews.ClipsUnplayed();
                p.Check(left != null && left.Contains("Draw Bow"), "a Charred warrior's list leaves out the bow its kin draw", left != null ? string.Join(", ", left) : "not worked out");
            }
        }

        /// <summary>
        /// The person, bare and holding a sword: each lists the clips it moves and attacks in with
        /// what it holds, and leaves out those of other gear, which a filter finds saying what they
        /// are held with.
        /// </summary>
        private static IEnumerator PersonClipsByWhatIsHeld(Probe p)
        {
            var sword = Pick(Kind.Item, "SwordIron", "SwordBronze");
            if (sword == null) p.Skip("there is no sword");

            Looks.OnPerson = true;
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            yield return Until(() => !Previews.ClipsSorting, 120);
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
            yield return PictureOf(p, "animations", "the person's clips holding a sword");

            var person = Pick(Kind.Creature, "Player");
            if (person != null)
            {
                Looks.OnPerson = false;
                Select(person);
                yield return Until(() => CopyOf(person) != null, 10);
                yield return Until(() => !Previews.ClipsSorting, 120);
                var bare = Previews.ClipsOtherGear();
                p.Check(bare != null && !bare.ContainsKey("Run New") && bare.ContainsKey("Run New Sword Right"), "bare-handed it runs as with bare hands", bare != null ? $"{Numbers.Count(bare.Count)} with other gear" : "not worked out");
                yield return PictureOf(p, "animations", "the bare person's clips");
            }
        }
    }
}
