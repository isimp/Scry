namespace Scry
{
    /// <summary>What a prefab is, as far as previewing it goes.</summary>
    public enum Kind
    {
        Creature,
        Item,
        Piece,
        Resource,
        Projectile,
        Effect,
        Sound,
        StatusEffect,
        Other,
    }

    /// <summary>Where a prefab comes from.</summary>
    public enum Origin
    {
        Unknown,
        Vanilla,
        Mod,
    }

    /// <summary>
    /// The facts about a prefab that decide its kind, read from its components in the game.
    /// Kept apart from the components themselves so the rule can be checked without the game.
    /// </summary>
    public sealed class PrefabTraits
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

        /// <summary>Breaks when hit, and whether it then drops anything.</summary>
        public bool HasDestructible;
        public bool HasDrops;

        /// <summary>Turns, when broken, into something mined or chopped (a silver vein into the vein itself).</summary>
        public bool BreaksIntoResource;

        /// <summary>Spawns creatures, as a nest does.</summary>
        public bool HasSpawner;

        /// <summary>Reached through an effect list on some other prefab rather than registered on its own.</summary>
        public bool FromEffectList;
    }

    public static class Kinds
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
            if (traits.HasPiece) return Kind.Piece;

            // What is chopped, mined, picked or grown, or broken for what it drops; a nest drops
            // things too, but is found with the other spawners.
            if (!traits.HasSpawner && (traits.HasResource || traits.BreaksIntoResource || (traits.HasDestructible && traits.HasDrops))) return Kind.Resource;

            var visible = traits.HasRenderer || traits.HasParticles || traits.HasLight;
            if (traits.HasAudio && !visible) return Kind.Sound;

            // Something solid is scenery even when it smokes; an effect is only ever looked at.
            if (!traits.HasSolidCollider && (traits.HasParticles || (traits.FromEffectList && visible))) return Kind.Effect;

            return Kind.Other;
        }

        /// <summary>Whether a prefab has anything to see or hear at all.</summary>
        public static bool IsEmpty(PrefabTraits traits)
        {
            if (traits.IsStatusEffect) return false;
            return !(traits.HasRenderer || traits.HasParticles || traits.HasLight || traits.HasAudio);
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
                default: return "Other";
            }
        }
    }
}
