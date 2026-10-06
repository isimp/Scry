using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class PreOrderWalkTests
    {
        // Reading a location's parts all at once costs a frame of its own for a big one; walked a
        // few at a time, they come in the order the prefab holds them, each part before what is
        // under it, as Unity gives a part and all under it.

        private sealed class Node
        {
            public string Name;
            public List<Node> Under = new List<Node>();

            public Node(string name, params Node[] under)
            {
                Name = name;
                Under.AddRange(under);
            }
        }

        private static Node Tree() =>
            new Node("root",
                new Node("a", new Node("a1"), new Node("a2", new Node("a2x"))),
                new Node("b"),
                new Node("c", new Node("c1")));

        private static PreOrderWalk<Node> Walk(Node root) => new PreOrderWalk<Node>(root, n => n.Under.Count, (n, i) => n.Under[i]);

        [Fact]
        public void EachPartComesBeforeWhatIsUnderItInTheOrderHeld()
        {
            var walk = Walk(Tree());
            var seen = new List<string>();
            while (walk.Next(out var node)) seen.Add(node.Name);

            Assert.Equal(new[] { "root", "a", "a1", "a2", "a2x", "b", "c", "c1" }, seen);
            Assert.True(walk.Done);
        }

        [Fact]
        public void ItGoesOnWhereItStoppedFromFrameToFrame()
        {
            var walk = Walk(Tree());
            var seen = new List<string>();
            for (var i = 0; i < 3 && walk.Next(out var node); i++) seen.Add(node.Name);
            Assert.False(walk.Done);
            while (walk.Next(out var node)) seen.Add(node.Name);

            Assert.Equal(new[] { "root", "a", "a1", "a2", "a2x", "b", "c", "c1" }, seen);
        }

        [Fact]
        public void APartWithNothingUnderItIsAWalkOfOne()
        {
            var walk = Walk(new Node("alone"));
            Assert.True(walk.Next(out var only));
            Assert.Equal("alone", only.Name);
            Assert.False(walk.Next(out _));
            Assert.True(walk.Done);
        }
    }
}
