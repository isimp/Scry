using System;

namespace Scry
{
    /// <summary>What the character can be seen to do, or see done, that shows a number the game never does (<see cref="EntryKeys.Did"/>).</summary>
    internal enum Deed
    {
        /// <summary>Killed a creature: its health.</summary>
        Killed,

        /// <summary>Tamed a creature: how long taming and a meal take.</summary>
        Tamed,

        /// <summary>Saw a creature born: how long it is pregnant.</summary>
        Born,

        /// <summary>Saw a young creature grow up: how long that takes.</summary>
        GrewUp,

        /// <summary>Saw a pickable grow back: how long that takes.</summary>
        GrewBack,

        /// <summary>Broke a tree, log, rock or anything that breaks: its health.</summary>
        Broke,

        /// <summary>Saw a starred creature fall: what its stars change.</summary>
        StarredFell,

        /// <summary>Saw a spawner spawn: its pool, its star chance and its pace.</summary>
        Spawned,

        /// <summary>Collected from a producer: how often it makes what it makes.</summary>
        Collected,
    }

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

        /// <summary>A damage type, kept only as what a creature was seen hit with (<see cref="Damage"/>); no entry goes by it.</summary>
        private const string DamageType = "dmg:";

        /// <summary>The key a damage type is paired under with a creature the game told how it takes it (<see cref="HitTold"/>).</summary>
        public static string Damage(string type) => DamageType + type;

        /// <summary>Whether a key is a damage type's (<see cref="Damage"/>), and which.</summary>
        public static bool IsDamage(string key, out string type)
        {
            type = key != null && key.StartsWith(DamageType, StringComparison.Ordinal) ? key.Substring(DamageType.Length) : null;
            return type != null;
        }

        /// <summary>Something the character saw done, kept only as paired with what it was done to (<see cref="Did"/>); no entry goes by it.</summary>
        private const string Deeds = "did:";

        /// <summary>The key a deed is paired under with what it was seen done to: a creature killed, tamed, born or grown up, a pickable grown back.</summary>
        public static string Did(Deed deed) => Deeds + deed;

        /// <summary>Whether a key is a deed's (<see cref="Did"/>), and which.</summary>
        public static bool IsDeed(string key, out Deed deed)
        {
            deed = default;
            return key != null && key.StartsWith(Deeds, StringComparison.Ordinal) && Enum.TryParse(key.Substring(Deeds.Length), out deed);
        }

        /// <summary>The note of what a reading of the explored map brought (<see cref="CompendiumBook.NoteNews"/>); no entry goes by it.</summary>
        public const string OnMap = "map:explored";

        /// <summary>A creature's weak spot, kept only as paired with a creature a hit of the character found it on; no entry goes by it.</summary>
        private const string Spot = "spot:";

        /// <summary>The key a weak spot is paired under, by the name of the part it is on.</summary>
        public static string WeakSpot(string part) => Spot + part;

        /// <summary>Whether a key is a weak spot's (<see cref="WeakSpot"/>).</summary>
        public static bool IsWeakSpot(string key) => key != null && key.StartsWith(Spot, StringComparison.Ordinal);

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

        /// <summary>A table line's link that plays an attack on the stage, by the item it is made with: a creature's, or a weapon's first or second on the person wearing it.</summary>
        public static string PlayAttack(string item, bool secondary = false, int step = 0) =>
            AttackPlay + item + (secondary ? SecondAttack : "") + (step > 0 ? AttackStep + Stored.Count(step) : "");

        /// <summary>Whether a link plays an attack, by which item, and whether its second.</summary>
        public static bool PlaysAttack(string link, out string item, out bool secondary) => PlaysAttack(link, out item, out secondary, out _);

        /// <summary>Whether a link plays an attack, by which item, whether its second, and which step of its combo from 0.</summary>
        public static bool PlaysAttack(string link, out string item, out bool secondary, out int step)
        {
            item = link != null && link.StartsWith(AttackPlay, StringComparison.Ordinal) ? link.Substring(AttackPlay.Length) : null;
            step = 0;
            var at = item != null ? item.LastIndexOf(AttackStep, StringComparison.Ordinal) : -1;
            if (at >= 0 && Stored.TryCount(item.Substring(at + AttackStep.Length), out var counted) && counted > 0)
            {
                step = counted;
                item = item.Substring(0, at);
            }
            secondary = item != null && item.EndsWith(SecondAttack, StringComparison.Ordinal);
            if (secondary) item = item.Substring(0, item.Length - SecondAttack.Length);
            return item != null;
        }

        private const string AttackPlay = "play-attack:";
        private const string SecondAttack = "#2";
        private const string AttackStep = "#step";

        /// <summary>A chip's link that runs a search, such as for what to bring against a creature.</summary>
        public static string Search(string query) => RunSearch + query;

        /// <summary>The search a link runs; null for one that runs none.</summary>
        public static string SearchOf(string link) => link != null && link.StartsWith(RunSearch, StringComparison.Ordinal) ? link.Substring(RunSearch.Length) : null;

        private const string RunSearch = "search:";

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
