using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a prefab's machines are, as read from the game; null for one it is not.</summary>
    internal sealed class MachineFacts
    {
        public TurretFacts Turret;
        public TrapFacts Trap;
        public ShipFacts Ship;
        public CartFacts Cart;
        public CatapultFacts Catapult;
    }

    /// <summary>A ballista (<c>Turret</c>): how many shots it holds, whom it shoots within how far, and how often.</summary>
    internal sealed class TurretFacts
    {
        public int MaxAmmo;
        public bool Enemies;
        public bool Players;
        public bool Tamed;
        public float Range;
        public float Cooldown;
    }

    /// <summary>A trap (<c>Trap</c>): on whom it springs, how soon it rearms, its hit's damage by type, whether it staggers.</summary>
    internal sealed class TrapFacts
    {
        public bool Enemies;
        public bool Players;
        public float Rearm;
        public (string Type, float Amount)[] Damage = Array.Empty<(string, float)>();
        public bool Staggers;
    }

    /// <summary>A ship (<c>Ship</c>): whether it is built for the Ashlands' seas, and what it takes upside down.</summary>
    internal sealed class ShipFacts
    {
        public bool AshlandsReady;
        public float CapsizedDamage;
        public float CapsizedEvery;
    }

    /// <summary>A cart (<c>Vagon</c>): its own weight, and the share of its load's that it adds.</summary>
    internal sealed class CartFacts
    {
        public float Mass;
        public float LoadShare;
    }

    /// <summary>A catapult (<c>Catapult</c>): which item types it loads or leaves, how many at a time, and whether it throws whoever stands where it is loaded.</summary>
    internal sealed class CatapultFacts
    {
        public bool ListExcludes;
        public string[] Types = Array.Empty<string>();
        public bool OnlyHeld;
        public int MaxLoad;
        public bool ThrowsWhoStandsThere;
    }

    /// <summary>
    /// Machines in words: what a ballista shoots (<c>Turret</c>, through
    /// <c>BaseAI.FindClosestCreature</c>), on whom a trap springs (<c>Trap</c>), how a ship fares
    /// in the Ashlands' seas and capsized (<c>Ship.TakeAshlandsDamage</c>, <c>m_upsideDownDmg</c>),
    /// what a cart weighs (<c>Vagon.UpdateMass</c>: its own weight and its load's at a factor), and
    /// what a catapult loads (<c>Catapult.CanItemBeLoaded</c>).
    /// </summary>
    internal static class MachineWords
    {
        /// <summary>A prefab's machines, line by line in the order they are read: a ballista's, a trap's, a ship's, a cart's, a catapult's.</summary>
        public static List<FactPair> Pairs(MachineFacts f)
        {
            var pairs = new List<FactPair>();
            void Add(string label, string value)
            {
                if (!string.IsNullOrEmpty(value)) pairs.Add(new FactPair(label, value));
            }

            if (f.Turret != null)
            {
                Add("Holds", $"{Numbers.Count(f.Turret.MaxAmmo)} shots");
                Add("Shoots", Shoots(f.Turret.Enemies, f.Turret.Players, f.Turret.Tamed, f.Turret.Range));
                Add("Shoots every", Numbers.Duration(f.Turret.Cooldown));
            }
            if (f.Trap != null)
            {
                Add("Springs on", Springs(f.Trap.Enemies, f.Trap.Players));
                Add("Rearms after", Numbers.Duration(f.Trap.Rearm));
                Add("Damage", CombatWords.Damage(f.Trap.Damage));
                if (f.Trap.Staggers) Add("Staggers", "whoever it hits");
            }
            if (f.Ship != null)
            {
                Add("Ashlands seas", Ashlands(f.Ship.AshlandsReady));
                Add("Capsized", Capsized(f.Ship.CapsizedDamage, f.Ship.CapsizedEvery));
            }
            if (f.Cart != null) Add("Weighs", CartWeight(f.Cart.Mass, f.Cart.LoadShare));
            if (f.Catapult != null)
            {
                Add("Loads", Loads(f.Catapult.ListExcludes, f.Catapult.Types, f.Catapult.OnlyHeld));
                if (f.Catapult.MaxLoad > 1) Add("At a time", $"up to {Numbers.Count(f.Catapult.MaxLoad)}");
                if (f.Catapult.ThrowsWhoStandsThere) Add("Also throws", "whoever stands where it is loaded");
            }
            return pairs;
        }

        /// <summary>The title of a ballista's row of trophies (<c>Turret.UseItem</c>, <c>UpdateTarget</c>).</summary>
        public static string TrophiesTitle(int most) => $"Given these trophies, up to {Numbers.Count(most)} at once, it shoots only their creatures";

        public static string Shoots(bool enemies, bool players, bool tamed, float range)
        {
            var who = new List<string>();
            if (enemies) who.Add("enemies");
            if (players) who.Add("players");
            if (tamed) who.Add("tame creatures");
            return who.Count == 0 ? "nothing" : $"{And(who)} within {Numbers.Amount(range)} m";
        }

        public static string Springs(bool enemies, bool players)
        {
            var who = new List<string>();
            if (enemies) who.Add("enemies");
            if (players) who.Add("players");
            return who.Count == 0 ? "nothing" : And(who);
        }

        public static string Ashlands(bool ready) => ready ? "sails them unharmed" : "their boiling water burns it";

        /// <summary>What it takes while upside down, or null when nothing.</summary>
        public static string Capsized(float damage, float interval) =>
            damage > 0f ? $"takes {Numbers.Amount(damage)} damage every {Numbers.Duration(interval)}" : null;

        public static string CartWeight(float baseMass, float factor)
        {
            var own = Numbers.Amount(baseMass);
            if (factor <= 0f) return $"{own}, whatever it carries";
            if (Math.Abs(factor - 1f) < 0.001f) return $"{own} empty, and all it carries";
            if (Math.Abs(factor - 0.5f) < 0.001f) return $"{own} empty, and half of what it carries";
            return $"{own} empty, and {Numbers.Count((int)Math.Round(factor * 100f, MidpointRounding.AwayFromZero))}% of what it carries";
        }

        /// <summary>What a catapult loads: anything (it can hold) but the types listed, or only those.</summary>
        public static string Loads(bool listExcludes, string[] types, bool onlyHeld)
        {
            var any = onlyHeld ? "anything it can hold" : "anything";
            if (types == null || types.Length == 0) return listExcludes ? any : "nothing";
            var list = And(new List<string>(types));
            return listExcludes ? $"{any} but {list}" : $"only {list}";
        }

        private static string And(List<string> parts) =>
            Naming.Joined(parts);
    }
}
