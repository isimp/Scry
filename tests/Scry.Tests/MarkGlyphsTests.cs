using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarkGlyphsTests
    {
        // A dungeon's mark on Valheim's map carries a glyph for the dungeon's type, told by the
        // location's prefab: each of the game's types its own, a dungeon a mod adds a plain door.
        // The glyphs are drawn from shapes in a 64-unit space, filled for a mark walked by you and
        // as the body's edge for one walked on this server, where an inlay stands solid.

        [Fact]
        public void EachDungeonTypeHasItsOwnGlyphAndAModsDungeonAPlainDoor()
        {
            Assert.Equal(MarkGlyph.Doorway, MarkGlyphs.Of("Crypt2"));
            Assert.Equal(MarkGlyph.Doorway, MarkGlyphs.Of("Crypt3"));
            Assert.Equal(MarkGlyph.Doorway, MarkGlyphs.Of("Crypt4"));
            Assert.Equal(MarkGlyph.Portcullis, MarkGlyphs.Of("SunkenCrypt4"));
            Assert.Equal(MarkGlyph.IcyPeak, MarkGlyphs.Of("MountainCave02"));
            Assert.Equal(MarkGlyph.MineGate, MarkGlyphs.Of("Mistlands_DvergrTownEntrance1"));
            Assert.Equal(MarkGlyph.MineGate, MarkGlyphs.Of("Mistlands_DvergrTownEntrance2"));
            Assert.Equal(MarkGlyph.Crown, MarkGlyphs.Of("Mistlands_DvergrBossEntrance1"));
            Assert.Equal(MarkGlyph.Howl, MarkGlyphs.Of("Hildir_cave"));
            Assert.Equal(MarkGlyph.Flame, MarkGlyphs.Of("Hildir_crypt"));
            Assert.Equal(MarkGlyph.KeyholeTower, MarkGlyphs.Of("Hildir_plainsfortress"));
            Assert.Equal(MarkGlyph.Door, MarkGlyphs.Of("MorkBorg"));
            Assert.Equal(MarkGlyph.Door, MarkGlyphs.Of("TheHole01"));
            Assert.Equal(MarkGlyph.Door, MarkGlyphs.Of(null));
            Assert.Equal(MarkGlyph.Door, MarkGlyphs.Of(""));
        }

        private static IEnumerable<MarkGlyph> All => Enum.GetValues(typeof(MarkGlyph)).Cast<MarkGlyph>();

        [Fact]
        public void EveryGlyphKeepsWithinTheDiscAndIsToldFromTheOthers()
        {
            const int side = 128;
            var masks = All.ToDictionary(g => g, g => MarkRaster.Filled(g, side));
            var reach = MarkRaster.InnerDisc / 64f * side;
            foreach (var (glyph, mask) in masks.Select(p => (p.Key, p.Value)))
            {
                Assert.Equal(side * side, mask.Length);
                var drawn = 0;
                for (var y = 0; y < side; y++)
                {
                    for (var x = 0; x < side; x++)
                    {
                        if (mask[y * side + x] == 0) continue;
                        drawn++;
                        var dx = x + 0.5f - side / 2f;
                        var dy = y + 0.5f - side / 2f;
                        Assert.True(Math.Sqrt(dx * dx + dy * dy) <= reach, $"{glyph} reaches past the disc at {x}, {y}");
                    }
                }
                // A glyph is a bold shape: a good part of the disc, never a sliver.
                Assert.True(drawn > side * side / 25, $"{glyph} draws only {drawn} pixels");
            }
            foreach (var a in All)
            {
                foreach (var b in All)
                {
                    if (a >= b) continue;
                    var differ = 0;
                    for (var i = 0; i < side * side; i++) if (Math.Abs(masks[a][i] - masks[b][i]) > 127) differ++;
                    Assert.True(differ > side * side / 40, $"{a} and {b} differ in only {differ} pixels");
                }
            }
        }

        [Fact]
        public void TheOutlineIsTheBodysEdgeWithItsInlaysSolid()
        {
            const int side = 128;
            float Coverage(byte[] mask) => mask.Sum(v => v) / 255f;
            foreach (var glyph in All)
            {
                var filled = MarkRaster.Filled(glyph, side);
                var outline = MarkRaster.Outline(glyph, side);
                Assert.True(Coverage(outline) > 0f, $"{glyph} has no outline");
                // A shape thinner than two strokes, as the portcullis's bars, is solid in outline too: never more than the body with its inlays.
                Assert.True(Coverage(outline) <= Coverage(filled) + side * side / 20f, $"{glyph}'s outline covers more than its body");
            }
            // The sealed tower's keyhole: a hole in the filled form, solid in the outline; the door's knob the same.
            int At(float x, float y) => (int)(side / 2f - y * side / 64f) * side + (int)(side / 2f + x * side / 64f);
            Assert.True(MarkRaster.Filled(MarkGlyph.KeyholeTower, side)[At(0f, 0f)] < 128);
            Assert.True(MarkRaster.Outline(MarkGlyph.KeyholeTower, side)[At(0f, 0f)] > 127);
            Assert.True(MarkRaster.Filled(MarkGlyph.Door, side)[At(3f, -1f)] < 128);
            Assert.True(MarkRaster.Outline(MarkGlyph.Door, side)[At(3f, -1f)] > 127);
            // A cut is a hole in both: the icy mouth's middle.
            Assert.True(MarkRaster.Filled(MarkGlyph.IcyPeak, side)[At(0f, -12f)] < 128);
            Assert.True(MarkRaster.Outline(MarkGlyph.IcyPeak, side)[At(0f, -12f)] < 128);
            // The doorway's middle is body in the filled form and clear in the outline.
            Assert.True(MarkRaster.Filled(MarkGlyph.Doorway, side)[At(0f, 0f)] > 127);
            Assert.True(MarkRaster.Outline(MarkGlyph.Doorway, side)[At(0f, 0f)] < 128);
        }
    }
}
