using Xunit;

namespace Scry.Tests
{
    public class BuildTests
    {
        // WearNTear.GetMaterialProperties gives each material its support on the ground, the least
        // it stands with, and the share lost per metre sideways and up (WearNTear.UpdateSupport).

        [Fact]
        public void SupportSaysWhatAPieceGivesOnTheGroundAndWhenItFalls()
        {
            Assert.Equal("100 on the ground, falls below 10", BuildWords.Support(100f, 10f, needsSupport: true));
            Assert.Equal("1,500 on the ground, falls below 20", BuildWords.Support(1500f, 20f, needsSupport: true));
        }

        [Fact]
        public void APieceThatNeedsNoSupportNeverFallsForLackOfIt()
        {
            Assert.Equal("100 on the ground, stands without it", BuildWords.Support(100f, 10f, needsSupport: false));
        }

        [Fact]
        public void AMaterialWithoutSupportFiguresSaysNothing()
        {
            // A material the game does not know (a mod's own) gets no figures at all.
            Assert.Null(BuildWords.Support(0f, 0f, needsSupport: true));
        }

        [Fact]
        public void SupportLossIsTheShareLostForEachMetreSidewaysAndUp()
        {
            Assert.Equal("20% a metre sideways, 12.5% a metre up", BuildWords.SupportLoss(0.2f, 0.125f));
            Assert.Equal("16.7% a metre sideways, 10% a metre up", BuildWords.SupportLoss(1f / 6f, 0.1f));
            Assert.Equal("7.7% a metre either way", BuildWords.SupportLoss(1f / 13f, 1f / 13f));
            Assert.Null(BuildWords.SupportLoss(0f, 0f));
        }

        [Fact]
        public void RainWearsAnUnroofedPieceDownToHalf()
        {
            // WearNTear.UpdateWear: while wet and unroofed, 5% of its health each minute, until half is left.
            Assert.Equal("5% of its health a minute while wet and unroofed, until half is left", BuildWords.Rain(true));
            Assert.Equal("does not wear it", BuildWords.Rain(false));
        }

        [Fact]
        public void AshAndSnowAreToldOnlyForPiecesThatStandUpToThem()
        {
            Assert.Equal("unharmed by ash and lava", BuildWords.Ash(immune: true, resists: false));
            Assert.Equal("ash wears it only down to a tenth, lava a third as hard", BuildWords.Ash(immune: false, resists: true));
            Assert.Null(BuildWords.Ash(immune: false, resists: false));
            Assert.Equal("does not harm it", BuildWords.Snow(immune: true));
            Assert.Null(BuildWords.Snow(immune: false));
        }
    }
}
