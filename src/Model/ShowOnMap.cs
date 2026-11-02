using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Why a page's Show on map can show nothing now; none where it can.</summary>
    internal enum MapBlock
    {
        None,

        /// <summary>The world has no map (the game's no-map setting).</summary>
        NoMap,

        /// <summary>The server lets the map keep no spots (its map reach is 0).</summary>
        NoReach,

        /// <summary>Keeping where things were seen is off, and none were kept before.</summary>
        RecordingOff,

        /// <summary>The character's file of spots could not be read.</summary>
        Unreadable,

        /// <summary>None of this kind was seen in this world yet.</summary>
        NoneSeen,
    }

    /// <summary>What Show on map says: its strip on the map, its button's tip, and the You box's line.</summary>
    internal static class SpotWords
    {
        /// <summary>The You box's label for how many of a kind the character saw (<see cref="YouLine"/>).</summary>
        public const string YouLabel = "Where you saw it";

        /// <summary>The words of the link and the button that show them on the map.</summary>
        public const string ShowOnMap = "Show on map";

        /// <summary>The strip's button taking the dots away.</summary>
        public const string Hide = "Hide";

        /// <summary>How near the nearest stands to be beside you rather than at a distance.</summary>
        private const float Beside = 5f;

        /// <summary>The strip over the map: the kind, how many the character saw, and how far and which way the nearest is.</summary>
        public static string Strip(string name, int count, float? nearest, string direction)
        {
            var said = $"{name} · {Numbers.Count(count)} you saw";
            if (nearest is float metres) said += metres < Beside ? " · nearest beside you" : $" · nearest {Distance(metres)} {direction}";
            return said;
        }

        /// <summary>A distance as the strip gives it: to the metre under a hundred, in tens of metres under a kilometre, past it in kilometres.</summary>
        public static string Distance(float metres)
        {
            if (metres >= 1000f) return Numbers.Fixed(Math.Round(metres / 1000f, 1), 1) + " km";
            var rounded = metres < 100f ? Math.Round(metres) : Math.Round(metres / 10f) * 10f;
            return Numbers.Count((long)rounded) + " m";
        }

        /// <summary>Which way a point lies, by the map's compass: north up, z growing to the north, x to the east.</summary>
        public static string Direction(float dx, float dz)
        {
            var degrees = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            var sector = (int)Math.Round((degrees + 360.0) % 360.0 / 45.0) % 8;
            return Compass[sector];
        }

        private static readonly string[] Compass = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>The You box's line: how many of the kind the character saw in this world.</summary>
        public static string YouLine(int count) => $"{Numbers.Count(count)} in this world";

        /// <summary>The button's tip where it can show them.</summary>
        public static string Tip(int count) => count == 1
            ? "Open Valheim's map with a dot where your character saw it in this world"
            : $"Open Valheim's map with a dot on each of the {Numbers.Count(count)} your character saw in this world";

        /// <summary>The greyed button's tip: why it shows nothing now.</summary>
        public static string WhyNot(MapBlock block)
        {
            switch (block)
            {
                case MapBlock.NoMap: return "This world has no map.";
                case MapBlock.NoReach: return "This server keeps no spots for the map.";
                case MapBlock.RecordingOff: return "Keeping where your character saw things is off in Scry's settings.";
                case MapBlock.Unreadable: return "The file of where your character saw things could not be read.";
                case MapBlock.NoneSeen: return "Your character has not seen this in this world since Scry began keeping where things stand.";
                default: return null;
            }
        }
    }

    /// <summary>
    /// The kind Show on map shows now: its spots as kept when the button was pressed, by world,
    /// until Hide, until the map opened for them closes, or until it never opens (as in a world
    /// whose map the game keeps shut), so nothing waits for the next time the map opens.
    /// </summary>
    internal sealed class MapShowing
    {
        /// <summary>How many frames the map may take to open after the button before the spots are dropped.</summary>
        public const int WaitTicks = 30;

        private static readonly IReadOnlyList<(int X, int Z)> None = Array.Empty<(int, int)>();

        private string _world;
        private bool _opened;
        private int _waited;

        public string Kind { get; private set; }
        public string Name { get; private set; }
        public IReadOnlyList<(int X, int Z)> Spots { get; private set; } = None;

        public bool Showing => Kind != null;

        /// <summary>A kind's spots shown in a world, in place of any shown before.</summary>
        public void Show(string world, string kind, string name, IReadOnlyList<(int X, int Z)> spots)
        {
            _world = world;
            Kind = kind;
            Name = name;
            Spots = spots ?? None;
            _opened = false;
            _waited = 0;
        }

        public void Hide()
        {
            _world = null;
            Kind = null;
            Name = null;
            Spots = None;
        }

        /// <summary>Each frame: whether the large map is open, and the world played.</summary>
        public void Tick(bool largeOpen, string world)
        {
            if (!Showing) return;
            if (!string.Equals(world, _world, StringComparison.Ordinal)) Hide();
            else if (largeOpen) _opened = true;
            else if (_opened || ++_waited >= WaitTicks) Hide();
        }
    }
}
