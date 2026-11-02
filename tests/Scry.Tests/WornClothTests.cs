using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    // Cloth worn on a body is set up as the game puts it on (VisEquipment.SetupCloth): it rests
    // against the wearer's shapes and moves with the wearer's bones, found by name.
    public class WornClothTests
    {
        [Fact]
        public void WornClothRestsAgainstTheWearersShapesFirstThenItsOwn()
        {
            Assert.Equal(new[] { "thigh L", "thigh R", "hood rim" }, WornCloth.Colliders(new[] { "thigh L", "thigh R" }, new[] { "hood rim" }));
        }

        [Fact]
        public void ClothWithNoShapesOfItsOwnRestsAgainstTheWearers()
        {
            Assert.Equal(new[] { "thigh L", "thigh R" }, WornCloth.Colliders(new[] { "thigh L", "thigh R" }, new string[0]));
        }

        [Fact]
        public void OnAWearerWithNoShapesClothRestsAgainstItsOwn()
        {
            Assert.Equal(new[] { "hood rim" }, WornCloth.Colliders(new string[0], new[] { "hood rim" }));
            Assert.Empty(WornCloth.Colliders(new string[0], new string[0]));
        }

        [Fact]
        public void TheWearersShapesAreNotChangedBySettingUpItsCloth()
        {
            var wearer = new List<string> { "thigh L" };
            WornCloth.Colliders(wearer, new[] { "hood rim" });
            Assert.Equal(new[] { "thigh L" }, wearer);
        }

        private sealed class Bone
        {
            public string Name;
            public int Id;
        }

        [Fact]
        public void WornClothFindsTheWearersBonesByName()
        {
            var bones = new[] { new Bone { Name = "Hips", Id = 1 }, new Bone { Name = "Spine", Id = 2 }, new Bone { Name = "Head", Id = 3 } };
            var map = WornCloth.BoneMap(bones, b => b.Name);
            Assert.Equal(new[] { "Head", "Hips", "Spine" }, map.Keys.OrderBy(k => k, System.StringComparer.Ordinal));
            Assert.Equal(2, map["Spine"].Id);
        }

        [Fact]
        public void ABoneNamedTwiceLeadsToTheFirstOfThatName()
        {
            var bones = new[] { new Bone { Name = "Hips", Id = 1 }, new Bone { Name = "Hips", Id = 2 } };
            Assert.Equal(1, WornCloth.BoneMap(bones, b => b.Name)["Hips"].Id);
        }

        [Fact]
        public void AMissingOrNamelessBoneIsLeftOut()
        {
            var bones = new[] { null, new Bone { Name = null, Id = 1 }, new Bone { Name = "Head", Id = 2 } };
            var map = WornCloth.BoneMap(bones, b => b.Name);
            Assert.Equal(new[] { "Head" }, map.Keys);
        }
    }
}
