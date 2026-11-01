using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A creature with no drop list dying on the game running it (<c>Character.OnDeath</c> with no
    /// <c>CharacterDrop</c>), as a starred piglet, cub or calf: no loot tells its death, so it is
    /// told by itself, with its level and who hit it. Its remains tell it where they take loot, as
    /// they go on every game alike (<c>Ragdoll.m_dropItems</c>); else it is told at once.
    /// </summary>
    internal sealed class DroplessDeaths
    {
        private (string Creature, int Level, IReadOnlyList<long> Doers)? _dying;

        /// <summary>A creature dying: noted where it is one of these, the death this game's and no loot to tell it; any noted before and left untold by a fault is dropped.</summary>
        public void Dying(string creature, int level, IReadOnlyList<long> doers, bool player, bool runsHere, bool hasDrops)
        {
            _dying = player || !runsHere || hasDrops || string.IsNullOrEmpty(creature) ? null : ((string, int, IReadOnlyList<long>)?)(creature, level, doers);
        }

        /// <summary>Remains made for the death noted: the death, where they take loot and so tell it, taken from the telling at once.</summary>
        public (string Creature, int Level, IReadOnlyList<long> Doers)? TakenByRemains(bool remainsTakeLoot)
        {
            if (!remainsTakeLoot || _dying == null) return null;
            var dying = _dying;
            _dying = null;
            return dying;
        }

        /// <summary>The death is over: the one noted, to tell at once, where no remains took it.</summary>
        public (string Creature, int Level, IReadOnlyList<long> Doers)? Died()
        {
            var dying = _dying;
            _dying = null;
            return dying;
        }
    }
}
