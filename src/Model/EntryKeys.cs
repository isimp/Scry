using System;

namespace Scry
{
    /// <summary>
    /// The key an entry is kept under in favourites, recent and links. A prefab goes by its name;
    /// what is no prefab has a namespace of its own, since it may share a name with one: a status
    /// effect "se:" and its name, a raid "raid:" and its name, a location or dungeon room "loc:"
    /// and its prefab's name, a mod "mod:" and its name, a biome "biome:" and its name.
    /// </summary>
    internal static class EntryKeys
    {
        public const string StatusEffect = "se:";
        public const string Raid = "raid:";
        public const string Location = "loc:";
        public const string Mod = "mod:";
        public const string Biome = "biome:";

        /// <summary>A value's link that plays the entry's own music rather than going to an entry.</summary>
        public const string PlayMusic = "play:music";

        /// <summary>A value's link that plays music by its name (a biome has one for each time of day).</summary>
        public static string PlayMusicNamed(string music) => PlayMusic + ":" + music;

        /// <summary>Whether a link plays music, and which by its name; null for the entry's own.</summary>
        public static bool PlaysMusic(string link, out string named)
        {
            named = null;
            if (link == null || !link.StartsWith(PlayMusic, StringComparison.Ordinal)) return false;
            if (link.Length > PlayMusic.Length + 1) named = link.Substring(PlayMusic.Length + 1);
            return true;
        }

        /// <summary>A table line's link that plays a creature's attack on the stage, by the item the attack is made with.</summary>
        public static string PlayAttack(string item) => AttackPlay + item;

        /// <summary>Whether a link plays an attack, and by which item.</summary>
        public static bool PlaysAttack(string link, out string item)
        {
            item = link != null && link.StartsWith(AttackPlay, StringComparison.Ordinal) ? link.Substring(AttackPlay.Length) : null;
            return item != null;
        }

        private const string AttackPlay = "play-attack:";

        /// <summary>A value's link that opens a website in the browser.</summary>
        public static string Website(string url) => Web + url;

        /// <summary>The address a website's link opens; null for a link that opens none.</summary>
        public static string WebsiteOf(string link) => link != null && link.StartsWith(Web, StringComparison.Ordinal) ? link.Substring(Web.Length) : null;

        private const string Web = "web:";

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
