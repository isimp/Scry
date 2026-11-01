using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ResistGridTests
    {
        // Resistances show as a grid of every damage type, so creatures, pieces and rocks line up
        // whatever they resist. Each cell is the share of that damage taken, as
        // HitData.ApplyModifier scales it, with the game's word on hover and a tone for its colour.

        [Theory]
        [InlineData(Degree.Normal, "100%", "takes it in full", Tone.Plain)]
        [InlineData(Degree.SlightlyResistant, "75%", "slightly resistant to it: takes three quarters", Tone.Resists)]
        [InlineData(Degree.Resistant, "50%", "resistant to it: takes half", Tone.Resists)]
        [InlineData(Degree.VeryResistant, "25%", "very resistant to it: takes a quarter", Tone.Resists)]
        [InlineData(Degree.SlightlyWeak, "125%", "slightly weak to it: takes a quarter more", Tone.Weak)]
        [InlineData(Degree.Weak, "150%", "weak to it: takes half again", Tone.Weak)]
        [InlineData(Degree.VeryWeak, "200%", "very weak to it: takes double", Tone.Weak)]
        [InlineData(Degree.Immune, "0%", "immune to it: takes none, and fire hits cannot put their effect on it", Tone.Immune)]
        [InlineData(Degree.Ignore, "0%", "unaffected by it: takes none, and its fire damage brings no lasting effect; only an attack that carries the effect itself still puts it on", Tone.Immune)]
        public void EachDegreeIsTheShareTaken(object degree, string value, string word, object tone)
        {
            var cell = ResistWords.Cell("Fire", (Degree)degree);

            Assert.Equal("Fire", cell.Type);
            Assert.Equal(value, cell.Value);
            Assert.Equal("Fire: " + word, cell.Tip);
            Assert.Equal((Tone)tone, cell.Tone);
        }

        [Fact]
        public void ImmuneAndUnaffectedDifferOnlyWhereAHitLeavesAnEffect()
        {
            // Only fire, frost, lightning and poison hits put an effect on, which immunity alone
            // keeps off (Character.RPC_Damage); for other types the two read the same.
            Assert.Equal("Poison: immune to it: takes none, and poison hits cannot put their effect on it", ResistWords.Cell("Poison", Degree.Immune).Tip);
            Assert.Equal("Lightning: unaffected by it: takes none, and its lightning damage brings no lasting effect; only an attack that carries the effect itself still puts it on", ResistWords.Cell("Lightning", Degree.Ignore).Tip);
            Assert.Equal("Blunt: immune to it: takes none", ResistWords.Cell("Blunt", Degree.Immune).Tip);
            Assert.Equal("Blunt: unaffected by it: takes none", ResistWords.Cell("Blunt", Degree.Ignore).Tip);
        }

        [Fact]
        public void ACreaturesGridQuietsToolDamageItTakesNoneOf()
        {
            // Chop and pickaxe are for trees and rocks: a creature immune to them need not stand out
            // for it, but the cells keep their places so the grid's rows stay the same.
            var cells = new[] { ResistWords.Cell("Blunt", Degree.Resistant), ResistWords.Cell("Chop", Degree.Immune), ResistWords.Cell("Pickaxe", Degree.Ignore), ResistWords.Cell("Fire", Degree.Immune) };
            var creature = ResistWords.ForCreature(cells);
            Assert.Equal(new[] { "Blunt", "Chop", "Pickaxe", "Fire" }, creature.Select(c => c.Type));
            Assert.Equal(new[] { Tone.Resists, Tone.Quiet, Tone.Quiet, Tone.Immune }, creature.Select(c => c.Tone));
            Assert.Contains("trees and rocks", creature[1].Tip);
            var takesChop = new[] { ResistWords.Cell("Chop", Degree.Normal), ResistWords.Cell("Pickaxe", Degree.Immune) };
            Assert.Equal(new[] { Tone.Plain, Tone.Quiet }, ResistWords.ForCreature(takesChop).Select(c => c.Tone));
        }

        [Fact]
        public void APiecesGridShowsWhatItTakesNoneOfQuietly()
        {
            // A wall's or a tree's grid waits for the character's own hits, type by type; what it
            // takes none of no hit can show, so it is drawn quietly and counted as nothing to find.
            var cells = new[] { ResistWords.Cell("Blunt", Degree.Normal), ResistWords.Cell("Poison", Degree.Immune), ResistWords.Cell("Spirit", Degree.Ignore), ResistWords.Cell("Fire", Degree.Weak) };
            var piece = ResistWords.ForBroken(cells);
            Assert.Equal(new[] { "Blunt", "Poison", "Spirit", "Fire" }, piece.Select(c => c.Type));
            Assert.Equal(new[] { Tone.Plain, Tone.Quiet, Tone.Quiet, Tone.Weak }, piece.Select(c => c.Tone));
            Assert.Contains("takes none", piece[1].Tip);
        }

        [Fact]
        public void DegreesFollowTheGamesOrder()
        {
            // The game's own enum, HitData.DamageModifier, in its order: the numbers must match.
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new[]
            {
                (int)Degree.Normal, (int)Degree.Resistant, (int)Degree.Weak, (int)Degree.Immune, (int)Degree.Ignore,
                (int)Degree.VeryResistant, (int)Degree.VeryWeak, (int)Degree.SlightlyResistant, (int)Degree.SlightlyWeak,
            });
        }

        [Fact]
        public void ADegreeNotKnownIsToldAsSuch()
        {
            var cell = ResistWords.Cell("Fire", (Degree)42);

            Assert.Equal("?", cell.Value);
            Assert.Equal(Tone.Plain, cell.Tone);
            Assert.Equal("Fire: a degree Scry does not know (42)", cell.Tip);
        }

        [Fact]
        public void ThereAreTenDamageTypesInTheGamesOrder()
        {
            Assert.Equal(new[] { "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit" }, ResistWords.Types);
        }
    }
}
