using UnityEngine;

namespace Scry
{
    /// <summary>The colours Scry's marks stand in on Valheim's map.</summary>
    internal static partial class Skin
    {
        /// <summary>A place's dots on Valheim's map: a sky blue, as the places' pale stone would read white inside the dots' light ring.</summary>
        public static readonly Color MapPlace = new Color(0.35f, 0.66f, 0.94f);

        /// <summary>A dot as its picture draws it, and one of a kind not shown alone while another is, faded (<see cref="SeenOnMap.SoloOthers"/>).</summary>
        public static readonly Color MarkShown = new Color(1f, 1f, 1f, 1f);
        public static readonly Color MarkFaded = new Color(1f, 1f, 1f, SeenOnMap.SoloOthers);

        /// <summary>The colour a kind's dots stand in on Valheim's map: its list colour, a place's sky blue.</summary>
        public static Color MapDot(Kind kind) => kind == Kind.Location ? MapPlace : KindColor(kind);

        /// <summary>
        /// The colours the kinds on Valheim's map are told apart by (<see cref="KeptMarks"/>),
        /// each clear of the others and of the map's greens, browns and blues, none the accent's
        /// amber, which every kind of dungeon keeps.
        /// </summary>
        private static readonly Color[] Marks =
        {
            new Color32(0xA8, 0xD3, 0x6A, 0xFF), // lime
            new Color32(0x7C, 0xC4, 0xF0, 0xFF), // sky
            new Color32(0xE5, 0x8A, 0x9A, 0xFF), // rose
            new Color32(0xB4, 0x9C, 0xF0, 0xFF), // violet
            new Color32(0x6F, 0xD3, 0xC0, 0xFF), // teal
            new Color32(0xE0, 0x7A, 0x45, 0xFF), // rust
            new Color32(0xD6, 0x5F, 0xC8, 0xFF), // magenta, clear of the writing colour a square of several kinds stands in
            new Color32(0xE0, 0x4E, 0x58, 0xFF), // red
        };

        /// <summary>The colour of a kind's mark on the map; the accent for every kind of dungeon.</summary>
        public static Color MarkColour(int mark) => mark < 0 ? Accent : Marks[KeptMarks.ColourOf(mark)];

        private static Texture2D _pointedRing, _eyeShut;

        /// <summary>The eye struck through from its lower left to its upper right, a gap kept round the stroke, for what is hidden on the map.</summary>
        public static Texture2D EyeShut => _eyeShut != null ? _eyeShut : _eyeShut = Shape(32, 32, (x, y) =>
        {
            var across = Mathf.Abs(x + y - 32f) / 1.4142f;
            var onStroke = x >= 5f && x <= 27f;
            if (onStroke && across <= 1.6f) return true;
            if (onStroke && across <= 3.4f) return false;
            return EyeCoverage(x, y);
        });

        /// <summary>A thin ring round the mark the pointer rests on, on Valheim's map.</summary>
        public static Texture2D PointedRing => _pointedRing != null ? _pointedRing : _pointedRing = Shape(32, 32, (x, y) =>
        {
            var r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
            return r <= 15.5f && r >= 13.5f;
        });
    }
}
