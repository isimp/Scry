using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Machines in words: what a ballista shoots (<c>Turret</c>, through
    /// <c>BaseAI.FindClosestCreature</c>), on whom a trap springs (<c>Trap</c>), how a ship fares
    /// in the Ashlands' seas and capsized (<c>Ship.TakeAshlandsDamage</c>, <c>m_upsideDownDmg</c>),
    /// what a cart weighs (<c>Vagon.UpdateMass</c>: its own weight and its load's at a factor), and
    /// what a catapult loads (<c>Catapult.CanItemBeLoaded</c>).
    /// </summary>
    public static class MachineWords
    {
        public static string Shoots(bool enemies, bool players, bool tamed, float range)
        {
            var who = new List<string>();
            if (enemies) who.Add("enemies");
            if (players) who.Add("players");
            if (tamed) who.Add("tame creatures");
            return who.Count == 0 ? "nothing" : $"{And(who)} within {Naming.Number(range)} m";
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
            damage > 0f ? $"takes {Naming.Number(damage)} damage every {Naming.Duration(interval)}" : null;

        public static string CartWeight(float baseMass, float factor)
        {
            var own = Naming.Number(baseMass);
            if (factor <= 0f) return $"{own}, whatever it carries";
            if (Math.Abs(factor - 1f) < 0.001f) return $"{own} empty, and all it carries";
            if (Math.Abs(factor - 0.5f) < 0.001f) return $"{own} empty, and half of what it carries";
            return $"{own} empty, and {(int)Math.Round(factor * 100f, MidpointRounding.AwayFromZero)}% of what it carries";
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
            parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }
}
