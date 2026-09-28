using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ShapeListTests
    {
        private const string Source =
@"        private static readonly (string Type, string Method, int Params, uint Shape, string Feature)[] Copied =
        {
            (""VisEquipment"", ""AttachItem"", 6, 0xD7DAC228, ""gear on creatures and the person""),
            (""ItemDrop+ItemData"", ""GetTooltip"", 6, 0xE717E1A4, ""item stats in the details""),

            // What the details tell the game's rules from.
            (""Fermenter"", ""UpdateCover"", 2, 0x861BA786, ""what a fermenter needs, in the details""),
        };";

        private static uint? Game(ShapeEntry e, Dictionary<string, uint> shapes) =>
            shapes.TryGetValue(e.Type + "." + e.Method, out var shape) ? shape : (uint?)null;

        [Fact]
        public void EveryEntryIsReadWithItsLine()
        {
            var entries = ShapeList.Parse(Source);

            Assert.Equal(3, entries.Count);
            Assert.Equal("ItemDrop+ItemData", entries[1].Type);
            Assert.Equal("GetTooltip", entries[1].Method);
            Assert.Equal(6, entries[1].Params);
            Assert.Equal(0xE717E1A4u, entries[1].Shape);
            Assert.Equal("item stats in the details", entries[1].Feature);
            Assert.Equal(4, entries[1].Line);
            Assert.Equal(7, entries[2].Line);
        }

        [Fact]
        public void AChangedShapeIsWrittenInItsPlaceAndNothingElseMoves()
        {
            var shapes = new Dictionary<string, uint>
            {
                ["VisEquipment.AttachItem"] = 0xD7DAC228, ["ItemDrop+ItemData.GetTooltip"] = 0x12345678, ["Fermenter.UpdateCover"] = 0x861BA786,
            };

            var updated = ShapeList.Update(Source, e => Game(e, shapes), out var report);

            Assert.Contains("(\"ItemDrop+ItemData\", \"GetTooltip\", 6, 0x12345678, \"item stats in the details\"),", updated);
            Assert.Equal(Source.Replace("0xE717E1A4", "0x12345678"), updated);
            var line = Assert.Single(report);
            Assert.Contains("changed", line);
            Assert.Contains("ItemDrop+ItemData.GetTooltip", line);
            Assert.Contains("0xE717E1A4 -> 0x12345678", line);
            Assert.Contains("item stats in the details", line);
        }

        [Fact]
        public void AMethodTheGameNoLongerHasIsToldAndLeftForAPerson()
        {
            var shapes = new Dictionary<string, uint> { ["VisEquipment.AttachItem"] = 0xD7DAC228, ["ItemDrop+ItemData.GetTooltip"] = 0xE717E1A4 };

            var updated = ShapeList.Update(Source, e => Game(e, shapes), out var report);

            Assert.Equal(Source, updated);
            var line = Assert.Single(report);
            Assert.StartsWith("missing", line);
            Assert.Contains("Fermenter.UpdateCover (2 parameters)", line);
        }

        [Fact]
        public void NothingChangedSaysNothingAndLeavesTheFileAsItIs()
        {
            var entries = ShapeList.Parse(Source);
            var updated = ShapeList.Update(Source, e => e.Shape, out var report);

            Assert.Equal(Source, updated);
            Assert.Empty(report);
            Assert.Equal(3, entries.Count(e => e.Shape != 0));
        }
    }
}
