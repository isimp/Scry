using System;

namespace Scry
{
    /// <summary>
    /// The key an entry is kept under in favourites, recent and links. A prefab goes by its name;
    /// what is no prefab has a namespace of its own, since it may share a name with one: a status
    /// effect "se:" and its name, a raid "raid:" and its name, a location or dungeon room "loc:"
    /// and its prefab's name, a mod "mod:" and its name, a biome "biome:" and its name.
    /// </summary>
    public static class EntryKeys
    {
        public const string StatusEffect = "se:";
        public const string Raid = "raid:";
        public const string Location = "loc:";
        public const string Mod = "mod:";
        public const string Biome = "biome:";

        /// <summary>The key of an entry of this kind and name.</summary>
        public static string For(Kind kind, string name)
        {
            switch (kind)
            {
                case Kind.StatusEffect: return StatusEffect + name;
                case Kind.Raid: return Raid + name;
                case Kind.Location: return Location + name;
                case Kind.Mod: return Mod + name;
                case Kind.Biome: return Biome + name;
                default: return name;
            }
        }

        /// <summary>
        /// The name a key names, and the kind when its namespace tells it; a prefab's key tells no
        /// kind, since every kind of prefab shares one namespace.
        /// </summary>
        public static string Split(string key, out Kind? kind)
        {
            key = key ?? "";
            if (key.StartsWith(StatusEffect, StringComparison.Ordinal))
            {
                kind = Kind.StatusEffect;
                return key.Substring(StatusEffect.Length);
            }
            if (key.StartsWith(Raid, StringComparison.Ordinal))
            {
                kind = Kind.Raid;
                return key.Substring(Raid.Length);
            }
            if (key.StartsWith(Location, StringComparison.Ordinal))
            {
                kind = Kind.Location;
                return key.Substring(Location.Length);
            }
            if (key.StartsWith(Mod, StringComparison.Ordinal))
            {
                kind = Kind.Mod;
                return key.Substring(Mod.Length);
            }
            if (key.StartsWith(Biome, StringComparison.Ordinal))
            {
                kind = Kind.Biome;
                return key.Substring(Biome.Length);
            }
            kind = null;
            return key;
        }

        /// <summary>Whether entries of this kind have a namespace of their own rather than the prefabs'.</summary>
        public static bool HasOwnNamespace(Kind kind) => kind == Kind.StatusEffect || kind == Kind.Raid || kind == Kind.Location || kind == Kind.Mod || kind == Kind.Biome;
    }
}
