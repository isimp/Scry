using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumWordsTests
    {
        // A silhouette tells only what kind of thing it is; a page not yet open says how much the
        // character knows and what would open it.

        private static readonly Kind[] Silhouetted = { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Spawner, Kind.StatusEffect, Kind.Location, Kind.Raid, Kind.Biome };

        [Fact]
        public void ASilhouetteIsNamedOnlyByItsKindEachKindInItsOwnWords()
        {
            Assert.Equal("Unknown creature", CompendiumWords.Unknown(Kind.Creature));
            Assert.Equal("Unknown place", CompendiumWords.Unknown(Kind.Location));
            Assert.Equal(Silhouetted.Length, Silhouetted.Select(CompendiumWords.Unknown).Distinct().Count());
        }

        [Fact]
        public void AKindsTabCountsWhatWasFoundOfAllThereIs()
        {
            Assert.Equal("Creatures  <color=#aabbcc>37/1,112</color>", CompendiumWords.Tab("Creatures", 37, 1112, "aabbcc"));
        }

        [Fact]
        public void AClosedPageSaysHowMuchIsKnownAndWhatWouldOpenIt()
        {
            Assert.Equal("Not met yet", CompendiumWords.Closed(KnownLevel.Unknown));
            Assert.Equal("Heard of, not met yet", CompendiumWords.Closed(KnownLevel.HeardOf));
            Assert.Equal("Seen, not yet come to know", CompendiumWords.Closed(KnownLevel.Seen));
            Assert.Equal("Fight it to learn all about it.", CompendiumWords.HowToKnow(Kind.Creature));
            Assert.Equal(Silhouetted.Length, Silhouetted.Select(CompendiumWords.HowToKnow).Distinct().Count());
        }
    }
}
