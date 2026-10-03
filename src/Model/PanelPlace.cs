using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where the panel was left and how its stage was seen, as kept in <c>panel.txt</c>: one
    /// setting a line, its name and then its value ("full 10 20 900 600", "light 2", "spin 1").
    /// A setting the file does not hold, or holds in a way it cannot be read, is null and left
    /// as the panel has it; one bad line costs only itself.
    /// </summary>
    internal sealed class PanelPlace
    {
        /// <summary>A window's place and size on the screen.</summary>
        public readonly struct Area : IEquatable<Area>
        {
            public readonly float X, Y, Width, Height;

            public Area(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public bool Equals(Area other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
            public override bool Equals(object obj) => obj is Area other && Equals(other);
            public override int GetHashCode() => (X, Y, Width, Height).GetHashCode();
        }

        public Area? Full, Compact;
        public bool? CompactView;
        public int? Lighting, Backdrop;
        public string Ground;
        public bool? Person, Worn, Spin, Creatures;
        public IReadOnlyList<string> Folded;
        public float? StageScale, ListShareFull, ListShareCompact;
        public bool? ListHiddenFull, ListHiddenCompact;

        /// <summary>The settings in the file's lines.</summary>
        public static PanelPlace Read(IEnumerable<string> lines)
        {
            var place = new PanelPlace();
            foreach (var line in lines)
            {
                var parts = (line ?? "").Split(' ');
                var name = parts[0];
                var value = parts.Length == 2 ? parts[1] : null;
                switch (name)
                {
                    case "full": place.Full = AreaOf(parts) ?? place.Full; break;
                    case "compact": place.Compact = AreaOf(parts) ?? place.Compact; break;
                    case "view" when value != null: place.CompactView = value == "compact"; break;
                    case "light" when Stored.TryCount(value, out var light): place.Lighting = light; break;
                    case "backdrop" when Stored.TryCount(value, out var backdrop): place.Backdrop = backdrop; break;
                    case "ground" when value != null: place.Ground = value; break;
                    case "person" when value != null: place.Person = value == "1"; break;
                    case "worn" when value != null: place.Worn = value == "1"; break;
                    case "spin" when value != null: place.Spin = value == "1"; break;
                    case "creatures" when value != null: place.Creatures = value == "1"; break;
                    case "folded": place.Folded = parts.Skip(1).Where(key => key.Length > 0).ToArray(); break;
                    case "stage" when Stored.TryNumber(value, out var stage): place.StageScale = stage; break;
                    case "list" when Stored.TryNumber(value, out var list): place.ListShareFull = list; break;
                    case "listhidden" when value != null: place.ListHiddenFull = value == "1"; break;
                    case "clist" when Stored.TryNumber(value, out var compactList): place.ListShareCompact = compactList; break;
                    case "clisthidden" when value != null: place.ListHiddenCompact = value == "1"; break;
                }
            }
            return place;
        }

        /// <summary>The file's lines for what is set, the window places to the whole pixel.</summary>
        public IEnumerable<string> Lines()
        {
            string Flag(bool on) => on ? "1" : "0";
            if (Full.HasValue) yield return AreaLine("full", Full.Value);
            if (Compact.HasValue) yield return AreaLine("compact", Compact.Value);
            if (CompactView.HasValue) yield return "view " + (CompactView.Value ? "compact" : "full");
            if (Lighting.HasValue) yield return "light " + Stored.Count(Lighting.Value);
            if (Backdrop.HasValue) yield return "backdrop " + Stored.Count(Backdrop.Value);
            if (Ground != null) yield return "ground " + Ground;
            if (Person.HasValue) yield return "person " + Flag(Person.Value);
            if (Worn.HasValue) yield return "worn " + Flag(Worn.Value);
            if (Spin.HasValue) yield return "spin " + Flag(Spin.Value);
            if (Creatures.HasValue) yield return "creatures " + Flag(Creatures.Value);
            if (Folded != null) yield return "folded " + string.Join(" ", Folded);
            if (StageScale.HasValue) yield return "stage " + Stored.Number(StageScale.Value);
            if (ListShareFull.HasValue) yield return "list " + Stored.Number(ListShareFull.Value);
            if (ListHiddenFull.HasValue) yield return "listhidden " + Flag(ListHiddenFull.Value);
            if (ListShareCompact.HasValue) yield return "clist " + Stored.Number(ListShareCompact.Value);
            if (ListHiddenCompact.HasValue) yield return "clisthidden " + Flag(ListHiddenCompact.Value);
        }

        private static Area? AreaOf(string[] parts)
        {
            if (parts.Length != 5) return null;
            var values = new float[4];
            for (var i = 0; i < 4; i++)
            {
                if (!Stored.TryNumber(parts[i + 1], out values[i])) return null;
            }
            return new Area(values[0], values[1], values[2], values[3]);
        }

        private static string AreaLine(string name, Area area) =>
            name + " " + string.Join(" ", new[] { area.X, area.Y, area.Width, area.Height }.Select(v => Stored.Number(v, 0)));
    }
}
