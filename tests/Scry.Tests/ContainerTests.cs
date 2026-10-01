using Xunit;

namespace Scry.Tests
{
    public class ContainerTests
    {
        // A chest, cart or ship holds so many slots, in rows as its inventory shows them
        // (Container.m_width a row, m_height rows).

        [Fact]
        public void AContainerTellsHowManySlotsItHoldsInRows()
        {
            Assert.Equal("10, in 2 rows of 5", ContainerWords.Slots(5, 2));
            Assert.Equal("32, in 4 rows of 8", ContainerWords.Slots(8, 4));
            Assert.Equal("6, in one row", ContainerWords.Slots(6, 1));
            Assert.Equal("1", ContainerWords.Slots(1, 1));
        }

        [Fact]
        public void AContainerWithNoSlotsTellsNone()
        {
            Assert.Null(ContainerWords.Slots(0, 4));
            Assert.Null(ContainerWords.Slots(5, 0));
            Assert.Null(ContainerWords.Slots(-1, 2));
        }
    }
}
