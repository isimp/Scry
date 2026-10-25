using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The glyph a dungeon's mark on Valheim's map carries, one for each of the game's kinds of dungeon and one for those mods add.</summary>
    internal enum MarkGlyph
    {
        /// <summary>A stone doorway: the Burial Chambers.</summary>
        Doorway,

        /// <summary>A portcullis, spiked at the foot: the Sunken Crypt's iron gate.</summary>
        Portcullis,

        /// <summary>A peak with an icy cave mouth: the Frost Caves.</summary>
        IcyPeak,

        /// <summary>Posts, a beam and a hanging lamp: the Infested Mine.</summary>
        MineGate,

        /// <summary>The Queen's crown: the Infested Citadel.</summary>
        Crown,

        /// <summary>A cave mouth and its howl rising: Hildir's Howling Cavern.</summary>
        Howl,

        /// <summary>A flame with a doorway in its foot: Hildir's Smouldering Tomb.</summary>
        Flame,

        /// <summary>A tower with a keyhole: Hildir's Sealed Tower.</summary>
        KeyholeTower,

        /// <summary>A plain door: a dungeon a mod adds, whose shape Scry cannot know.</summary>
        Door,
    }

    /// <summary>One shape of a glyph, in the mark's 64-unit space with y up and the disc's middle at 0, 0: a polygon, the upper half of an ellipse, a disc or an arc stroke; a cut is a hole in the body, an inlay a hole that stands solid in the outline.</summary>
    internal sealed class MarkPart
    {
        private enum Form { Poly, Arch, Dot, Band }

        private Form _form;
        private (float X, float Y)[] _points;
        private float _x, _y, _rx, _ry, _width, _from, _to;

        /// <summary>Whether the part is a hole in the body.</summary>
        public bool Cut { get; private set; }

        /// <summary>Whether the part is a hole in the body that stands solid in the outline.</summary>
        public bool Inlay { get; private set; }

        /// <summary>A polygon through its points.</summary>
        public static MarkPart Poly(params (float X, float Y)[] points) => new MarkPart { _form = Form.Poly, _points = points };

        /// <summary>A rectangle from its lower left to its upper right corner.</summary>
        public static MarkPart Rect(float x0, float y0, float x1, float y1) => Poly((x0, y1), (x1, y1), (x1, y0), (x0, y0));

        /// <summary>A rounded-top shape: a half disc over a rectangle, from <paramref name="y0"/> at the foot to <paramref name="y1"/> at the top.</summary>
        public static MarkPart[] Arch(float x0, float y0, float x1, float y1)
        {
            var r = (x1 - x0) / 2f;
            return new[] { HalfEllipse((x0 + x1) / 2f, y1 - r, r, r), Rect(x0, y0, x1, y1 - r) };
        }

        /// <summary>The upper half of an ellipse: a dome, from <paramref name="y0"/> at its foot to <paramref name="y1"/> at its top.</summary>
        public static MarkPart Dome(float x0, float y0, float x1, float y1) => HalfEllipse((x0 + x1) / 2f, y0, (x1 - x0) / 2f, y1 - y0);

        private static MarkPart HalfEllipse(float x, float y, float rx, float ry) => new MarkPart { _form = Form.Arch, _x = x, _y = y, _rx = rx, _ry = ry };

        /// <summary>A disc.</summary>
        public static MarkPart Dot(float x, float y, float r) => new MarkPart { _form = Form.Dot, _x = x, _y = y, _rx = r };

        /// <summary>An arc stroke of an outer radius and a width, between two angles in degrees, counter-clockwise from the right.</summary>
        public static MarkPart Band(float x, float y, float r, float width, float from, float to) =>
            new MarkPart { _form = Form.Band, _x = x, _y = y, _rx = r, _width = width, _from = from, _to = to };

        /// <summary>The parts given as holes in the body.</summary>
        public static MarkPart[] CutOut(params MarkPart[] parts)
        {
            foreach (var part in parts) part.Cut = true;
            return parts;
        }

        /// <summary>The parts given as inlays: holes in the body that stand solid in the outline.</summary>
        public static MarkPart[] InlayOf(params MarkPart[] parts)
        {
            foreach (var part in parts) part.Inlay = true;
            return parts;
        }

        /// <summary>Whether a point of the mark's space lies in the part.</summary>
        public bool Holds(float x, float y)
        {
            switch (_form)
            {
                case Form.Poly: return InPolygon(x, y);
                case Form.Arch:
                    if (y < _y) return false;
                    var ex = (x - _x) / _rx;
                    var ey = (y - _y) / _ry;
                    return ex * ex + ey * ey <= 1f;
                case Form.Dot: return (x - _x) * (x - _x) + (y - _y) * (y - _y) <= _rx * _rx;
                default:
                    var dx = x - _x;
                    var dy = y - _y;
                    var d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > _rx || d < _rx - _width) return false;
                    var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (angle < 0) angle += 360.0;
                    return angle >= _from && angle <= _to;
            }
        }

        /// <summary>Whether a point lies in the polygon, by the crossings of a ray from it.</summary>
        private bool InPolygon(float x, float y)
        {
            var inside = false;
            for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
            {
                var (xi, yi) = _points[i];
                var (xj, yj) = _points[j];
                if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>
    /// The glyph of each dungeon's mark, told by the location's prefab: the game's own kinds each
    /// theirs, every other a plain door, as a mod's dungeon has a shape Scry cannot know; and each
    /// glyph's shapes (<see cref="MarkPart"/>), drawn by <see cref="MarkRaster"/>.
    /// </summary>
    internal static class MarkGlyphs
    {
        /// <summary>The glyph of a location's dungeon, by the prefab's name; a plain door for one not the game's own or none.</summary>
        public static MarkGlyph Of(string location)
        {
            if (string.IsNullOrEmpty(location)) return MarkGlyph.Door;
            if (location.StartsWith("SunkenCrypt", StringComparison.Ordinal)) return MarkGlyph.Portcullis;
            if (location.StartsWith("Crypt", StringComparison.Ordinal)) return MarkGlyph.Doorway;
            if (location.StartsWith("MountainCave", StringComparison.Ordinal)) return MarkGlyph.IcyPeak;
            if (location.StartsWith("Mistlands_DvergrTownEntrance", StringComparison.Ordinal)) return MarkGlyph.MineGate;
            if (location.StartsWith("Mistlands_DvergrBossEntrance", StringComparison.Ordinal)) return MarkGlyph.Crown;
            switch (location)
            {
                case "Hildir_cave": return MarkGlyph.Howl;
                case "Hildir_crypt": return MarkGlyph.Flame;
                case "Hildir_plainsfortress": return MarkGlyph.KeyholeTower;
                default: return MarkGlyph.Door;
            }
        }

        /// <summary>A glyph's shapes, in the mark's 64-unit space.</summary>
        public static IReadOnlyList<MarkPart> PartsOf(MarkGlyph glyph)
        {
            var parts = new List<MarkPart>();
            switch (glyph)
            {
                case MarkGlyph.Doorway:
                    parts.AddRange(MarkPart.Arch(-12f, -16f, 12f, 16f));
                    break;
                case MarkGlyph.Portcullis:
                    parts.Add(MarkPart.Rect(-15f, 8f, 15f, 13f));
                    parts.Add(MarkPart.Rect(-15f, -3f, 15f, 1f));
                    foreach (var x in new[] { -10f, 0f, 10f })
                    {
                        parts.Add(MarkPart.Rect(x - 2.5f, -10f, x + 2.5f, 16f));
                        parts.Add(MarkPart.Poly((x - 2.5f, -10f), (x + 2.5f, -10f), (x, -17f)));
                    }
                    break;
                case MarkGlyph.IcyPeak:
                    parts.Add(MarkPart.Poly((-18f, -15f), (18f, -15f), (16f, -3f), (0f, 19f), (-16f, -3f)));
                    parts.AddRange(MarkPart.CutOut(MarkPart.Poly((-10f, -15f), (-10f, -2f), (-6.7f, -9.5f), (-3.3f, -2f), (0f, -9.5f), (3.3f, -2f), (6.7f, -9.5f), (10f, -2f), (10f, -15f))));
                    break;
                case MarkGlyph.MineGate:
                    parts.Add(MarkPart.Rect(-17f, 8f, 17f, 14f));
                    parts.Add(MarkPart.Rect(-13f, -16f, -7f, 8f));
                    parts.Add(MarkPart.Rect(7f, -16f, 13f, 8f));
                    parts.Add(MarkPart.Rect(-1.5f, 0f, 1.5f, 8f));
                    parts.Add(MarkPart.Dot(0f, -2.5f, 3.8f));
                    break;
                case MarkGlyph.Crown:
                    parts.Add(MarkPart.Poly((-15f, -13f), (15f, -13f), (15f, -5f), (12f, 9f), (6f, -1f), (0f, 12f), (-6f, -1f), (-12f, 9f), (-15f, -5f)));
                    parts.Add(MarkPart.Dot(-12f, 11.5f, 3.2f));
                    parts.Add(MarkPart.Dot(0f, 14.5f, 3.2f));
                    parts.Add(MarkPart.Dot(12f, 11.5f, 3.2f));
                    break;
                case MarkGlyph.Howl:
                    parts.Add(MarkPart.Dome(-15f, -16f, 15f, 0f));
                    parts.AddRange(MarkPart.CutOut(MarkPart.Arch(-5.5f, -16f, 5.5f, -5f)));
                    parts.Add(MarkPart.Band(0f, -8f, 15f, 3.5f, 35f, 145f));
                    parts.Add(MarkPart.Band(0f, -8f, 22f, 3.5f, 50f, 130f));
                    break;
                case MarkGlyph.Flame:
                    parts.Add(MarkPart.Dot(0f, -5f, 13f));
                    parts.Add(MarkPart.Poly((-13f, -4f), (-11f, 6f), (-5f, 13f), (-1f, 21f), (3f, 11f), (6f, 7f), (10f, 14f), (13f, 3f), (13f, -4f)));
                    parts.AddRange(MarkPart.CutOut(MarkPart.Arch(-5f, -19f, 5f, -5f)));
                    break;
                case MarkGlyph.KeyholeTower:
                    parts.Add(MarkPart.Rect(-12f, -16f, 12f, 10f));
                    parts.Add(MarkPart.Rect(-12f, 10f, -6f, 16f));
                    parts.Add(MarkPart.Rect(-2f, 10f, 2f, 16f));
                    parts.Add(MarkPart.Rect(6f, 10f, 12f, 16f));
                    parts.AddRange(MarkPart.InlayOf(MarkPart.Dot(0f, 0f, 4.4f), MarkPart.Poly((-1.8f, -1f), (1.8f, -1f), (3.2f, -9.5f), (-3.2f, -9.5f))));
                    break;
                default:
                    parts.Add(MarkPart.Rect(-10.5f, -17f, 10.5f, 17f));
                    parts.AddRange(MarkPart.InlayOf(MarkPart.Dot(3f, -1f, 2.5f)));
                    break;
            }
            return parts;
        }
    }
}
