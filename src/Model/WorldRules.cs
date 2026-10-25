using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The compendium's rules as a world's server sets them: left to each player, or one set for everyone.</summary>
    internal enum WorldRule
    {
        /// <summary>Each player chooses their own.</summary>
        PlayersChoose,

        /// <summary>The compendium off for everyone.</summary>
        Off,

        /// <summary>The loose rules for everyone.</summary>
        Loose,

        /// <summary>The strict rules for everyone.</summary>
        Strict,
    }

    /// <summary>
    /// What the world's server allows the compendium, from its config synced to every player
    /// (ServerSync): the rules everyone plays by, none leaving them to each player; whether
    /// peeking and *Known up to* are allowed; and the highest level each source may show, Off
    /// among them. The book keeps the player's own choices beneath it (<see cref="CompendiumBook.Under"/>).
    /// </summary>
    internal sealed class WorldRules
    {
        /// <summary>A world that sets nothing: every choice the player's.</summary>
        public static WorldRules Open => new WorldRules();

        /// <summary>The rules from the server's settings: its rules or the players' choice, peeking and *Known up to* allowed or not, and each source's highest level.</summary>
        public static WorldRules From(WorldRule rule, bool peeking, bool floor, Func<WorldSource, SourceLevel> upTo, float seenReach = SeenReachSetting.Default)
        {
            var rules = new WorldRules
            {
                Rule = rule == WorldRule.Off ? CompendiumRule.Off : rule == WorldRule.Loose ? CompendiumRule.Loose : rule == WorldRule.Strict ? CompendiumRule.Strict : (CompendiumRule?)null,
                Peeking = peeking,
                FloorAllowed = floor,
                SeenReach = Math.Max(0f, seenReach),
            };
            foreach (var source in WorldSources.All) rules.UpTo(source, upTo(source));
            return rules;
        }

        /// <summary>The rules everyone in the world plays by; none leaves them to each player.</summary>
        public CompendiumRule? Rule { get; set; }

        /// <summary>Whether peeking is allowed.</summary>
        public bool Peeking { get; set; } = true;

        /// <summary>Whether *Known up to* is allowed.</summary>
        public bool FloorAllowed { get; set; } = true;

        /// <summary>
        /// How near, in metres, a creature in view is seen up close, its stars and what it carries
        /// kept with the book (<see cref="CompendiumBook.Sighted"/>): as far as the game names a
        /// creature and marks its stars over it, 10 m, unless the server sets otherwise; 0 never.
        /// </summary>
        public float SeenReach { get; set; } = SeenReachSetting.Default;

        /// <summary>What the seen reach starts at: the game's own, its enemy hud's 10 m.</summary>
        public static class SeenReachSetting
        {
            public const float Default = 10f;
        }

        private readonly Dictionary<WorldSource, SourceLevel> _upTo = new Dictionary<WorldSource, SourceLevel>();

        /// <summary>Whether the world sets the rules.</summary>
        public bool RulesSet => Rule != null;

        /// <summary>Sets the highest level a source may show.</summary>
        public void UpTo(WorldSource source, SourceLevel level) => _upTo[source] = level;

        /// <summary>The highest level a source may show: the highest it offers within the world's cap, never below its least.</summary>
        public SourceLevel Highest(WorldSource source)
        {
            var offered = WorldSources.Offered(source);
            var cap = _upTo.TryGetValue(source, out var set) ? set : offered[offered.Length - 1];
            // The first level offered is the least the source shows, whatever the cap.
            var highest = offered[0];
            foreach (var level in offered) if (level <= cap) highest = level;
            return highest;
        }

        /// <summary>Whether a source may show a level.</summary>
        public bool Allows(WorldSource source, SourceLevel level) => level <= Highest(source);

        /// <summary>What a source shows of the level chosen for it: the choice, or the highest the world allows beneath it.</summary>
        public SourceLevel Capped(WorldSource source, SourceLevel chosen)
        {
            var highest = Highest(source);
            return chosen <= highest ? chosen : highest;
        }

        /// <summary>Whether other rules allow the same in every way.</summary>
        public bool SameAs(WorldRules other)
        {
            if (other == null || other.Rule != Rule || other.Peeking != Peeking || other.FloorAllowed != FloorAllowed || !other.SeenReach.Equals(SeenReach)) return false;
            foreach (var source in WorldSources.All) if (Highest(source) != other.Highest(source)) return false;
            return true;
        }

        /// <summary>A copy, unchanged by what is set on this one later.</summary>
        public WorldRules Copy()
        {
            var copy = new WorldRules { Rule = Rule, Peeking = Peeking, FloorAllowed = FloorAllowed, SeenReach = SeenReach };
            foreach (var pair in _upTo) copy._upTo[pair.Key] = pair.Value;
            return copy;
        }
    }

    /// <summary>What the compendium's box says of what the world's server sets.</summary>
    internal static class WorldRulesWords
    {
        /// <summary>
        /// The line told once on joining a world whose server sets any of the compendium's rules:
        /// the rules it plays by, what it keeps off, and whether it limits what its world shows as
        /// known; none where it sets nothing.
        /// </summary>
        public static string Notice(WorldRules rules)
        {
            var off = new List<string>();
            if (!rules.Peeking) off.Add("peeking");
            if (!rules.FloorAllowed) off.Add(CompendiumWords.FloorCaption);
            var limits = false;
            foreach (var source in WorldSources.All)
            {
                var offered = WorldSources.Offered(source);
                if (rules.Highest(source) < offered[offered.Length - 1]) limits = true;
            }
            var reach = !rules.SeenReach.Equals(WorldRules.SeenReachSetting.Default);
            if (!rules.RulesSet && off.Count == 0 && !limits && !reach) return null;
            var said = rules.RulesSet ? "This server plays " + CompendiumWords.RuleName(rules.Rule.Value) : "This server";
            if (off.Count > 0) said += rules.RulesSet ? "; " + Naming.Joined(off) + (off.Count == 1 ? " is off" : " are off") : " keeps " + Naming.Joined(off) + " off";
            if (limits) said += off.Count > 0 ? (rules.RulesSet ? ", and it limits" : ", and limits") : rules.RulesSet ? "; it limits" : " limits";
            if (limits) said += " what its world shows as known";
            // The reach where the server sets it apart from the game's 10 m: at 0 nothing is seen up close.
            if (reach) said += (rules.RulesSet || off.Count > 0 || limits ? "; " : " ") + (rules.SeenReach <= 0f ? "it keeps no stars, gear or attacks seen up close" : $"it sees up close within {Numbers.Count((long)Math.Round(rules.SeenReach))} m");
            return said + ".";
        }

        /// <summary>The notice's button, opening the compendium's guide.</summary>
        public const string ShowRules = "Show the rules";

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutAway = "Put this away until the server's rules change; the box keeps showing them locked";

        /// <summary>The tip of the rules the server sets.</summary>
        public static string RulesSet(CompendiumRule rule) => $"This server sets the rules: {CompendiumWords.RuleName(rule)}. An admin changes it in the server's Scry config.";

        /// <summary>The tip of peeking where the server allows none.</summary>
        public const string NoPeeking = "This server allows no peeking.";

        /// <summary>The tip of *Known up to* where the server keeps it off.</summary>
        public const string NoFloor = "This server keeps Known up to off.";

        /// <summary>The description of a source's setting of the highest level it may show, with the levels it takes.</summary>
        public static string UpToSetting(WorldSource source)
        {
            var levels = Array.ConvertAll(WorldSources.Offered(source), l => l.ToString());
            var taken = string.Join(", ", levels, 0, levels.Length - 1) + " or " + levels[levels.Length - 1];
            return $"The highest level {CompendiumWords.SourceName(source)} may show in the compendium's box; one chosen above it shows this one. {taken}.";
        }

        /// <summary>The tip of a level above what the server allows a source.</summary>
        public static string UpTo(WorldSource source, SourceLevel highest) => $"This server allows {CompendiumWords.SourceName(source)} up to {CompendiumWords.LevelName(highest)}.";
    }
}
