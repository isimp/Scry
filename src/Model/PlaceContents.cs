using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>One kind of networked part a location or room places, how many of it, and the chance each is there.</summary>
    internal sealed class PlacePart
    {
        public string Prefab = "";
        public int Count;

        /// <summary>From 0 to 1; 1 is always there.</summary>
        public float Chance;
    }

    /// <summary>One kind of thing across a dungeon's or camp's kinds of room: its prefab, and in how many of them it is (<see cref="PlaceParts.Across"/>).</summary>
    internal sealed class PartInRooms
    {
        public string Prefab = "";
        public int Rooms;
    }

    /// <summary>What a place's part is, which row of its page it is told in, in the order the rows come.</summary>
    internal enum PartRole
    {
        /// <summary>Chests and what is picked or picked up.</summary>
        Loot,

        /// <summary>What is mined, felled or broken for what it gives.</summary>
        Gather,

        /// <summary>The rest: spawners, creatures, altars, stations and the like.</summary>
        Other,

        /// <summary>Building pieces.</summary>
        Built,
    }

    /// <summary>What a part has, as its components tell, to know its <see cref="PartRole"/>.</summary>
    internal struct PartTraits
    {
        /// <summary>A container: a chest, remains holding loot.</summary>
        public bool Container;

        /// <summary>Picked or picked up: a pickable, a pickable item, an item lying there.</summary>
        public bool Pickup;

        /// <summary>Mined or felled: a rock, an ore vein, a tree, a log.</summary>
        public bool Gathered;

        /// <summary>Broken for what it drops.</summary>
        public bool Breaks;

        /// <summary>A building piece: a piece, or something that wears as one.</summary>
        public bool Built;

        /// <summary>Used for what it does: a station, a bed, a fire, an altar, an item stand, a vegvisir, a runestone, a portal, a trader.</summary>
        public bool Used;

        /// <summary>A spawner, or a creature itself.</summary>
        public bool Spawns;
    }

    /// <summary>
    /// The networked parts of a location or room as the game places them (<c>ZoneSystem.SpawnLocation</c>,
    /// <c>DungeonGenerator.PlaceRoom</c>): each rolled once when its zone is first built, at the
    /// chance of every <c>RandomSpawn</c> above it and the share of every <c>RandomObject</c> pick.
    /// </summary>
    internal static class PlaceParts
    {
        /// <summary>
        /// Each part by its prefab and chance, counted together, what is always there first, then
        /// the likeliest, then by name. Chances equal to a tenth of a percent are one.
        /// </summary>
        public static List<PlacePart> Group(IEnumerable<(string Prefab, float Chance)> raw)
        {
            var parts = new Dictionary<(string, int), PlacePart>();
            foreach (var (prefab, chance) in raw)
            {
                if (string.IsNullOrEmpty(prefab)) continue;
                var key = (prefab, (int)Math.Round(chance * 1000f));
                if (!parts.TryGetValue(key, out var part)) parts[key] = part = new PlacePart { Prefab = prefab, Chance = chance };
                part.Count++;
            }
            return parts.Values
                .OrderByDescending(p => (int)Math.Round(p.Chance * 1000f))
                .ThenBy(p => p.Prefab, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>The rows a place's parts are told in, in their order.</summary>
        public static readonly PartRole[] Roles = { PartRole.Loot, PartRole.Gather, PartRole.Other, PartRole.Built };

        /// <summary>
        /// The row a part is told in: chests and pickups whatever else they are; spawners and what
        /// is used for what it does with the rest, built or not; what is mined or felled gathered;
        /// other building pieces built, breaking or not; what only breaks gathered; anything else
        /// with the rest.
        /// </summary>
        public static PartRole RoleOf(PartTraits part)
        {
            if (part.Container || part.Pickup) return PartRole.Loot;
            if (part.Spawns || part.Used) return PartRole.Other;
            if (part.Gathered) return PartRole.Gather;
            if (part.Built) return PartRole.Built;
            return part.Breaks ? PartRole.Gather : PartRole.Other;
        }

        /// <summary>A row's title on a location's page, or across a dungeon's or camp's rooms.</summary>
        public static string Title(PartRole role, bool rooms)
        {
            switch (role)
            {
                case PartRole.Loot: return rooms ? "Chests and pickups in its rooms" : "Chests and pickups";
                case PartRole.Gather: return rooms ? "To gather in its rooms" : "To gather";
                case PartRole.Built: return rooms ? "Its rooms are built of" : "Built of";
                default: return rooms ? "Its rooms hold" : "Holds";
            }
        }

        /// <summary>
        /// Which entry of a <c>RandomObject</c> a roll from 0 to 1 picks (<c>GetWeightedObject</c>):
        /// the first whose running weight reaches the roll's share of them all, or -1 for none.
        /// </summary>
        public static int Pick(IReadOnlyList<float> weights, double roll)
        {
            var total = 0f;
            foreach (var weight in weights) total += weight;
            var at = (float)(roll * total);
            var running = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                running += weights[i];
                if (at <= running) return i;
            }
            return -1;
        }

        /// <summary>Each entry's share of a <c>RandomObject</c>'s picks; all 0 when no entry weighs anything.</summary>
        public static float[] Shares(IReadOnlyList<float> weights)
        {
            var total = 0f;
            foreach (var weight in weights) total += weight;
            return weights.Select(w => total > 0f ? w / total : 0f).ToArray();
        }

        /// <summary>
        /// Whether a place can come out otherwise when rolled again: a <c>RandomSpawn</c> between
        /// never and always (chances in percent), or a <c>RandomObject</c> with two or more
        /// entries it can pick.
        /// </summary>
        public static bool LeftToChance(IEnumerable<float> spawnChances, IEnumerable<IReadOnlyList<float>> pickWeights)
        {
            return spawnChances.Any(c => c > 0f && c < 100f) || pickWeights.Any(w => w.Count(x => x > 0f) > 1);
        }

        /// <summary>
        /// What a dungeon's or camp's kinds of room hold, each thing with how many of them it is
        /// in (once for a room however often it is there), the most widespread first, then by
        /// name; a room not read yet adds nothing.
        /// </summary>
        public static List<PartInRooms> Across(IEnumerable<IReadOnlyList<PlacePart>> rooms)
        {
            var counted = new Dictionary<string, PartInRooms>(StringComparer.Ordinal);
            var found = new List<PartInRooms>();
            foreach (var room in rooms)
            {
                if (room == null) continue;
                foreach (var prefab in room.Where(p => p != null && !string.IsNullOrEmpty(p.Prefab)).Select(p => p.Prefab).Distinct())
                {
                    if (!counted.TryGetValue(prefab, out var part))
                    {
                        counted[prefab] = part = new PartInRooms { Prefab = prefab };
                        found.Add(part);
                    }
                    part.Rooms++;
                }
            }

            // The most widespread first, then by name; what is alike in both keeps the order found.
            var order = new int[found.Count];
            for (var i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var told = found[b].Rooms.CompareTo(found[a].Rooms);
                if (told == 0) told = StringComparer.OrdinalIgnoreCase.Compare(found[a].Prefab, found[b].Prefab);
                return told != 0 ? told : a.CompareTo(b);
            });
            var across = new List<PartInRooms>(found.Count);
            foreach (var i in order) across.Add(found[i]);
            return across;
        }

        /// <summary>
        /// The loot a dungeon's or camp's kinds of room give, counted as <see cref="Across"/>
        /// counts what they hold: each item with how many of them give it. What a kind of part
        /// gives (<paramref name="lootOf"/>, by its prefab) is asked once, however many rooms and
        /// lines name it.
        /// </summary>
        public static List<PartInRooms> LootAcross(IEnumerable<IReadOnlyList<PlacePart>> rooms, Func<string, IReadOnlyList<string>> lootOf)
        {
            var asked = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var given = new List<IReadOnlyList<PlacePart>>();
            foreach (var room in rooms)
            {
                if (room == null) continue;
                // An item given by several of the room's parts is one item there: Across counts a room once.
                var items = new List<PlacePart>();
                foreach (var part in room)
                {
                    if (part == null || string.IsNullOrEmpty(part.Prefab)) continue;
                    if (!asked.TryGetValue(part.Prefab, out var loot)) asked[part.Prefab] = loot = lootOf(part.Prefab);
                    foreach (var item in loot) items.Add(new PlacePart { Prefab = item, Count = 1, Chance = 1f });
                }
                given.Add(items);
            }
            return Across(given);
        }

        /// <summary>How widespread a thing is among a dungeon's kinds of room, as a chip's amount.</summary>
        public static string InRooms(int rooms) => "in " + Naming.Count(rooms, "kind of room", "kinds of room");

        /// <summary>The same for loot, with the best chance a chest or pickup there gives it.</summary>
        public static string InRooms(int rooms, double best) => best > 0.0 ? $"{InRooms(rooms)}, at best {DropWords.Share((float)best)}" : InRooms(rooms);

        /// <summary>How many and how likely, as a chip's amount: nothing for one that is always there.</summary>
        public static string Amount(int count, float chance)
        {
            var always = chance >= 0.9995f;
            if (always) return count > 1 ? Numbers.Count(count) : "";
            return count > 1 ? $"{Numbers.Count(count)}, {DropWords.Share(chance)} each" : DropWords.Share(chance);
        }
    }
}
