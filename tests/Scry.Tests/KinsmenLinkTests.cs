using Xunit;

namespace Scry.Tests
{
    public class KinsmenLinkTests
    {
        // The optional Kinsmen mod counts a skill far ahead of the group lower on its server. Scry
        // asks it by name, so a game without it, or with a Kinsmen answering another way, shows
        // every level as kept.

        public static class Full
        {
            public const int Version = 1;

            // A false answer counts in full whatever else it leaves in its outs.
            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 0f;
                holder = "Ragna";
                holderLevel = 40f;
                return false;
            }
        }

        public static class HeldByRagna
        {
            public const int Version = 1;

            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 58.75f;
                holder = "Ragna";
                holderLevel = 40f;
                return true;
            }
        }

        public static class WithinAWholeLevel
        {
            public const int Version = 1;

            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 99.6f;
                holder = "Ragna";
                holderLevel = 94.5f;
                return true;
            }
        }

        public static class CappedByHand
        {
            public const int Version = 1;

            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 70f;
                holder = null;
                holderLevel = 60f;
                return true;
            }
        }

        public static class LaterVersion
        {
            public const int Version = 2;

            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 1f;
                holder = null;
                holderLevel = 0f;
                return true;
            }
        }

        public static class NoVersion
        {
            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
            {
                counted = 1f;
                holder = null;
                holderLevel = 0f;
                return true;
            }
        }

        public static class SentenceOnly
        {
            public const int Version = 1;

            public static bool TryCounted(int skill, float savedLevel, out float counted, out string why)
            {
                counted = 1f;
                why = "";
                return true;
            }
        }

        public static class OtherShape
        {
            public const int Version = 1;

            public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder)
            {
                counted = 1f;
                holder = null;
                return true;
            }
        }

        [Fact]
        public void WithoutKinsmenThereIsNoLink()
        {
            Assert.Null(KinsmenLink.From(null, null));
            Assert.Null(KinsmenLink.From(null, 1));
        }

        [Fact]
        public void AKinsmenAnsweringAnotherWayIsLeftAlone()
        {
            Assert.Null(KinsmenLink.From(typeof(LaterVersion), LaterVersion.Version));
            Assert.Null(KinsmenLink.From(typeof(NoVersion), null));
            Assert.Null(KinsmenLink.From(typeof(SentenceOnly), SentenceOnly.Version));
            Assert.Null(KinsmenLink.From(typeof(OtherShape), OtherShape.Version));
            Assert.NotNull(KinsmenLink.From(typeof(Full), Full.Version));
        }

        [Fact]
        public void KinsmenIsFoundByItsOwnNames()
        {
            Assert.Equal("isimp.Kinsmen", KinsmenLink.Guid);
            Assert.Equal("Kinsmen.KinsmenApi", KinsmenLink.TypeName);
            Assert.Equal("TryHolder", KinsmenLink.MethodName);
            Assert.Equal("Version", KinsmenLink.VersionField);
        }

        [Fact]
        public void ASkillCountingInFullKeepsItsSavedLevel()
        {
            var count = KinsmenLink.From(typeof(Full), Full.Version).Count(1, 87.5f);
            Assert.Equal(87.5f, count.Counted);
            Assert.False(count.Held);
            Assert.Null(count.Holder);
        }

        [Fact]
        public void AHeldSkillCountsLowerAndNamesItsHolder()
        {
            var count = KinsmenLink.From(typeof(HeldByRagna), HeldByRagna.Version).Count(1, 100f);
            Assert.Equal(58.75f, count.Counted);
            Assert.True(count.Held);
            Assert.Equal("Ragna", count.Holder);
            Assert.Equal(40f, count.HolderLevel);
        }

        [Fact]
        public void ALoweringWithinTheSameWholeLevelCountsButIsNotTold()
        {
            // The game works with the lowered level, but the whole level the page shows is the same.
            var count = KinsmenLink.From(typeof(WithinAWholeLevel), WithinAWholeLevel.Version).Count(1, 99.9f);
            Assert.Equal(99.6f, count.Counted);
            Assert.False(count.Held);
            Assert.Null(count.Holder);
        }

        [Fact]
        public void ACapSetByHandNamesNoHolder()
        {
            var count = KinsmenLink.From(typeof(CappedByHand), CappedByHand.Version).Count(1, 100f);
            Assert.Equal(70f, count.Counted);
            Assert.True(count.Held);
            Assert.Null(count.Holder);
        }

        [Fact]
        public void WithoutALinkASkillCountsInFull()
        {
            var count = SkillCount.InFull(42.5f);
            Assert.Equal(42.5f, count.Counted);
            Assert.False(count.Held);
        }
    }
}
