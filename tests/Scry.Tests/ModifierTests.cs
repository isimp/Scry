using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class ModifierTests
    {
        private static Modifiers For(Entry entry)
        {
            var modifiers = new Modifiers();
            modifiers.ResetFor(entry);
            return modifiers;
        }

        [Fact]
        public void ScaleStaysWithinSensibleBounds()
        {
            var modifiers = For(E("Troll", Kind.Creature));

            modifiers.Scale = 0f;
            Assert.Equal(Modifiers.MinScale, modifiers.Scale);

            modifiers.Scale = 1000f;
            Assert.Equal(Modifiers.MaxScale, modifiers.Scale);

            modifiers.Scale = float.NaN;
            Assert.Equal(1f, modifiers.Scale);
        }

        [Fact]
        public void ACreatureCanOnlyBeShownAtLevelsItHasALookFor()
        {
            var troll = E("Troll", Kind.Creature);
            troll.ExtraLevels = 2;
            var modifiers = For(troll);

            Assert.Equal(3, modifiers.MaxLevel);

            modifiers.Level = 5;
            Assert.Equal(3, modifiers.Level);

            modifiers.Level = 0;
            Assert.Equal(1, modifiers.Level);
        }

        [Fact]
        public void ACreatureWithoutStarLooksStaysAtTheFirstLevel()
        {
            var modifiers = For(E("Deer", Kind.Creature));

            modifiers.Level = 3;

            Assert.Equal(1, modifiers.MaxLevel);
            Assert.Equal(1, modifiers.Level);
        }

        [Fact]
        public void WearIsOfferedOnlyForPiecesThatHaveIt()
        {
            var wall = E("wood_wall", Kind.Piece);
            wall.HasWear = true;
            var worn = For(wall);
            worn.Wear = Wear.Broken;

            var plain = For(E("sign", Kind.Piece));
            plain.Wear = Wear.Broken;

            Assert.True(worn.WearAvailable);
            Assert.Equal(Wear.Broken, worn.Wear);
            Assert.False(plain.WearAvailable);
            Assert.Equal(Wear.New, plain.Wear);
        }

        [Fact]
        public void AnimationSpeedStaysBetweenStoppedAndThreeTimes()
        {
            var modifiers = For(E("Troll", Kind.Creature));

            modifiers.AnimationSpeed = -1f;
            Assert.Equal(0f, modifiers.AnimationSpeed);

            modifiers.AnimationSpeed = 10f;
            Assert.Equal(Modifiers.MaxAnimationSpeed, modifiers.AnimationSpeed);
        }

        [Fact]
        public void ResetRestoresThePrefabAsItIs()
        {
            var troll = E("Troll", Kind.Creature);
            troll.ExtraLevels = 2;
            troll.HasWear = true;
            var modifiers = For(troll);
            modifiers.Scale = 3f;
            modifiers.Level = 3;
            modifiers.Wear = Wear.Worn;
            modifiers.AnimationSpeed = 2f;

            modifiers.Reset();

            Assert.Equal(1f, modifiers.Scale);
            Assert.Equal(1, modifiers.Level);
            Assert.Equal(Wear.New, modifiers.Wear);
            Assert.Equal(1f, modifiers.AnimationSpeed);
            Assert.Equal(3, modifiers.MaxLevel);
        }

        [Fact]
        public void VolumeGoesFromSilentToTwiceTheGamesOwn()
        {
            var modifiers = new Modifiers();
            Assert.Equal(1f, modifiers.Volume);

            modifiers.Volume = 1.5f;
            Assert.Equal(1.5f, modifiers.Volume);
            modifiers.Volume = 5f;
            Assert.Equal(Modifiers.MaxVolume, modifiers.Volume);
            Assert.Equal(2f, Modifiers.MaxVolume);
            modifiers.Volume = -1f;
            Assert.Equal(0f, modifiers.Volume);
            modifiers.Volume = float.NaN;
            Assert.Equal(1f, modifiers.Volume);
        }

        [Fact]
        public void EverySelectionStartsAtTheGamesOwnVolume()
        {
            // Some previews play as soon as they are selected; one turned up must not carry over to the next.
            var modifiers = new Modifiers { Volume = 2f };
            modifiers.ResetFor(new Entry { Name = "sfx_troll_idle" });
            Assert.Equal(1f, modifiers.Volume);

            modifiers.Volume = 0.5f;
            modifiers.Reset();
            Assert.Equal(1f, modifiers.Volume);
        }

        [Fact]
        public void TurningTheVolumeDoesNotRebuildThePreview()
        {
            // A rebuild would start the sound again at every step of the slider.
            var modifiers = new Modifiers();
            var version = modifiers.Version;
            modifiers.Volume = 1.8f;
            Assert.Equal(version, modifiers.Version);
        }
    }
}
