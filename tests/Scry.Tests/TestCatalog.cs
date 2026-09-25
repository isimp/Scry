using System;
using System.Collections.Generic;
using System.IO;

namespace Scry.Tests
{
    /// <summary>A small catalog shaped like the game's, and a throwaway folder for files.</summary>
    internal static class TestCatalog
    {
        public static Entry E(string name, Kind kind, string display = "", Origin origin = Origin.Vanilla)
        {
            return new Entry { Name = name, DisplayName = display, Kind = kind, Origin = origin };
        }

        public static List<Entry> Game()
        {
            return new List<Entry>
            {
                E("Troll", Kind.Creature, "Troll"),
                E("TrollArmorChest", Kind.Item, "Troll leather tunic"),
                E("Troll_Summoned", Kind.Creature, "Troll"),
                E("vfx_troll_death", Kind.Effect),
                E("sfx_troll_idle", Kind.Sound),
                E("MountainTroll", Kind.Creature, "Mountain troll"),
                E("Bow", Kind.Item, "Crude bow"),
                E("wood_wall", Kind.Piece, "Wood wall"),
                E("Rested", Kind.StatusEffect, "Rested"),
                E("arrow_wood_projectile", Kind.Projectile),
                E("Rock_3", Kind.Other),
                E("CoolMod_TrollStatue", Kind.Piece, "Troll statue", Origin.Mod),
            };
        }

        public static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "scry-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
