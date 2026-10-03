using System;

namespace Scry
{
    /// <summary>How something gives a thing.</summary>
    public enum SourceWay
    {
        /// <summary>A creature drops it on dying (<c>CharacterDrop</c>).</summary>
        Dropped,

        /// <summary>Picked from a bush or plant (<c>Pickable</c>).</summary>
        Picked,

        /// <summary>A sapling or seedling grows into it (<c>Plant</c>).</summary>
        GrowsFrom,

        /// <summary>An entry of a drop table: what a tree, rock, chest or anything broken gives.</summary>
        Table,

        /// <summary>A producer makes it over time (<c>Beehive</c>, <c>SapCollector</c>).</summary>
        Made,

        /// <summary>A trader sells it.</summary>
        Sold,

        /// <summary>Born to a tame creature (<c>Procreation</c>).</summary>
        Born,

        /// <summary>A young one grows up into it (<c>Growup</c>).</summary>
        GrowsUp,

        /// <summary>An egg hatches into it (<c>EggGrow</c>).</summary>
        Hatches,
    }

    /// <summary>What a drop table belongs to, which says how it gives what it holds.</summary>
    public enum TableOf
    {
        Tree,
        Log,
        Rock,
        Container,
        Pickable,
        Broken,

        /// <summary>A mod's own part, or anything else holding a drop table.</summary>
        Other,
    }

    /// <summary>
    /// One way a thing comes from something, as read from the game: what gives it, how, and the
    /// figures that way has. Told in words by <see cref="SourceWords"/>, which also says how sure
    /// it is to give the thing.
    /// </summary>
    public sealed class SourceFacts
    {
        public SourceWay Way;

        /// <summary>The prefab that gives it, which its line goes to.</summary>
        public string Giver = "";

        /// <summary>That prefab's name as the game shows it.</summary>
        public string Shown = "";

        /// <summary>A creature's drop: its amounts as set (the top one never rolled), whether one drops for each player, and its chance.</summary>
        public int Min = 1;
        public int Max = 1;
        public bool OnePerPlayer;
        public float Chance = 1f;

        /// <summary>A drop table's entry: what the table belongs to, how it is rolled, and the entry.</summary>
        public TableOf Table;
        public DropTableInfo Rolls;
        public DropInfo Drop;

        /// <summary>A producer's pace in seconds, how many it holds, and the biomes it works in, named; empty for any.</summary>
        public float Every;
        public int HoldsUpTo;
        public string In = "";

        /// <summary>A trader's ware: how many for its price, and the world key it waits for; empty for none.</summary>
        public int Stack = 1;
        public int Price;
        public string Key = "";

        /// <summary>Born to a tame creature with no partner near.</summary>
        public bool NoPartner;

        /// <summary>A way that has no figures of its own.</summary>
        public static SourceFacts Of(SourceWay way, string giver, string shown) => new SourceFacts { Way = way, Giver = giver ?? "", Shown = shown ?? "" };

        public static SourceFacts Dropped(string giver, string shown, int min, int max, bool onePerPlayer, float chance)
        {
            var facts = Of(SourceWay.Dropped, giver, shown);
            facts.Min = min;
            facts.Max = max;
            facts.OnePerPlayer = onePerPlayer;
            facts.Chance = chance;
            return facts;
        }

        public static SourceFacts FromTable(string giver, string shown, TableOf table, DropTableInfo rolls, DropInfo drop)
        {
            var facts = Of(SourceWay.Table, giver, shown);
            facts.Table = table;
            facts.Rolls = rolls;
            facts.Drop = drop;
            return facts;
        }

        public static SourceFacts Made(string giver, string shown, float every, int holdsUpTo, string biomes)
        {
            var facts = Of(SourceWay.Made, giver, shown);
            facts.Every = every;
            facts.HoldsUpTo = holdsUpTo;
            facts.In = biomes ?? "";
            return facts;
        }

        public static SourceFacts Sold(string giver, string shown, int stack, int price, string key)
        {
            var facts = Of(SourceWay.Sold, giver, shown);
            facts.Stack = stack;
            facts.Price = price;
            facts.Key = key ?? "";
            return facts;
        }

        public static SourceFacts BornAlone(string giver, string shown)
        {
            var facts = Of(SourceWay.Born, giver, shown);
            facts.NoPartner = true;
            return facts;
        }
    }

    /// <summary>
    /// A way a thing comes from something in words, the same on every page, and how sure it is to
    /// give the thing, for telling the surest first (<see cref="ContentOrder.SurestFirst{T}"/>).
    /// </summary>
    public static class SourceWords
    {
        /// <summary>The line telling it; a world key it waits for is named by the boss whose defeat sets it.</summary>
        public static string Line(SourceFacts source, Func<string, string> bossOf)
        {
            switch (source.Way)
            {
                case SourceWay.Dropped: return $"Dropped by {source.Shown}, {DropWords.CreatureDrop(source.Min, source.Max, source.OnePerPlayer, source.Chance)}";
                case SourceWay.Picked: return $"Picked from {source.Shown}";
                case SourceWay.GrowsFrom: return $"Grows from {source.Shown}";
                case SourceWay.Table: return $"{Verb(source.Table)} {source.Shown}, {DropWords.ForItem(source.Rolls, source.Drop)}";
                case SourceWay.Made:
                    return $"Made by {source.Shown}, {Pace(source.Every, source.HoldsUpTo)}" + (source.In.Length > 0 ? ", in " + source.In : "");
                case SourceWay.Sold:
                    return $"Sold by {source.Shown}, {Price(source.Stack, source.Price)}" + (source.Key.Length > 0 ? ", " + SpawnWords.Once(source.Key, bossOf) : "");
                case SourceWay.Born: return $"Born to a tame {source.Shown}" + (source.NoPartner ? " with no partner near" : "");
                case SourceWay.GrowsUp: return $"Grows up from {source.Shown}";
                default: return $"Hatches from {source.Shown}";
            }
        }

        /// <summary>A producer's pace: one every so long, holding up to so many.</summary>
        public static string Pace(float everySeconds, int holdsUpTo) => $"one every {Numbers.Duration(everySeconds)}, holding up to {Numbers.Count(holdsUpTo)}";

        /// <summary>A trader's price: so many coins, for a stack where it sells more than one.</summary>
        public static string Price(int stack, int price) => stack > 1 ? $"{Numbers.Count(stack)} for {Numbers.Count(price)} coins" : $"{Numbers.Count(price)} coins";

        /// <summary>How a drop table gives what it holds, by what it belongs to, as the other side's facts tell it.</summary>
        public static string Verb(TableOf table)
        {
            switch (table)
            {
                case TableOf.Tree: return "Felled from";
                case TableOf.Log: return "Chopped from";
                case TableOf.Rock: return "Mined from";
                case TableOf.Container: return "Found in";
                case TableOf.Pickable: return "Also picked from";
                case TableOf.Broken: return "Broken out of";
                default: return "Comes out of";
            }
        }

        /// <summary>
        /// How often one meeting gives the thing, from 0 to 1: a creature's drop at its chance
        /// (stars aside), a drop table's entry about as often as one opening gives it
        /// (<see cref="ContentOrder.AtLeastOnce"/>), every other way always.
        /// </summary>
        public static double Sureness(SourceFacts source)
        {
            switch (source.Way)
            {
                case SourceWay.Dropped: return Math.Max(0.0, Math.Min(1.0, source.Chance));
                case SourceWay.Table:
                    var weights = 0f;
                    foreach (var drop in source.Rolls.Drops) weights += drop.Weight;
                    var share = weights > 0f ? source.Drop.Weight / weights : 0.0;
                    return ContentOrder.AtLeastOnce(share, source.Rolls.Min, source.Rolls.Max, source.Rolls.Chance, source.Rolls.OneOfEach, source.Rolls.Drops.Count);
                default: return 1.0;
            }
        }
    }
}
