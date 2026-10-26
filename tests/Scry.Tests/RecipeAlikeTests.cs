using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class RecipeAlikeTests
    {
        // An item's page shows each way of making it once: recipes the game holds several times
        // over, the same station, level, amount and ingredients, as a mod registering one again
        // leaves them, are one row; a recipe differing in anything keeps a row of its own.

        private static string Key(string title, params (string, string)[] items) => RecipeAlike.Key(title, "forge", items, new List<(string, string)> { ("Idol", "1") });

        [Fact]
        public void TheSameRecipeHeldAgainIsShownOnce()
        {
            var shown = new RecipeAlike();
            Assert.True(shown.First(Key("Made at Forge level 3", ("FineWood", "15"), ("BlackMetal", "10"))));
            Assert.False(shown.First(Key("Made at Forge level 3", ("FineWood", "15"), ("BlackMetal", "10"))));
        }

        [Fact]
        public void ARecipeDifferingInAnythingKeepsItsRow()
        {
            var shown = new RecipeAlike();
            Assert.True(shown.First(Key("Made at Forge level 3", ("FineWood", "15"), ("BlackMetal", "10"))));
            Assert.True(shown.First(Key("Made at Forge level 2", ("FineWood", "15"), ("BlackMetal", "10"))));
            Assert.True(shown.First(Key("Made at Forge level 3", ("FineWood", "16"), ("BlackMetal", "10"))));
            Assert.True(shown.First(Key("Made at Forge level 3", ("FineWood", "15"), ("Iron", "10"))));
            Assert.True(shown.First(RecipeAlike.Key("Made at Forge level 3", "forge", new[] { ("FineWood", "15"), ("BlackMetal", "10") }, new List<(string, string)>())));
            // Words that run together never read as another recipe's.
            Assert.True(shown.First(Key("Made at Forge level 3", ("Fine", "Wood15"), ("BlackMetal", "10"))));
            // Where one part ends and the next begins is kept, a bar in a name too.
            var none = new List<(string, string)>();
            var idol = new List<(string, string)> { ("Idol", "1") };
            Assert.True(shown.First(RecipeAlike.Key("Made", "forge", idol, none)));
            Assert.True(shown.First(RecipeAlike.Key("Made", "forge", none, idol)));
            Assert.True(shown.First(RecipeAlike.Key("Made|forge", "", none, none)));
            Assert.True(shown.First(RecipeAlike.Key("Made", "forge|", none, none)));
        }
    }
}
