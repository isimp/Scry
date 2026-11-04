using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What guards the character against a creature's hit, read from the game at most once a
    /// second while something asks, as the game takes a hit on it (<c>Character.RPC_Damage</c>):
    /// its armour (<c>Player.GetBodyArmor</c>, what it wears and what effects change), its modifier
    /// of each damage type (<c>Character.GetDamageModifiers</c>, its own, its armour's and its
    /// effects'), and how much harder the world makes creatures hit it where it stands
    /// (<c>Game.m_enemyDamageRate</c>, <c>Game.GetDifficultyDamageScalePlayer</c>). Each change
    /// counted, so what is told at it is told again (<see cref="HitTaken"/>).
    /// </summary>
    internal static class YourGuard
    {
        /// <summary>Leaving a world forgets the character read (<see cref="WorldCaches"/>).</summary>
        static YourGuard() => WorldCaches.Register(nameof(YourGuard), Forget);

        /// <summary>The damage types a modifier is read for, by the game's type and the name a hit's figures go by.</summary>
        private static readonly (HitData.DamageType Type, string Name)[] Types =
        {
            (HitData.DamageType.Blunt, "blunt"), (HitData.DamageType.Slash, "slash"), (HitData.DamageType.Pierce, "pierce"),
            (HitData.DamageType.Chop, "chop"), (HitData.DamageType.Pickaxe, "pickaxe"), (HitData.DamageType.Fire, "fire"),
            (HitData.DamageType.Frost, "frost"), (HitData.DamageType.Lightning, "lightning"), (HitData.DamageType.Poison, "poison"),
            (HitData.DamageType.Spirit, "spirit"),
        };

        private static readonly Dictionary<string, float> Factors = new Dictionary<string, float>(StringComparer.Ordinal);
        private static float _readAt = -1f;
        private static int _version;
        private static bool _known;
        private static float _armour;
        private static float _scale = 1f;

        /// <summary>Counts every change of what guards the character, so what is told at it is told again.</summary>
        public static int Version
        {
            get
            {
                ReadIfDue();
                return _version;
            }
        }

        /// <summary>Whether there is a character to read: in a world, not on a server.</summary>
        public static bool Known
        {
            get
            {
                ReadIfDue();
                return _known;
            }
        }

        /// <summary>The character's armour, as last read.</summary>
        public static float Armour
        {
            get
            {
                ReadIfDue();
                return _armour;
            }
        }

        /// <summary>How much harder the world makes a creature's hit on the character where it stands, as last read: 1 as it comes.</summary>
        public static float Scale
        {
            get
            {
                ReadIfDue();
                return _scale;
            }
        }

        /// <summary>What the character's modifier leaves of a damage type, by the name a hit's figures go by (<see cref="HitTaken.Factor"/>), as last read; 1 for one not read.</summary>
        public static readonly Func<string, float> Factor = type => Factors.TryGetValue(type, out var factor) ? factor : 1f;

        private static void ReadIfDue()
        {
            if (_readAt >= 0f && Time.unscaledTime - _readAt < 1f) return;
            _readAt = Time.unscaledTime;
            Guard.Run(Feature.WhatYouTake, "reading what guards your character", Read);
        }

        /// <summary>Reads the character's armour, modifiers and the world's scale of hits, counting a change.</summary>
        private static void Read()
        {
            var player = Player.m_localPlayer;
            var known = player != null;
            var changed = known != _known;
            _known = known;
            if (!known) return;
            var armour = player.GetBodyArmor();
            var scale = Game.m_enemyDamageRate * (Game.instance != null ? Game.instance.GetDifficultyDamageScalePlayer(player.transform.position) : 1f);
            changed |= !Mathf.Approximately(armour, _armour) || !Mathf.Approximately(scale, _scale);
            _armour = armour;
            _scale = scale;
            var mods = player.GetDamageModifiers();
            foreach (var (type, name) in Types)
            {
                var factor = HitTaken.Factor(mods.GetModifier(type).ToString());
                if (!Factors.TryGetValue(name, out var was) || !Mathf.Approximately(was, factor)) changed = true;
                Factors[name] = factor;
            }
            if (changed) _version++;
        }

        /// <summary>Lets go of the character read, for a world that was left.</summary>
        private static void Forget()
        {
            Factors.Clear();
            _readAt = -1f;
            _known = false;
            _armour = 0f;
            _scale = 1f;
            _version++;
        }
    }
}
