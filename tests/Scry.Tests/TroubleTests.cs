using System;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TroubleTests
    {
        [Fact]
        public void AMemberTheGameNoLongerHasIsToldAsTheGameChanged()
        {
            // A field or method an update renamed fails the first time the code naming it runs.
            Assert.True(Trouble.IsGameChange(new MissingFieldException("ItemDrop+ItemData+SharedData", "m_blockPower")));
            Assert.True(Trouble.IsGameChange(new MissingMethodException("Character", "GetLevel")));
            Assert.True(Trouble.IsGameChange(new TypeLoadException("SE_Stats")));
            Assert.False(Trouble.IsGameChange(new NullReferenceException()));
            Assert.False(Trouble.IsGameChange(null));
        }

        [Fact]
        public void AGameChangeIsFoundWhereverItIsWrapped()
        {
            var inner = new MissingFieldException("Piece", "m_comfortGroup");
            Assert.True(Trouble.IsGameChange(new TypeInitializationException("Scry.Facts", inner)));
            Assert.True(Trouble.IsGameChange(new System.Reflection.TargetInvocationException(inner)));
        }

        [Fact]
        public void SkipsAreCountedByPartAndToldOnceWithAnExample()
        {
            var trouble = new Trouble();
            trouble.Skip("links", "Troll", new NullReferenceException("a"));
            trouble.Skip("links", "Greydwarf", new NullReferenceException("b"));
            trouble.Skip("drops", "Boar", new InvalidOperationException("c"));

            var lines = trouble.Summary().ToList();
            Assert.Equal(2, lines.Count);
            Assert.Contains(lines, l => l.StartsWith("links: 2 prefabs") && l.Contains("Troll"));
            Assert.Contains(lines, l => l.StartsWith("drops: 1 prefab,") && l.Contains("Boar"));
        }

        [Fact]
        public void AFeatureTheGameChangedIsKeptForThePanelOnce()
        {
            var trouble = new Trouble();
            Assert.True(trouble.Changed("item stats", new MissingFieldException("x")));
            Assert.False(trouble.Changed("item stats", new MissingFieldException("y")));
            trouble.Changed("links", new MissingMethodException("z"));

            Assert.Equal(new[] { "item stats", "links" }, trouble.ChangedFeatures);
        }

        [Fact]
        public void NothingSkippedSaysNothing()
        {
            Assert.Empty(new Trouble().Summary());
            Assert.Empty(new Trouble().ChangedFeatures);
        }

        [Fact]
        public void ForgettingStartsAfresh()
        {
            var trouble = new Trouble();
            trouble.Skip("links", "Troll", new NullReferenceException());
            trouble.Changed("links", new MissingFieldException());
            trouble.Forget();

            Assert.Empty(trouble.Summary());
            Assert.Empty(trouble.ChangedFeatures);
        }
    }
}
