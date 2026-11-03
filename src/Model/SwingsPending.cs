using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Attacks seen begun on a game that does not run their creature, whose trigger several of its
    /// weapons share, waiting to be told apart. The game running a creature equips the weapon it
    /// attacks with before the attack begins (<c>Humanoid.EquipBestWeapon</c>) and sends the
    /// trigger to every game at once (<c>ZSyncAnimation.SetTrigger</c>), while changed data goes
    /// out with the next send, every 0.05 s on each leg (<c>ZDOMan.SendZDOToPeers2</c>), so the
    /// trigger can come before the weapon it was equipped for. A creature re-picks its weapon only
    /// while not attacking (<c>MonsterAI</c>), so the weapon in its hand <see cref="Settle"/>
    /// after the trigger is the one it attacked with. What the attack put into the world beside
    /// its caster tells it too, at once: a shield's area or the creatures a summon places. What
    /// neither tells within <see cref="Window"/> is let go, told as nothing.
    /// </summary>
    internal sealed class SwingsPending
    {
        /// <summary>How long after the trigger the hand is read, in seconds: past a couple of data sends on each leg, short of the creature's next pick, which waits for its attack to end.</summary>
        public const float Settle = 0.5f;

        /// <summary>How long an attack waits to be told, in seconds: past a summon's own delays (a Dvergr mage's Mistiles come 0.4 s apart) and a hand read late.</summary>
        public const float Window = 2f;

        private sealed class Swing
        {
            public long Creature;
            public string Prefab;
            public IReadOnlyList<string> Candidates;
            public IReadOnlyList<(string Item, string Made, float Near)> Makes;
            public Vec3 At;
            public float Away;
            public float Since;
            public bool HandRead;
        }

        private readonly List<Swing> _swings = new List<Swing>();

        /// <summary>How many attacks wait to be told.</summary>
        public int Count => _swings.Count;

        /// <summary>
        /// An attack begun by a creature, by its own number (<paramref name="creature"/>) and prefab,
        /// the weapons its trigger may start, what each of them puts into the world and how near its
        /// caster (<paramref name="makes"/>), where the caster stood and how far from the character.
        /// </summary>
        public void Begin(long creature, string prefab, IReadOnlyList<string> candidates, IReadOnlyList<(string Item, string Made, float Near)> makes, Vec3 at, float away, float now)
        {
            if (candidates == null || candidates.Count < 2) return;
            _swings.Add(new Swing { Creature = creature, Prefab = prefab, Candidates = candidates, Makes = makes ?? Array.Empty<(string, string, float)>(), At = at, Away = away, Since = now });
        }

        /// <summary>
        /// The attacks the hand tells now (<paramref name="handOf"/>, the weapon a creature holds
        /// by its number, none for none), each read once, <see cref="Settle"/> after its trigger;
        /// those waiting past <see cref="Window"/> are let go. Each told as its creature, its
        /// weapon and how far it was; the list given when none is told is shared, to be read only.
        /// </summary>
        public List<(string Creature, string Item, float Away)> Tick(Func<long, string> handOf, float now)
        {
            List<(string, string, float)> told = null;
            for (var i = 0; i < _swings.Count; i++)
            {
                var swing = _swings[i];
                if (now - swing.Since > Window)
                {
                    _swings.RemoveAt(i--);
                    continue;
                }
                if (swing.HandRead || now - swing.Since < Settle) continue;
                swing.HandRead = true;
                var item = AttackTriggers.Resolve(swing.Candidates, handOf(swing.Creature));
                if (item == null) continue;
                if (told == null) told = new List<(string, string, float)>();
                told.Add((swing.Prefab, item, swing.Away));
                _swings.RemoveAt(i--);
            }
            return told ?? Nothing;
        }

        /// <summary>None told, given each frame nothing is, so a frame waiting makes no list.</summary>
        private static readonly List<(string, string, float)> Nothing = new List<(string, string, float)>();

        /// <summary>
        /// Something the game keeps in the world woke, by its prefab and where it stands: the first
        /// attack waiting whose weapons only one of makes it, near enough its caster, is told by it.
        /// </summary>
        public (string Creature, string Item, float Away)? Woke(string made, Vec3 at, float now)
        {
            if (_swings.Count == 0 || string.IsNullOrEmpty(made)) return null;
            for (var i = 0; i < _swings.Count; i++)
            {
                var swing = _swings[i];
                if (now - swing.Since > Window) continue;
                string found = null;
                var makers = 0;
                foreach (var (item, prefab, near) in swing.Makes)
                {
                    if (prefab != made || !Within(swing.At, at, near)) continue;
                    if (found != item) makers++;
                    found = item;
                }
                if (makers != 1 || !Contains(swing.Candidates, found)) continue;
                _swings.RemoveAt(i);
                return (swing.Prefab, found, swing.Away);
            }
            return null;
        }

        /// <summary>Lets every attack waiting go, as on leaving a world.</summary>
        public void Clear() => _swings.Clear();

        /// <summary>Whether a thing stands within a reach of the caster across the ground, as a summon places its creatures round the caster and raises them off the ground.</summary>
        private static bool Within(Vec3 caster, Vec3 thing, float near)
        {
            var dx = thing.X - caster.X;
            var dz = thing.Z - caster.Z;
            return dx * dx + dz * dz <= near * near;
        }

        private static bool Contains(IReadOnlyList<string> items, string item)
        {
            foreach (var each in items) if (each == item) return true;
            return false;
        }
    }
}
