using System.IO;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class FavouritesTests
    {
        [Fact]
        public void FavouritesSurviveARestart()
        {
            var path = Path.Combine(TempDir(), "favourites.txt");
            new Favourites(path).Toggle(E("Troll", Kind.Creature));

            var later = new Favourites(path);

            Assert.True(later.Contains(E("Troll", Kind.Creature)));
        }

        [Fact]
        public void AnUnstarredFavouriteStaysGoneAfterARestart()
        {
            var path = Path.Combine(TempDir(), "favourites.txt");
            var favourites = new Favourites(path);
            favourites.Toggle(E("Troll", Kind.Creature));
            favourites.Toggle(E("Troll", Kind.Creature));

            Assert.False(new Favourites(path).Contains(E("Troll", Kind.Creature)));
        }

        [Fact]
        public void TheFolderIsMadeWhenTheFirstFavouriteIsSaved()
        {
            var path = Path.Combine(TempDir(), "not-yet", "favourites.txt");

            new Favourites(path).Toggle(E("Troll", Kind.Creature));

            Assert.True(File.Exists(path));
        }

        [Fact]
        public void AnUnreadableFavouritesFileStartsEmptyAndNothingBreaks()
        {
            // A folder where the file should be cannot be read or written.
            var path = Path.Combine(TempDir(), "favourites.txt");
            Directory.CreateDirectory(path);

            var favourites = new Favourites(path);
            favourites.Toggle(E("Troll", Kind.Creature));

            Assert.True(favourites.Contains(E("Troll", Kind.Creature)));
        }

        [Fact]
        public void AFavouriteFromAModThatIsSwitchedOffIsKept()
        {
            var path = Path.Combine(TempDir(), "favourites.txt");
            File.WriteAllLines(path, new[] { "CoolMod_TrollStatue" });

            new Favourites(path).Toggle(E("Troll", Kind.Creature));

            Assert.Contains("CoolMod_TrollStatue", File.ReadAllLines(path));
        }

        [Fact]
        public void AStatusEffectAndAPrefabWithTheSameNameAreSeparateFavourites()
        {
            var favourites = new Favourites(Path.Combine(TempDir(), "favourites.txt"));

            favourites.Toggle(E("Rested", Kind.StatusEffect));

            Assert.True(favourites.Contains(E("Rested", Kind.StatusEffect)));
            Assert.False(favourites.Contains(E("Rested", Kind.Other)));
        }

        [Fact]
        public void BlankLinesAndStraySpacesInTheFileAreIgnored()
        {
            var path = Path.Combine(TempDir(), "favourites.txt");
            File.WriteAllLines(path, new[] { "", "  Troll  ", "   " });

            var favourites = new Favourites(path);

            Assert.True(favourites.Contains(E("Troll", Kind.Creature)));
            Assert.Single(favourites.Keys);
        }
    }
}
