using Xunit;

namespace Scry.Tests
{
    public class BirthYoungTests
    {
        // A tame creature's birth names what it brought (Procreation.Procreate spawns its offspring,
        // or its offspring without a partner): a creature by the newest creature in the world, an
        // item, as the egg a hen lays, by its kind where only one item could have come. So a hen
        // seen laying is paired with its egg, as a lox with its calf.

        [Fact]
        public void AHenSeenLayingNamesItsEgg()
        {
            Assert.Equal("ChickenEgg", BirthYoung.Named(null, new[] { ("ChickenEgg", false) }));
            // Whatever creature came last into the world.
            Assert.Equal("ChickenEgg", BirthYoung.Named("Boar", new[] { ("ChickenEgg", false) }));
        }

        [Fact]
        public void ACalfIsNamedOnlyWhereItIsTheNewestCreature()
        {
            Assert.Equal("Lox_Calf", BirthYoung.Named("Lox_Calf", new[] { ("Lox_Calf", true) }));
            Assert.Null(BirthYoung.Named("Boar", new[] { ("Lox_Calf", true) }));
            Assert.Null(BirthYoung.Named(null, new[] { ("Lox_Calf", true) }));
        }

        [Fact]
        public void WhereTwoItemsCouldHaveComeNoneIsNamed()
        {
            Assert.Null(BirthYoung.Named(null, new[] { ("ChickenEgg", false), ("GoldenEgg", false) }));
            // One kind given twice is one kind.
            Assert.Equal("ChickenEgg", BirthYoung.Named(null, new[] { ("ChickenEgg", false), ("ChickenEgg", false) }));
            // A creature not the newest: the item came.
            Assert.Equal("ChickenEgg", BirthYoung.Named("Boar", new[] { ("Chick", true), ("ChickenEgg", false) }));
            Assert.Equal("Chick", BirthYoung.Named("Chick", new[] { ("Chick", true), ("ChickenEgg", false) }));
            Assert.Null(BirthYoung.Named(null, new (string, bool)[0]));
            Assert.Null(BirthYoung.Named(null, new[] { ((string)null, false) }));
            // An empty slot is no kind: the one item left is the young.
            Assert.Equal("ChickenEgg", BirthYoung.Named(null, new[] { ((string)null, false), ("ChickenEgg", false) }));
            // The newest creature names only a creature the birth could bring, never an item of its name.
            Assert.Null(BirthYoung.Named("ChickenEgg", new[] { ("ChickenEgg", false), ("GoldenEgg", false) }));
        }
    }

    public class ChainLinkTests
    {
        // Under the strict rules a chain names each step seen together with a step next to it, the
        // way the chain goes: a hen laying its egg, the egg hatching, the chick growing up. Wherever
        // a thing stands in the chain, once a link of it was seen it is named, so the chain reads the
        // same on every page.

        private static readonly Chain Breeding = new Chain
        {
            Title = "Breeding",
            Steps = new System.Collections.Generic.List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" }, new[] { "Hen" } },
        };

        private static System.Func<string, string, bool> Seen(params (string, string)[] pairs) =>
            (a, b) => System.Array.Exists(pairs, p => (p.Item1 == a && p.Item2 == b) || (p.Item1 == b && p.Item2 == a));

        [Fact]
        public void EachStepSeenWithItsNeighbourIsNamed()
        {
            var linked = Breeding.Linked(Seen(("ChickenEgg", "Chicken"), ("Chicken", "Hen")));
            Assert.Equal(new[] { "Chicken", "ChickenEgg", "Hen" }, linked.OrderBy());
            Assert.Equal(new[] { "Chicken", "ChickenEgg" }, Breeding.Linked(Seen(("ChickenEgg", "Chicken"))).OrderBy());
            Assert.Empty(Breeding.Linked(Seen()));
        }

        [Fact]
        public void APairNotNextToEachOtherInTheChainLinksNothing()
        {
            // Seen together otherwise, a hen and a chick do not make the egg between them known.
            var chain = new Chain { Title = "Breeding", Steps = new System.Collections.Generic.List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" } } };
            Assert.Empty(chain.Linked(Seen(("Hen", "Chicken"))));
        }

        [Fact]
        public void AStepOfSeveralNamesEachOfItsOwn()
        {
            var chain = new Chain { Title = "Planting", Steps = new System.Collections.Generic.List<string[]> { new[] { "sapling" }, new[] { "tree", "tree_old" }, new[] { "wood" } } };
            Assert.Equal(new[] { "tree_old", "wood" }, chain.Linked(Seen(("tree_old", "wood"))).OrderBy());
        }
    }

    internal static class LinkedOrder
    {
        public static string[] OrderBy(this System.Collections.Generic.IEnumerable<string> keys) =>
            System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(keys, k => k, System.StringComparer.Ordinal));
    }
}
