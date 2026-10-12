using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>An altar a place holds: the boss it summons, with what and how many, and whether on item stands; by prefab names.</summary>
    internal sealed class PlaceAltar
    {
        public string Boss = "", Item = "";
        public int Count = 1;
        public bool OnStands;
    }

    /// <summary>
    /// What one location or dungeon room was read to hold, as its bundle holds it, kept from one
    /// session to the next (<see cref="KeptPlaces"/>): its contents as read (told anew with each
    /// session's terms, <see cref="PlaceContents.Tell"/>), the kinds of room its dungeons are built
    /// with or the kind of room it is, what of the catalog it names, its altars, and the stamp of
    /// what it was read from, which says whether it still holds the same.
    /// </summary>
    internal sealed class PlaceRecord
    {
        public string Prefab = "";
        public bool Room;

        /// <summary>
        /// The build of Scry that read it and what it was read from (its bundle's file, or the mods
        /// it may come from): a place whose stamp is the same now holds the same (<see cref="PlacePlan"/>).
        /// </summary>
        public string Stamp = "";

        public PlaceContents Contents = new PlaceContents();

        /// <summary>For a location, the kinds of room each dungeon in it is built with (<c>DungeonGenerator.m_themes</c>); for a room, its own kind (<c>Room.m_theme</c>).</summary>
        public List<int> Themes = new List<int>();

        /// <summary>The prefabs of the catalog it names: placed there, named by a field, in a drop table or played by an effect list.</summary>
        public List<string> Names = new List<string>();

        public List<PlaceAltar> Altars = new List<PlaceAltar>();
    }

    /// <summary>
    /// The places read so far, kept from one session to the next as text (<see cref="Save"/>,
    /// <see cref="Load"/>), each with its stamp. Each place is a run of lines
    /// closed by an end line, so a place cut short, or with a line that cannot be read, is left
    /// out and read again, and the rest kept; a file of another kind gives nothing. Text is
    /// written with its tabs, line breaks and backslashes escaped, numbers exactly.
    /// </summary>
    internal sealed class KeptPlaces
    {
        /// <summary>The first line, which a file of these places begins with; another format is another first line.</summary>
        private const string Header = "Scry places 1";

        private readonly List<PlaceRecord> _records = new List<PlaceRecord>();
        private readonly Dictionary<(string Prefab, bool Room), int> _at = new Dictionary<(string Prefab, bool Room), int>();

        public IReadOnlyList<PlaceRecord> Records => _records;

        /// <summary>The place of that prefab, a room or a location; null when none is kept.</summary>
        public PlaceRecord Find(string prefab, bool room) => _at.TryGetValue((prefab, room), out var at) ? _records[at] : null;

        /// <summary>Keeps a place, in the place of one of its prefab and kind kept before.</summary>
        public void Put(PlaceRecord record)
        {
            var key = (record.Prefab, record.Room);
            if (_at.TryGetValue(key, out var at))
            {
                _records[at] = record;
                return;
            }
            _at[key] = _records.Count;
            _records.Add(record);
        }

        public string Save()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            foreach (var record in _records) Write(text, record);
            return text.ToString();
        }

        private static void Write(StringBuilder text, PlaceRecord record)
        {
            var c = record.Contents;
            Line(text, "place", record.Prefab, Flag(record.Room), record.Stamp);
            Line(text, "names", c.GameNameKey, c.TraderKey, c.BossPrefab);
            foreach (var part in c.Parts) Line(text, "part", part.Prefab, Stored.Count(part.Count), Stored.Exact(part.Chance));
            foreach (var creature in c.Creatures) Line(text, "creature", creature.Prefab, Stored.Count(creature.Count), Stored.Exact(creature.Chance));
            foreach (var vegvisir in c.Vegvisirs) Line(text, "vegvisir", vegvisir);
            Line(text, "levels", Stored.Count(c.EnemyMinLevel), Stored.Count(c.EnemyMaxLevel), Stored.Exact(c.EnemyLevelUpChance), Flag(c.LevelOverrideExceptions));
            Line(text, "nobuild", Flag(c.NoBuild), Stored.Exact(c.NoBuildRadius));
            Line(text, "rolls", Flag(c.PartsRolled), Flag(c.StarsRolled), Flag(c.GroupsRolled));
            foreach (var music in c.OwnMusic) Line(text, "music", music.When.ToString(), music.Name, Stored.Exact(music.Chance));
            foreach (var weather in c.Weathers) Line(text, "weather", weather);
            foreach (var stone in c.Stones)
            {
                Line(text, "stone", stone.Name);
                foreach (var rune in stone.Texts) Line(text, "rune", rune.Topic, rune.Text);
            }
            if (c.Dungeon != null)
            {
                var d = c.Dungeon;
                var fields = new List<string>
                {
                    d.Algorithm, Stored.Count(d.Themes), Stored.Count(d.MinRooms), Stored.Count(d.MaxRooms), Stored.Count(d.MinRequiredRooms), Flag(d.Weighted),
                    Stored.Exact(d.DoorChance), Stored.Count(d.GridSize), Stored.Exact(d.TileWidth), Stored.Exact(d.SpawnChance), Stored.Exact(d.CampRadiusMin),
                    Stored.Exact(d.CampRadiusMax), Stored.Count(d.PerimeterSections), Stored.Exact(d.PerimeterBuffer), Flag(d.CustomInterior),
                };
                fields.AddRange(Vector(d.ZoneSize));
                fields.AddRange(Vector(d.ZoneFromGenerator));
                fields.AddRange(Vector(d.GeneratorAt));
                fields.AddRange(Turn(d.GeneratorTurn));
                Line(text, "dungeon", fields.ToArray());
                foreach (var required in d.RequiredRooms) Line(text, "required", required);
                foreach (var door in d.Doors) Line(text, "door", door.Type, Stored.Exact(door.Chance), door.Prefab);
            }
            if (c.Room != null)
            {
                var r = c.Room;
                var fields = new List<string> { r.Name, Stored.Count(r.Theme) };
                fields.AddRange(Vector(r.Size));
                fields.AddRange(new[] { Flag(r.Entrance), Flag(r.EndCap), Flag(r.Divider), Flag(r.Perimeter), Flag(r.FaceCenter), Stored.Count(r.EndCapPrio), Stored.Count(r.MinPlaceOrder), Stored.Exact(r.Weight) });
                Line(text, "room", fields.ToArray());
                foreach (var doorway in r.Doorways)
                {
                    var way = new List<string> { doorway.Type, Flag(doorway.Entrance), Flag(doorway.AllowDoor), Flag(doorway.DoorOnlyIfOtherAllows) };
                    way.AddRange(Vector(doorway.Position));
                    way.AddRange(Turn(doorway.Rotation));
                    Line(text, "doorway", way.ToArray());
                }
            }
            foreach (var theme in record.Themes) Line(text, "theme", Stored.Count(theme));
            foreach (var name in record.Names) Line(text, "named", name);
            foreach (var altar in record.Altars) Line(text, "altar", altar.Boss, altar.Item, Stored.Count(altar.Count), Flag(altar.OnStands));
            Line(text, "end");
        }

        private static void Line(StringBuilder text, string kind, params string[] fields)
        {
            text.Append(kind);
            foreach (var field in fields) text.Append('\t').Append(Stored.Escaped(field ?? ""));
            text.Append('\n');
        }

        private static string Flag(bool value) => value ? "1" : "0";

        private static string[] Vector(Vec3 v) => new[] { Stored.Exact(v.X), Stored.Exact(v.Y), Stored.Exact(v.Z) };

        private static string[] Turn(Quat q) => new[] { Stored.Exact(q.X), Stored.Exact(q.Y), Stored.Exact(q.Z), Stored.Exact(q.W) };

        /// <summary>The places kept in a file's text; none for a file of another kind or none.</summary>
        public static KeptPlaces Load(string text)
        {
            var kept = new KeptPlaces();
            if (string.IsNullOrEmpty(text)) return kept;
            var lines = text.Split('\n');
            if (lines[0].TrimEnd('\r') != Header) return kept;

            PlaceRecord record = null;
            var whole = true;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                var raw = line.Split('\t');
                var fields = new string[raw.Length - 1];
                var read = true;
                for (var f = 1; f < raw.Length; f++) read &= Stored.TryUnescaped(raw[f], out fields[f - 1]);
                var kind = raw[0];

                if (kind == "place")
                {
                    // A place begun before this one and never ended is cut short: left out.
                    record = null;
                    if (!read || fields.Length != 3 || !Flag(fields[1], out var room)) continue;
                    record = new PlaceRecord { Prefab = fields[0], Room = room, Stamp = fields[2] };
                    whole = true;
                    continue;
                }
                if (record == null) continue;
                if (kind == "end")
                {
                    if (whole) kept.Put(record);
                    record = null;
                    continue;
                }
                whole &= read && ReadLine(record, kind, fields);
            }
            return kept;
        }

        /// <summary>Takes one line of a place; false for a line that cannot be read, which leaves the place out.</summary>
        private static bool ReadLine(PlaceRecord record, string kind, string[] f)
        {
            var c = record.Contents;
            switch (kind)
            {
                case "names":
                    if (f.Length != 3) return false;
                    c.GameNameKey = f[0];
                    c.TraderKey = f[1];
                    c.BossPrefab = f[2];
                    return true;
                case "part":
                case "creature":
                    if (f.Length != 3 || !Count(f[1], out var count) || !Stored.TryExact(f[2], out var chance)) return false;
                    (kind == "part" ? c.Parts : c.Creatures).Add(new PlacePart { Prefab = f[0], Count = count, Chance = chance });
                    return true;
                case "vegvisir":
                    if (f.Length != 1) return false;
                    c.Vegvisirs.Add(f[0]);
                    return true;
                case "levels":
                    if (f.Length != 4 || !Stored.TryCount(f[0], out var min) || !Stored.TryCount(f[1], out var max) || !Stored.TryExact(f[2], out var up) || !Flag(f[3], out var exceptions)) return false;
                    c.EnemyMinLevel = min;
                    c.EnemyMaxLevel = max;
                    c.EnemyLevelUpChance = up;
                    c.LevelOverrideExceptions = exceptions;
                    return true;
                case "nobuild":
                    if (f.Length != 2 || !Flag(f[0], out var noBuild) || !Stored.TryExact(f[1], out var radius)) return false;
                    c.NoBuild = noBuild;
                    c.NoBuildRadius = radius;
                    return true;
                case "rolls":
                    if (f.Length != 3 || !Flag(f[0], out var parts) || !Flag(f[1], out var stars) || !Flag(f[2], out var groups)) return false;
                    c.PartsRolled = parts;
                    c.StarsRolled = stars;
                    c.GroupsRolled = groups;
                    return true;
                case "music":
                    if (f.Length != 3 || !Enum.TryParse<MusicWhen>(f[0], out var when) || !Enum.IsDefined(typeof(MusicWhen), when) || !Stored.TryExact(f[2], out var often)) return false;
                    c.OwnMusic.Add(new PlaceMusic { When = when, Name = f[1], Chance = often });
                    return true;
                case "weather":
                    if (f.Length != 1) return false;
                    c.Weathers.Add(f[0]);
                    return true;
                case "stone":
                    if (f.Length != 1) return false;
                    c.Stones.Add(new PlaceRunestone { Name = f[0] });
                    return true;
                case "rune":
                    if (f.Length != 2 || c.Stones.Count == 0) return false;
                    c.Stones[c.Stones.Count - 1].Texts.Add(new RuneText { Topic = f[0], Text = f[1] });
                    return true;
                case "dungeon":
                    return f.Length == 28 && Dungeon(c, f);
                case "required":
                    if (f.Length != 1 || c.Dungeon == null) return false;
                    c.Dungeon.RequiredRooms.Add(f[0]);
                    return true;
                case "door":
                    if (f.Length != 3 || c.Dungeon == null || !Stored.TryExact(f[1], out var doorChance)) return false;
                    c.Dungeon.Doors.Add((f[0], doorChance, f[2]));
                    return true;
                case "room":
                    return f.Length == 13 && RoomOf(c, f);
                case "doorway":
                    return f.Length == 11 && c.Room != null && DoorwayOf(c.Room, f);
                case "theme":
                    if (f.Length != 1 || !Stored.TryCount(f[0], out var theme)) return false;
                    record.Themes.Add(theme);
                    return true;
                case "named":
                    if (f.Length != 1) return false;
                    record.Names.Add(f[0]);
                    return true;
                case "altar":
                    if (f.Length != 4 || !Count(f[2], out var items) || !Flag(f[3], out var onStands)) return false;
                    record.Altars.Add(new PlaceAltar { Boss = f[0], Item = f[1], Count = items, OnStands = onStands });
                    return true;
                default:
                    return false;
            }
        }

        private static bool Dungeon(PlaceContents c, string[] f)
        {
            var d = new DungeonPlan { Algorithm = f[0] };
            var read = Stored.TryCount(f[1], out d.Themes) & Stored.TryCount(f[2], out d.MinRooms) & Stored.TryCount(f[3], out d.MaxRooms)
                       & Stored.TryCount(f[4], out d.MinRequiredRooms) & Flag(f[5], out d.Weighted) & Stored.TryExact(f[6], out d.DoorChance)
                       & Stored.TryCount(f[7], out d.GridSize) & Stored.TryExact(f[8], out d.TileWidth) & Stored.TryExact(f[9], out d.SpawnChance)
                       & Stored.TryExact(f[10], out d.CampRadiusMin) & Stored.TryExact(f[11], out d.CampRadiusMax) & Stored.TryCount(f[12], out d.PerimeterSections)
                       & Stored.TryExact(f[13], out d.PerimeterBuffer) & Flag(f[14], out d.CustomInterior)
                       & Vector(f, 15, out d.ZoneSize) & Vector(f, 18, out d.ZoneFromGenerator) & Vector(f, 21, out d.GeneratorAt) & Turn(f, 24, out d.GeneratorTurn);
            if (!read) return false;
            c.Dungeon = d;
            return true;
        }

        private static bool RoomOf(PlaceContents c, string[] f)
        {
            var r = new RoomShape { Name = f[0] };
            var read = Stored.TryCount(f[1], out r.Theme) & Vector(f, 2, out r.Size) & Flag(f[5], out r.Entrance) & Flag(f[6], out r.EndCap)
                       & Flag(f[7], out r.Divider) & Flag(f[8], out r.Perimeter) & Flag(f[9], out r.FaceCenter) & Stored.TryCount(f[10], out r.EndCapPrio)
                       & Stored.TryCount(f[11], out r.MinPlaceOrder) & Stored.TryExact(f[12], out r.Weight);
            if (!read) return false;
            c.Room = r;
            return true;
        }

        private static bool DoorwayOf(RoomShape room, string[] f)
        {
            var way = new Doorway { Type = f[0] };
            var read = Flag(f[1], out way.Entrance) & Flag(f[2], out way.AllowDoor) & Flag(f[3], out way.DoorOnlyIfOtherAllows)
                       & Vector(f, 4, out way.Position) & Turn(f, 7, out way.Rotation);
            if (!read) return false;
            room.Doorways.Add(way);
            return true;
        }

        private static bool Flag(string text, out bool value)
        {
            value = text == "1";
            return text == "1" || text == "0";
        }

        private static bool Count(string text, out int value) => Stored.TryCount(text, out value) && value >= 0;

        private static bool Vector(string[] f, int at, out Vec3 v)
        {
            var read = Stored.TryExact(f[at], out var x) & Stored.TryExact(f[at + 1], out var y) & Stored.TryExact(f[at + 2], out var z);
            v = new Vec3(x, y, z);
            return read;
        }

        private static bool Turn(string[] f, int at, out Quat q)
        {
            var read = Stored.TryExact(f[at], out var x) & Stored.TryExact(f[at + 1], out var y) & Stored.TryExact(f[at + 2], out var z) & Stored.TryExact(f[at + 3], out var w);
            q = new Quat(x, y, z, w);
            return read;
        }
    }
}
