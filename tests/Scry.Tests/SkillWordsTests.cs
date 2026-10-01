using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SkillWordsTests
    {
        // A skill the game has goes by its name. A skill a mod adds has no name in the game's
        // list, only a number, and its mod gives it a name under the game's own word for a
        // skill ("$skill_" and that number), as the game's skill screen looks it up.

        private static string Localize(string token)
        {
            var words = new Dictionary<string, string> { ["$skill_1795429391"] = "Ranching", ["$skill_swords"] = "Schwerter" };
            return words.TryGetValue(token, out var word) ? word : "";
        }

        [Fact]
        public void AGameSkillGoesByItsName()
        {
            Assert.Equal("Swords", SkillWords.Name("Swords", Localize));
            Assert.Equal("Elemental magic", SkillWords.Name("ElementalMagic", Localize));
        }

        [Fact]
        public void AModsSkillGoesByTheNameItsModGivesIt()
        {
            Assert.Equal("Ranching", SkillWords.Name("1795429391", Localize));
        }

        [Fact]
        public void AModsSkillWithNoNameIsStillTold()
        {
            Assert.Equal("a skill a mod adds", SkillWords.Name("-48213", Localize));
            Assert.Equal("a skill a mod adds", SkillWords.Name("48213", null));
        }

        [Fact]
        public void NoSkillIsNone()
        {
            Assert.Null(SkillWords.Name("", Localize));
            Assert.Null(SkillWords.Name(null, Localize));
        }
    }
}
