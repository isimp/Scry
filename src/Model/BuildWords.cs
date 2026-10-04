using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A piece in words: what holds it up and what wears it down, where it may be placed and
    /// what it stands near or apart from, the comfort it gives, the station it upgrades, and what
    /// building it costs and with which tool. Support follows <c>WearNTear.UpdateSupport</c>: a
    /// piece on the ground has its material's full support, and one resting on another has that
    /// one's support less a share for each metre between their centres, a bigger share sideways
    /// than up, and falls below its material's least. Weather follows <c>WearNTear.UpdateWear</c>.
    /// </summary>
    internal static class BuildWords
    {
        /// <summary>
        /// A piece's comfort group, or that it has none (<c>SE_Rested.CalculateComfortLevel</c>
        /// counts, within 10 m, only the best of each group and a piece of the same name once).
        /// </summary>
        public static string ComfortGroup(string group) =>
            group != null ? $"{group}: only the best of these within 10 m counts" : "none: a second one within 10 m adds nothing";

        /// <summary>The station an upgrade upgrades, how near it must stand and how far from its other upgrades.</summary>
        public static string Upgrades(string station, float within, float apart) =>
            $"{station}, within {Numbers.Metres(within)} of it" + (apart > 0f ? $", {Numbers.Metres(apart)} from its other upgrades" : "");

        /// <summary>The title of what building it costs, with the station it is built near where it needs one.</summary>
        public static string Cost(string station) => string.IsNullOrEmpty(station) ? "Build cost" : "Built near " + station;

        /// <summary>The mod one of the game's own prefabs is made buildable through.</summary>
        public static string Through(string mod) => "through " + mod;

        /// <summary>The title of what building it through a mod costs, with the station it is built near where it needs one.</summary>
        public static string CostThrough(string mod, string station) =>
            string.IsNullOrEmpty(station) ? $"Built through {mod} with" : $"Built through {mod} near {station}";

        /// <summary>The build tool a piece is built with, and the tab it is on there.</summary>
        public static string OnTab(string tool, string tab) => $"{tool}, on its {tab} tab";

        /// <summary>The title of a build tool's tab, with how many pieces are on it.</summary>
        public static string BuildsOnTab(string tab, int pieces) => $"Builds on its {tab} tab ({Numbers.Count(pieces)})";

        /// <summary>How many of an ingredient it needs, and how many more each quality, where it goes up.</summary>
        public static string Needs(int amount, int perQuality) =>
            Numbers.Count(amount) + (perQuality > 0 ? $", +{Numbers.Count(perQuality)} per quality" : "");

        /// <summary>The support it has on the ground and the least it stands with; null for a material without figures.</summary>
        public static string Support(float max, float min, bool needsSupport)
        {
            if (max <= 0f) return null;
            return $"{Numbers.Amount(max)} on the ground, " + (needsSupport ? $"falls below {Numbers.Amount(min)}" : "stands without it");
        }

        /// <summary>The share of support lost for each metre sideways and up; null when none is lost.</summary>
        public static string SupportLoss(float sideways, float up)
        {
            if (sideways <= 0f && up <= 0f) return null;
            if (Numbers.Percent(sideways, 1) == Numbers.Percent(up, 1)) return $"{Numbers.Percent(sideways, 1)} a metre either way";
            var parts = new List<string>();
            if (sideways > 0f) parts.Add($"{Numbers.Percent(sideways, 1)} a metre sideways");
            if (up > 0f) parts.Add($"{Numbers.Percent(up, 1)} a metre up");
            return string.Join(", ", parts);
        }

        /// <summary>Whether rain wears it: every minute it is wet without a roof, 5% of its health, while more than half is left.</summary>
        public static string Rain(bool wears)
        {
            return wears ? "5% of its health a minute while wet and unroofed, until half is left" : "does not wear it";
        }

        /// <summary>
        /// How it stands up to the Ashlands, told only when it does better than most pieces: one
        /// immune takes no harm from ash or lava; one that resists stops wearing from ash at a
        /// tenth of its health and takes a third of the lava's harm.
        /// </summary>
        public static string Ash(bool immune, bool resists)
        {
            if (immune) return "unharmed by ash and lava";
            return resists ? "ash wears it only down to a tenth, lava a third as hard" : null;
        }

        /// <summary>Heavy snow in the Deep North, told only for a piece it does not harm.</summary>
        public static string Snow(bool immune) => immune ? "does not harm it" : null;

        /// <summary>
        /// Where a piece may be placed, as <c>Player.UpdatePlacementGhost</c> refuses it, in the
        /// order the game checks; null for a piece placed anywhere.
        /// </summary>
        public static string Placement(PlacementRules rules)
        {
            var lines = new List<string>();
            if (rules.GroundOnly) lines.Add("on the ground only");
            if (rules.CultivatedOnly) lines.Add("on cultivated ground only");
            if (rules.DirtOnly) lines.Add("on dirt only");
            if (rules.OnWater) lines.Add("on water only");
            if (rules.NotInWater) lines.Add("not in water");
            if (rules.NotOnWood) lines.Add("not on wood");
            if (rules.Level) lines.Add("not on a slope");
            if (rules.CeilingOnly) lines.Add("under a ceiling only");
            if (rules.NotOnFloor) lines.Add("not on a floor");
            if (rules.TeleportArea) lines.Add("in a teleport area only");
            if (rules.DeepSnowOnly) lines.Add("in the Deep North's deep snow only");
            if (rules.InDungeons) lines.Add("in dungeons too");
            return lines.Count > 0 ? string.Join(", ", lines) : null;
        }

        /// <summary>
        /// The title over the pieces one is kept apart from (<c>Piece.m_blockingPieces</c>), each a
        /// chip; none without a radius, which the game keeps them apart by.
        /// </summary>
        public static string KeptApart(float radius) => radius > 0f ? $"Not within {Numbers.Amount(radius)} m of" : null;

        /// <summary>The label over the piece one must stand near (<c>Piece.m_mustConnectTo</c>), or on top of.</summary>
        public static string Near(float radius, bool above) =>
            above ? $"On top of, within {Numbers.Amount(radius)} m" : $"Within {Numbers.Amount(radius)} m of";

        /// <summary>How far from a station its pieces may be built (<c>CraftingStation.m_rangeBuild</c>), more with each upgrade.</summary>
        public static string Range(float range, float perUpgrade) =>
            perUpgrade > 0f ? $"{Numbers.Amount(range)} m, {Numbers.Amount(perUpgrade)} m more for each upgrade" : $"{Numbers.Amount(range)} m";

        /// <summary>What claiming a bed does and needs (<c>Bed.Interact</c>, <c>CheckExposure</c>).</summary>
        public static string BedClaim(float cover) => $"where you come back to after dying; it needs a roof and at least {Numbers.Percent(cover)} cover";

        /// <summary>What sleeping in one's own bed needs (<c>Bed.Interact</c>: night, <c>CheckEnemies</c>, <c>CheckExposure</c>, <c>CheckFire</c>, <c>CheckWet</c>).</summary>
        public static string BedSleep(float cover) => $"at night, with no enemy sensing you, under a roof with at least {Numbers.Percent(cover)} cover, by a fire, and dry";
    }
}

namespace Scry
{
    internal sealed class PlacementRules
    {
        public bool GroundOnly, CultivatedOnly, DirtOnly, OnWater, NotInWater, NotOnWood, Level, CeilingOnly, NotOnFloor, TeleportArea, InDungeons, DeepSnowOnly;
    }

    /// <summary>
    /// What an area does (<c>EffectArea.Type</c>), each kind by where the game reads it: warmth
    /// for resting and beds (<c>Character.OnNearFire</c>, <c>Bed.CheckFire</c>), fire that
    /// fire-shy creatures avoid (<c>BaseAI.AvoidFire</c>), a base that keeps spawning out and
    /// counts toward raids (<c>SpawnSystem.IsSpawnPointGood</c>, <c>CreatureSpawner</c>,
    /// <c>RandEventSystem.CheckBase</c>), flames a cooking station needs, a teleport area some
    /// pieces need, ground untamed creatures keep out of (<c>MonsterAI</c>), and warm, cozy
    /// ground the cold does not reach (<c>Player.UpdateEnvStatusEffects</c>). A private area
    /// the game reads nowhere is not told.
    /// </summary>
    internal static class AreaWords
    {
        public static List<(string Label, string Text)> Lines(int type, float radius)
        {
            var lines = new List<(string, string)>();
            var known = radius > 0f;
            var r = Numbers.Amount(radius);
            var within = known ? $"within {r} m" : "near it";
            if ((type & 0x01) != 0) lines.Add(("Warmth", $"{within} you are by a fire, which resting and sleeping need"));
            if ((type & 0x02) != 0) lines.Add(("Fire", known ? $"creatures afraid of fire keep away from within {r} m" : "creatures afraid of fire keep away from it"));
            if ((type & 0x04) != 0) lines.Add(("A base", $"{within} the world's spawning and creature spawners place nothing, unless set to; three such near you let raids that come for bases come"));
            if ((type & 0x08) != 0) lines.Add(("Flames", "a cooking station needing a fire cooks over it"));
            if ((type & 0x10) != 0) lines.Add(("Teleport area", known ? $"pieces built only in one can be built within {r} m" : "pieces built only in one can be built near it"));
            if ((type & 0x20) != 0) lines.Add(("No monsters", known ? $"untamed creatures turn away from 15 m out and will not chase you into its {r} m" : "untamed creatures turn away from 15 m out and will not chase you into it"));
            if ((type & 0x40) != 0) lines.Add(("Warm and cozy", $"{within} cold and freezing do not reach you, and you rest even wet"));
            return lines;
        }
    }
}
