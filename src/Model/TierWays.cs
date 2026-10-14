using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// One way to make a thing: a recipe, a piece's cost, or one thing a station turns into
    /// another. What it makes, where (a station's prefab; none or "hand" for anywhere), and how
    /// many of each ingredient making it once takes.
    /// </summary>
    internal sealed class MakingWay
    {
        public MakingWay(string target, string place, params (string Name, int Amount)[] needs)
        {
            Target = target;
            Place = place;
            Needs = needs ?? Array.Empty<(string, int)>();
        }

        public string Target { get; }

        public string Place { get; }

        public IReadOnlyList<(string Name, int Amount)> Needs { get; }
    }

    /// <summary>One way a raid comes to be ready: the keys it waits for and the items it waits to be known.</summary>
    internal sealed class RaidReady
    {
        public RaidReady(string[] keys, string[] items)
        {
            Keys = keys ?? Array.Empty<string>();
            Items = items ?? Array.Empty<string>();
        }

        public string[] Keys { get; }

        public string[] Items { get; }
    }

    /// <summary>
    /// The ways things are made and raids come, as the milestone tiers count them
    /// (<see cref="TierPass"/>). Each recipe, piece cost and conversion is a way of its own, so
    /// two recipes for a thing or two ores a smelter takes are alternatives, never both needed;
    /// a raid comes in its earliest biome, no sooner than the bosses' fall its keys mark and the
    /// items it waits to be known.
    /// </summary>
    internal static class TierWays
    {
        /// <summary>
        /// Adds a way to make a thing: every ingredient making it once takes (one asked for in
        /// no amount is only asked for by an upgrade), and its station where that is a thing of
        /// its own. What is no thing of its own (<paramref name="keyOf"/> gives none) is left out.
        /// </summary>
        public static void Making(TierPass pass, MakingWay way, Func<string, string> keyOf)
        {
            var target = keyOf(way.Target);
            if (target == null) return;
            var needs = new List<string>();
            foreach (var (name, amount) in way.Needs)
            {
                if (amount <= 0) continue;
                Need(needs, keyOf(name), target);
            }
            if (way.Place != null) Need(needs, keyOf(way.Place), target);
            pass.Add(target, 0, needs.ToArray());
        }

        /// <summary>The ways a station's making is a way to make its output: one for each item where any one will do, else one taking them all.</summary>
        public static List<MakingWay> Of(Making making)
        {
            var ways = new List<MakingWay>();
            if (making.AnyOne)
            {
                foreach (var input in making.Inputs) ways.Add(new MakingWay(making.Output, making.Station, input));
            }
            else
            {
                ways.Add(new MakingWay(making.Output, making.Station, making.Inputs.ToArray()));
            }
            return ways;
        }

        private static void Need(List<string> needs, string key, string target)
        {
            if (key != null && key != target && !needs.Contains(key)) needs.Add(key);
        }

        /// <summary>
        /// The ways a raid becomes ready, as the game checks it (<c>RandEventSystem.PlayerIsReadyForEvent</c>):
        /// one started by the world's keys waits for all of them; one by each player's progress
        /// comes once any item it names is known or any of its keys is had, and only a raid naming
        /// neither waits for every key of its last list.
        /// </summary>
        public static List<RaidReady> Ready(bool perPlayer, IEnumerable<string> worldKeys, IEnumerable<string> knownItems, IEnumerable<string> anyKeys, IEnumerable<string> allKeys)
        {
            var ready = new List<RaidReady>();
            if (!perPlayer)
            {
                ready.Add(new RaidReady(new List<string>(worldKeys ?? Array.Empty<string>()).ToArray(), null));
                return ready;
            }
            foreach (var item in knownItems ?? Array.Empty<string>()) ready.Add(new RaidReady(null, new[] { item }));
            foreach (var key in anyKeys ?? Array.Empty<string>()) ready.Add(new RaidReady(new[] { key }, null));
            if (ready.Count == 0) ready.Add(new RaidReady(new List<string>(allKeys ?? Array.Empty<string>()).ToArray(), null));
            return ready;
        }

        /// <summary>
        /// Adds the ways a raid comes: in each biome it comes to, once a way it becomes ready is
        /// open: the tier of each boss whose fall a key marks (a key marking none adds nothing),
        /// and the items it waits to be known.
        /// </summary>
        public static void Raid(TierPass pass, string raid, IEnumerable<int> biomeTiers, IEnumerable<RaidReady> ready, IReadOnlyDictionary<string, int> keyTiers, Func<string, string> keyOf)
        {
            var ways = new List<RaidReady>(ready);
            foreach (var biome in biomeTiers)
            {
                foreach (var way in ways)
                {
                    var tier = biome;
                    foreach (var key in way.Keys)
                    {
                        if (key != null && keyTiers.TryGetValue(key, out var marked) && marked > tier) tier = marked;
                    }
                    var needs = new List<string>();
                    foreach (var item in way.Items) Need(needs, keyOf(item), raid);
                    pass.Add(raid, tier, needs.ToArray());
                }
            }
        }
    }
}
