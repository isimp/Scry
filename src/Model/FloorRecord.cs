using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>A surface a ray cast straight down met, in the place's own frame: how high, whether flat enough to stand on, and the part of the place it is on.</summary>
    internal struct RaySurface
    {
        public float Height;
        public bool Flat;
        public int Part;
    }

    /// <summary>A part of a place a ray can meet: its name, and whether what the game drops lands on it (<c>ZoneSystem.m_solidRayMask</c>).</summary>
    internal sealed class RayPart
    {
        public string Name = "";
        public bool Solid = true;
    }

    /// <summary>Where a ray of a kept place lands on something flat for a roll: its place on the grid, the height, whether nothing of the place is above it, and the part it is on.</summary>
    internal struct FloorLanding
    {
        public int I, J, Part;
        public float X, Z, Height;
        public bool Open;
    }

    /// <summary>What a probe of one roll of a place saw: the parts there, each ray's surfaces top down by its place in the grid's list, and under each spawn point there, its own.</summary>
    internal sealed class ProbeSeen
    {
        public readonly HashSet<int> Present = new HashSet<int>();
        public readonly Dictionary<int, List<RaySurface>> Rays = new Dictionary<int, List<RaySurface>>();
        public readonly Dictionary<string, List<RaySurface>> Spawns = new Dictionary<string, List<RaySurface>>(StringComparer.Ordinal);
    }

    /// <summary>
    /// What the rays cast over one kind of place met, kept so its floors need no copy woken to be
    /// found again: the grid in the place's own frame, every part a roll can bring, and what each
    /// ray met, each surface with its part, as probes of its rolls saw them (<see cref="With"/>).
    /// A roll whose parts were all seen tells its floors from the record (<see cref="Covers"/>,
    /// <see cref="Landings"/>) as its own rays would: each ray's flat surfaces among the parts
    /// there, open where nothing of them is over it. What a spawn point's creature drops onto is
    /// kept the same way, each spawn point by the parts it was probed with. Stamped as the place
    /// is (<see cref="PlaceStamps"/>), and kept as text (<see cref="Save"/>, <see cref="Load"/>).
    /// A record is never changed once made: what a probe adds makes a new one.
    /// </summary>
    internal sealed class FloorRecord
    {
        /// <summary>How far under the topmost thing on a ray a surface must be for something to be above it.</summary>
        public const float Covered = 0.5f;

        private const string Header = "Scry floors 1";

        public string Prefab = "", Stamp = "";
        public bool Room;

        /// <summary>The footprint the grid is laid over, and the heights the rays are cast from and to, in the place's own frame.</summary>
        public float MinX, MaxX, MinZ, MaxZ, Top, Bottom;

        private List<RayPart> _parts = new List<RayPart>();
        private HashSet<int> _seen = new HashSet<int>();
        private Dictionary<int, List<RaySurface>> _rays = new Dictionary<int, List<RaySurface>>();
        private Dictionary<string, (HashSet<int> Seen, List<RaySurface> Surfaces)> _spawns = new Dictionary<string, (HashSet<int>, List<RaySurface>)>(StringComparer.Ordinal);
        private List<(int I, int J, float X, float Z)> _grid;

        public IReadOnlyList<RayPart> Parts => _parts;

        /// <summary>Where the rays are cast, each with its place on the grid (<see cref="FloorFinder.Grid"/>).</summary>
        public IReadOnlyList<(int I, int J, float X, float Z)> Grid => _grid ?? (_grid = FloorFinder.Grid(MinX, MaxX, MinZ, MaxZ));

        /// <summary>The ground each ray stands for, and all the rays cover.</summary>
        public float Cell => FloorFinder.CellArea(MinX, MaxX, MinZ, MaxZ, Grid.Count);

        public float Ground => Grid.Count * Cell;

        /// <summary>The file a kind of place is kept in, named by a hash of it, as a prefab's name may hold anything.</summary>
        public static string FileName(string prefab, bool room) => Stored.Hash(prefab + "\n" + (room ? "room" : "location")) + ".dat";

        public static FloorRecord New(string prefab, bool room, string stamp, float minX, float maxX, float minZ, float maxZ, float top, float bottom, IReadOnlyList<RayPart> parts) => new FloorRecord
        {
            Prefab = prefab, Room = room, Stamp = stamp, MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ, Top = top, Bottom = bottom,
            _parts = parts.Select(p => new RayPart { Name = p.Name, Solid = p.Solid }).ToList(),
        };

        /// <summary>Whether a copy's parts are this record's: as many, each of the same name and as solid.</summary>
        public bool Fits(IReadOnlyList<RayPart> parts) =>
            parts.Count == _parts.Count && parts.Zip(_parts, (a, b) => a.Name == b.Name && a.Solid == b.Solid).All(same => same);

        /// <summary>Whether a roll with these parts there, and these spawn points, is told whole by the record: every part seen, every spawn point probed with all of them.</summary>
        public bool Covers(ISet<int> present, IEnumerable<string> spawns)
        {
            if (!present.All(_seen.Contains)) return false;
            foreach (var spawn in spawns)
            {
                if (!_spawns.TryGetValue(spawn, out var under) || !present.All(under.Seen.Contains)) return false;
            }
            return true;
        }

        /// <summary>The record with what a probe saw added: the surfaces of the parts it is the first to see, and each spawn point's of the parts it had not been probed with.</summary>
        public FloorRecord With(ProbeSeen seen)
        {
            var next = (FloorRecord)MemberwiseClone();
            next._grid = null;
            next._seen = new HashSet<int>(_seen);
            next._rays = _rays.ToDictionary(r => r.Key, r => r.Value);
            next._spawns = new Dictionary<string, (HashSet<int>, List<RaySurface>)>(_spawns, StringComparer.Ordinal);

            var first = new HashSet<int>(seen.Present.Where(p => p >= 0 && p < _parts.Count && !_seen.Contains(p)));
            foreach (var ray in seen.Rays)
            {
                var adding = ray.Value.Where(s => first.Contains(s.Part)).ToList();
                if (adding.Count == 0) continue;
                next._rays[ray.Key] = TopDown(next._rays.TryGetValue(ray.Key, out var held) ? held.Concat(adding) : adding);
            }
            next._seen.UnionWith(first);

            foreach (var spawn in seen.Spawns)
            {
                var there = new HashSet<int>(seen.Present.Where(p => p >= 0 && p < _parts.Count));
                if (!next._spawns.TryGetValue(spawn.Key, out var under))
                {
                    next._spawns[spawn.Key] = (there, TopDown(spawn.Value.Where(s => there.Contains(s.Part))));
                    continue;
                }
                var adding = spawn.Value.Where(s => there.Contains(s.Part) && !under.Seen.Contains(s.Part));
                var all = new HashSet<int>(under.Seen);
                all.UnionWith(there);
                next._spawns[spawn.Key] = (all, TopDown(under.Surfaces.Concat(adding)));
            }
            return next;
        }

        private static List<RaySurface> TopDown(IEnumerable<RaySurface> surfaces) => surfaces.OrderByDescending(s => s.Height).ToList();

        /// <summary>Where the rays land on something flat for a roll with these parts there, each open where nothing of them is <see cref="Covered"/> over it.</summary>
        public List<FloorLanding> Landings(ISet<int> present)
        {
            var landings = new List<FloorLanding>();
            for (var ray = 0; ray < Grid.Count; ray++)
            {
                if (!_rays.TryGetValue(ray, out var surfaces)) continue;
                var highest = float.NaN;
                foreach (var surface in surfaces)
                {
                    if (!present.Contains(surface.Part)) continue;
                    if (float.IsNaN(highest)) highest = surface.Height;
                    if (!surface.Flat) continue;
                    var at = Grid[ray];
                    landings.Add(new FloorLanding { I = at.I, J = at.J, X = at.X, Z = at.Z, Height = surface.Height, Open = highest - surface.Height < Covered, Part = surface.Part });
                }
            }
            return landings;
        }

        /// <summary>The height a spawn point's creature drops onto for a roll with these parts there: the first solid part under it; false where none is.</summary>
        public bool GroundUnder(string spawn, ISet<int> present, out float height)
        {
            height = 0f;
            if (!_spawns.TryGetValue(spawn, out var under)) return false;
            foreach (var surface in under.Surfaces)
            {
                if (!present.Contains(surface.Part) || !_parts[surface.Part].Solid) continue;
                height = surface.Height;
                return true;
            }
            return false;
        }

        // ----- As text -----

        public string Save()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            Line(text, "place", Stored.Escaped(Prefab), Room ? "1" : "0", Stored.Escaped(Stamp));
            Line(text, "grid", Stored.Exact(MinX), Stored.Exact(MaxX), Stored.Exact(MinZ), Stored.Exact(MaxZ), Stored.Exact(Top), Stored.Exact(Bottom));
            foreach (var part in _parts) Line(text, "part", part.Solid ? "1" : "0", Stored.Escaped(part.Name));
            Line(text, "seen", string.Join(",", _seen.OrderBy(p => p).Select(p => Stored.Count(p))));
            foreach (var ray in _rays.OrderBy(r => r.Key))
            {
                Line(text, "ray", Stored.Count(ray.Key), string.Join(";", ray.Value.Select(s => Stored.Number(s.Height, 3) + ":" + (s.Flat ? "1" : "0") + ":" + Stored.Count(s.Part))));
            }
            foreach (var spawn in _spawns.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                Line(text, "spawn", Stored.Escaped(spawn.Key), string.Join(",", spawn.Value.Seen.OrderBy(p => p).Select(p => Stored.Count(p))),
                    string.Join(";", spawn.Value.Surfaces.Select(s => Stored.Number(s.Height, 3) + ":" + Stored.Count(s.Part))));
            }
            Line(text, "end");
            return text.ToString();
        }

        private static void Line(StringBuilder text, string kind, params string[] fields)
        {
            text.Append(kind);
            foreach (var field in fields) text.Append('\t').Append(field);
            text.Append('\n');
        }

        /// <summary>The record a file's text holds; none for a file of another kind, cut short, or with a line that cannot be read.</summary>
        public static FloorRecord Load(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            if (lines.Count < 3 || lines[0] != Header || lines[lines.Count - 1] != "end") return null;
            var record = new FloorRecord();
            var placed = false;
            var gridded = false;
            for (var i = 1; i < lines.Count - 1; i++)
            {
                var f = lines[i].Split('\t');
                switch (f[0])
                {
                    case "place":
                        if (f.Length != 4 || !Stored.TryUnescaped(f[1], out record.Prefab) || !Flag(f[2], out record.Room) || !Stored.TryUnescaped(f[3], out record.Stamp)) return null;
                        placed = true;
                        break;
                    case "grid":
                        if (f.Length != 7 || !Stored.TryExact(f[1], out record.MinX) || !Stored.TryExact(f[2], out record.MaxX) || !Stored.TryExact(f[3], out record.MinZ)
                            || !Stored.TryExact(f[4], out record.MaxZ) || !Stored.TryExact(f[5], out record.Top) || !Stored.TryExact(f[6], out record.Bottom)) return null;
                        gridded = true;
                        break;
                    case "part":
                        if (f.Length != 3 || !Flag(f[1], out var solid) || !Stored.TryUnescaped(f[2], out var name)) return null;
                        record._parts.Add(new RayPart { Name = name, Solid = solid });
                        break;
                    case "seen":
                        if (f.Length != 2 || !Indexes(f[1], record._parts.Count, out var seen)) return null;
                        record._seen.UnionWith(seen);
                        break;
                    case "ray":
                        if (f.Length != 3 || !gridded || !Stored.TryCount(f[1], out var ray) || ray < 0 || ray >= record.Grid.Count || !Surfaces(f[2], true, record._parts.Count, out var surfaces)) return null;
                        record._rays[ray] = surfaces;
                        break;
                    case "spawn":
                        if (f.Length != 4 || !Stored.TryUnescaped(f[1], out var key) || !Indexes(f[2], record._parts.Count, out var with) || !Surfaces(f[3], false, record._parts.Count, out var under)) return null;
                        record._spawns[key] = (new HashSet<int>(with), under);
                        break;
                    default:
                        return null;
                }
            }
            return placed && gridded ? record : null;
        }

        private static bool Flag(string text, out bool value)
        {
            value = text == "1";
            return text == "1" || text == "0";
        }

        private static bool Indexes(string text, int parts, out List<int> indexes)
        {
            indexes = new List<int>();
            if (text.Length == 0) return true;
            foreach (var each in text.Split(','))
            {
                if (!Stored.TryCount(each, out var index) || index < 0 || index >= parts) return false;
                indexes.Add(index);
            }
            return true;
        }

        private static bool Surfaces(string text, bool flat, int parts, out List<RaySurface> surfaces)
        {
            surfaces = new List<RaySurface>();
            if (text.Length == 0) return true;
            foreach (var each in text.Split(';'))
            {
                var f = each.Split(':');
                if (f.Length != (flat ? 3 : 2) || !Stored.TryNumber(f[0], out var height)) return false;
                var surface = new RaySurface { Height = height };
                if (flat && !Flag(f[1], out surface.Flat)) return false;
                if (!Stored.TryCount(f[flat ? 2 : 1], out surface.Part) || surface.Part < 0 || surface.Part >= parts) return false;
                surfaces.Add(surface);
            }
            return true;
        }
    }
}
