using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>An attack's clip playing the attack whole: as it begins, when it strikes, and what it throws.</summary>
    internal sealed partial class AnimationEars
    {
        private void StartAttack(AnimationClip clip)
        {
            var part = clip != null ? AttackFor(clip.name) : null;
            _clipAttack = part?.Key as Attack;
            _clipTrigger = null;
            _clipHit = null;
            _strikeAt = -1f;
            if (_clipAttack != null)
            {
                Previews.AttackParts(_clipAttack, out var begin, out _clipTrigger, out _clipHit);
                Listen.Note(Listening, "the attack " + _clipAttack.m_attackAnimation + (part.Begins ? "" : ", begun in an earlier clip"));

                // The game strikes when a clip says so; an attack whose clips never say, halfway.
                if (part.Halfway)
                {
                    _strikeAt = clip.length * 0.5f;
                    Listen.Note(Listening, "no clip of the attack says when it strikes, so it strikes halfway");
                }
                // Begun where the attack comes from, as the game starts it there.
                if (part.Begins) Report(Previews.PlayOnCopy(_copy, begin, Previews.AttackOrigin(_copy, _clipAttack)));
            }
        }

        private Attack _clipAttack;
        private EffectList _clipTrigger;
        private EffectList _clipHit;
        private float _strikeAt = -1f;

        /// <summary>
        /// What an attack's clip plays when it strikes, where the game plays it: its trigger where
        /// the attack comes from, or for a throw where it lets go; its hit where it reaches; what
        /// it spawns there; and what it throws or shoots.
        /// </summary>
        private void ClipStrike()
        {
            var attack = _clipAttack;
            _strikeAt = -1f;
            if (attack == null || _copy == null) return;
            if (_clipTrigger != null)
            {
                // A swing's at its joint, hung there; an area's in its middle and a throw's where
                // it lets go (DoMeleeAttack, DoAreaAttack, ProjectileAttackTriggered).
                Report(Previews.IsMelee(attack)
                    ? Previews.PlayOnCopy(_copy, _clipTrigger, Previews.AttackOrigin(_copy, attack))
                    : Previews.PlayOnCopyAt(_copy, _clipTrigger, Previews.ReachPoint(_copy, attack)));
            }
            if (EffectSlots.Of(_clipHit).Length > 0)
            {
                var point = Previews.StrikePoint(_copy, attack, Previews.LandsOnGround(_clipHit));
                var ahead = Vector3.Dot(point - _copy.transform.position, _copy.transform.forward);
                if (Plugin.LogPreviews) Listen.Note(Listening, $"hits {Numbers.Fixed(ahead, 1)} m ahead and {Numbers.Fixed(point.y - _copy.transform.position.y, 1)} m up of a copy shown at ×{Numbers.Fixed(Previews.SizeOf(_copy), 2)}, its scale {Numbers.Fixed(_copy.transform.lossyScale.x, 2)}");
                Report(Previews.PlayOnCopyAt(_copy, _clipHit, point));
            }
            if (attack.m_spawnOnTrigger != null && (Previews.IsMelee(attack) || attack.m_attackType == Attack.AttackType.Area))
            {
                Report(Previews.PlayOnCopyAt(_copy, AsList(new[] { attack.m_spawnOnTrigger }), Previews.StrikePoint(_copy, attack, false)));
                Listen.Note(Listening, "spawned " + attack.m_spawnOnTrigger.name);
            }
            Launch(attack);
        }

        /// <summary>
        /// Throws or shoots an attack's projectile, as <c>Attack</c> does when its swing strikes:
        /// from where the attack sends it, along the creature's facing and its launch angle.
        /// </summary>
        private void Launch(Attack attack)
        {
            if (attack == null || _copy == null || attack.m_attackType != Attack.AttackType.Projectile) return;
            if (attack.m_attackProjectile == null)
            {
                Listen.Note(Listening, "a projectile attack that names no projectile");
                return;
            }

            // Where Attack.GetProjectileSpawnPoint puts it, at the copy's size.
            var t = _copy.transform;
            var start = Previews.ReachPoint(_copy, attack);
            var aim = t.forward;
            if (attack.m_launchAngle != 0f) aim = Quaternion.AngleAxis(attack.m_launchAngle, Vector3.Cross(Vector3.up, aim)) * aim;

            var thrown = Previews.Launch(attack.m_attackProjectile, start, aim * attack.m_projectileVel, _copy == Stage.Subject, Previews.SizeOf(_copy));
            if (thrown != null)
            {
                Report(new List<GameObject> { thrown });
                var heard = Listening;
                Listen.Add(heard, new List<GameObject> { thrown });
                Listen.Note(heard, "threw " + attack.m_attackProjectile.name);
            }
        }

        /// <summary>The strike of the attack whose clip is playing, when the clip says it strikes.</summary>
        private void AttackTrigger()
        {
            var clip = _copy.GetComponent<ClipPlayer>()?.Clip;
            if (clip == null || _clipAttack == null) return;
            ClipStrike();
        }

        /// <summary>What a clip of this prefab plays of an attack, as its animator plays it; null for a clip no attack plays.</summary>
        private ClipAttack AttackFor(string clip) => Previews.AttackOfClip(_prefab, _copy, clip);
    }
}
