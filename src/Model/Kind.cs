using System;
using System.Linq;

namespace Scry
{
    /// <summary>What a prefab is, as far as previewing it goes.</summary>
    internal enum Kind
    {
        Creature,
        Item,
        Piece,
        Resource,
        Projectile,
        Effect,
        Sound,
        StatusEffect,

        /// <summary>A biome of the world, no prefab: its weathers, music, and what lives, grows and stands there.</summary>
        Biome,

        /// <summary>A place the world generator puts in the world, or a room a dungeon is built of; no prefab of the scene.</summary>
        Location,

        /// <summary>A raid (the game's random event), no prefab: what it brings, when and for whom.</summary>
        Raid,

        /// <summary>A mod loaded, no prefab: what it adds and which of the game's rules it hooks into.</summary>
        Mod,
        Other,
    }

    /// <summary>How a resource is gathered, the groups the Resources tab lists them in, in this order.</summary>
    internal enum ResourceGroup
    {
        None,
        Trees,
        Logs,
        RocksAndOre,
        Plants,
        BushesAndPickables,
        Other,
    }

    /// <summary>Where a prefab comes from.</summary>
    internal enum Origin
    {
        Unknown,
        Vanilla,
        Mod,
    }

    /// <summary>
    /// The facts about a prefab that decide its kind, read from its components in the game.
    /// Kept apart from the components themselves so the rule can be checked without the game.
    /// </summary>
    internal sealed class PrefabTraits
    {
        public bool IsStatusEffect;
        public bool HasCharacter;
        public bool HasItemDrop;
        public bool HasPiece;
        public bool HasProjectile;
        public bool HasRenderer;
        public bool HasParticles;
        public bool HasLight;
        public bool HasAudio;
        public bool HasSolidCollider;

        /// <summary>Chopped, mined, picked or grown: a tree, a log, a rock or ore vein, a bush, a plant.</summary>
        public bool HasResource;

        /// <summary>Grows from being planted: a crop or sapling put in the ground with the cultivator.</summary>
        public bool HasPlant;

        /// <summary>What kind of resource it is: a standing tree, a fallen log, a rock or vein mined a piece at a time, or something picked.</summary>
        public bool IsTree;
        public bool IsLog;
        public bool IsMined;
        public bool IsPicked;

        /// <summary>Breaks when hit, and whether it then drops anything.</summary>
        public bool HasDestructible;
        public bool HasDrops;

        /// <summary>Turns, when broken, into something mined or chopped (a silver vein into the vein itself).</summary>
        public bool BreaksIntoResource;

        /// <summary>Spawns creatures, as a nest does.</summary>
        public bool HasSpawner;

        /// <summary>Summons a boss when offered to (<c>OfferingBowl</c>).</summary>
        public bool IsAltar;

        /// <summary>What a creature leaves when it dies, falling as a body (<c>Ragdoll</c>).</summary>
        public bool HasRagdoll;

        /// <summary>Holds items (<c>Container</c>).</summary>
        public bool HasContainer;

        /// <summary>Can be used by a player: a door, a bed, a sign, a stone to read (the game's <c>Interactable</c>).</summary>
        public bool IsUsable;

        /// <summary>Reached through an effect list on some other prefab rather than registered on its own.</summary>
        public bool FromEffectList;
    }

    internal static class Kinds
    {
        /// <summary>The kind a prefab is previewed as.</summary>
        public static Kind Of(PrefabTraits traits)
        {
            if (traits.IsStatusEffect) return Kind.StatusEffect;

            // What a prefab does in the game outranks how it looks: an arrow trails particles and
            // a troll makes noise, but they are a projectile and a creature.
            if (traits.HasCharacter) return Kind.Creature;
            if (traits.HasProjectile) return Kind.Projectile;
            if (traits.HasItemDrop) return Kind.Item;

            // What is chopped, mined or picked stays a resource when a mod makes it buildable too
            // (MoreVanillaBuildPrefabs does so for hundreds of the game's own prefabs); a crop
            // planted with the cultivator is built like any piece. A nest drops things too, but is
            // found with the other spawners.
            var gathered = !traits.HasSpawner && (traits.HasResource || traits.BreaksIntoResource);
            if (traits.HasPiece) return gathered && !traits.HasPlant ? Kind.Resource : Kind.Piece;
            if (gathered || (!traits.HasSpawner && traits.HasDestructible && traits.HasDrops)) return Kind.Resource;

            var visible = traits.HasRenderer || traits.HasParticles || traits.HasLight;
            if (traits.HasAudio && !visible) return Kind.Sound;

            // Something solid is scenery even when it smokes; an effect is only ever looked at.
            if (!traits.HasSolidCollider && (traits.HasParticles || (traits.FromEffectList && visible))) return Kind.Effect;

            return Kind.Other;
        }

        /// <summary>The group a resource is listed in; none for anything else.</summary>
        public static ResourceGroup GroupOf(PrefabTraits traits)
        {
            if (Of(traits) != Kind.Resource) return ResourceGroup.None;
            if (traits.IsTree) return ResourceGroup.Trees;
            if (traits.IsLog) return ResourceGroup.Logs;
            if (traits.IsMined || traits.BreaksIntoResource) return ResourceGroup.RocksAndOre;
            if (traits.HasPlant) return ResourceGroup.Plants;
            if (traits.IsPicked) return ResourceGroup.BushesAndPickables;
            return ResourceGroup.Other;
        }

        /// <summary>The heading a group of resources goes by.</summary>
        public static string GroupLabel(ResourceGroup group)
        {
            switch (group)
            {
                case ResourceGroup.Trees: return "Trees";
                case ResourceGroup.Logs: return "Logs";
                case ResourceGroup.RocksAndOre: return "Rocks and ore";
                case ResourceGroup.Plants: return "Plants";
                case ResourceGroup.BushesAndPickables: return "Bushes and pickables";
                case ResourceGroup.Other: return "Broken for what they drop";
                default: return "";
            }
        }

        /// <summary>Whether a prefab has anything to see or hear at all.</summary>
        public static bool IsEmpty(PrefabTraits traits)
        {
            if (traits.IsStatusEffect) return false;
            return !(traits.HasRenderer || traits.HasParticles || traits.HasLight || traits.HasAudio);
        }

        /// <summary>The word that picks a kind in a search's kind: term, as the search's help gives it.</summary>
        public static string TermWord(Kind kind) => kind == Kind.StatusEffect ? "se" : kind.ToString().ToLowerInvariant();

        /// <summary>The search help's line for the kind: term, naming every kind by the word that picks it.</summary>
        public static string HelpLine()
        {
            var words = ((Kind[])Enum.GetValues(typeof(Kind))).Select(k => k == Kind.StatusEffect ? TermWord(k) + " (status effect)" : TermWord(k));
            return $"Only one kind: {Naming.Commas(words)}.";
        }

        /// <summary>The name a kind goes by in the panel, in the plural.</summary>
        public static string Label(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Creatures";
                case Kind.Item: return "Items";
                case Kind.Piece: return "Pieces";
                case Kind.Resource: return "Resources";
                case Kind.Projectile: return "Projectiles";
                case Kind.Effect: return "Effects";
                case Kind.Sound: return "Sounds";
                case Kind.StatusEffect: return "Status effects";
                case Kind.Biome: return "Biomes";
                case Kind.Location: return "Locations";
                case Kind.Raid: return "Raids";
                case Kind.Mod: return "Mods";
                default: return "Other";
            }
        }
    }
}
