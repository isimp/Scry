using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>The selection's copy in the world, where you look: shown, placed, pinned, cleared, falling and breaking there.</summary>
    internal static partial class Previews
    {
        public static bool IsModel(Entry entry)
        {
            return entry != null && !entry.Empty && entry.Source is GameObject
                   && entry.Kind != Kind.Sound && entry.Kind != Kind.Effect && entry.Kind != Kind.StatusEffect;
        }

        /// <summary>Shows the selection in the world where you are looking, or takes it away again.</summary>
        public static void ToggleWorld()
        {
            if (InWorld)
            {
                InWorld = false;
                Standin.ClearFor(_world);
                Destroy(ref _world);
                return;
            }

            InWorld = true;
            Aim();
            if (_explorer != null) RebuildWorld(_explorer.Modifiers);
        }

        /// <summary>Moves the copy in the world to where you are looking now.</summary>
        public static void PlaceHere()
        {
            Aim();
            if (_world == null) return;
            Standin.ClearFor(_world);
            _world.transform.position = _spot;
            _world.transform.rotation = _facing;
        }

        /// <summary>Leaves the copy in the world standing, so another can be shown beside it.</summary>
        public static void Pin()
        {
            if (_world == null) return;
            Pinned.Add(_world);
            _world = null;
            InWorld = false;
        }

        /// <summary>Removes everything Scry has put in the world.</summary>
        public static void ClearWorld()
        {
            InWorld = false;
            Destroy(ref _world);
            foreach (var pinned in Pinned) if (pinned != null) Object.Destroy(pinned);
            Pinned.Clear();
            foreach (var played in Played) if (played.Thing != null) Object.Destroy(played.Thing);
            Played.Clear();
            StopSound();
            StopStatus(false);
            Playing.Forget();
        }

        private static void RebuildWorld(Modifiers modifiers)
        {
            Standin.ClearFor(_world);
            Destroy(ref _world);
            if (!IsModel(_entry)) return;

            _world = Looks.Copy(_entry, modifiers, null, _spot, _facing);
            if (_world == null) return;

            _world.AddComponent<Keep>().BaseScale = _world.transform.localScale;
            ApplyLive(_world, modifiers);
        }

        private static void ApplyLive(GameObject copy, Modifiers modifiers)
        {
            if (copy == null) return;
            var keep = copy.GetComponent<Keep>();
            if (keep != null) copy.transform.localScale = keep.BaseScale * modifiers.Scale;
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true)) animator.speed = modifiers.AnimationSpeed;
            ClipPlayer.SetSpeed(copy, modifiers.AnimationSpeed);
        }

        /// <summary>
        /// Where you are looking: the first ground or building along the camera's view, or a few
        /// metres in front of you when there is none. Copies there face you.
        /// </summary>
        private static void Aim()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

            if (Physics.Raycast(view.position, view.forward, out var hit, 60f, mask, QueryTriggerInteraction.Ignore))
            {
                _spot = hit.point;
            }
            else
            {
                _spot = player.transform.position + player.transform.forward * 4f;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(_spot, out var ground)) _spot.y = ground;
            }

            var toPlayer = player.transform.position - _spot;
            toPlayer.y = 0f;
            _facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
        }

        private static GameObject FallInWorld(global::Ragdoll ragdoll, GameObject creature, int level, IList<GameObject> gear)
        {
            if (_world == null || Standin.IsDown(_world) || !Falling.Ready(Stage.Layer)) return null;
            var scale = _explorer != null ? _explorer.Modifiers.Scale : 1f;
            var fallen = Falling.Ragdoll(ragdoll, creature, _world, level, scale, gear, null, -1, Stage.Layer);
            if (fallen == null) return null;

            var seconds = Mathf.Clamp(ragdoll.m_ttl, 3f, 12f);
            Standin.For(fallen, _world, seconds, ragdoll.m_removeEffect, onStage: false);
            Remember(fallen, seconds + 1f);
            return fallen;
        }

        private static GameObject LetFallInWorld(GameObject prefab)
        {
            if (_world == null || Standin.IsDown(_world) || !Falling.Ready(Stage.Layer)) return null;
            var player = Player.m_localPlayer;
            var away = player != null ? Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(_world.transform.position - player.transform.position, Vector3.up).normalized) : Vector3.right;
            var loose = Falling.Loose(prefab, _world, null, -1, Stage.Layer, away);
            if (loose == null) return null;
            Standin.For(loose, _world, Stage.LooseSeconds, null, onStage: false);
            Remember(loose, Stage.LooseSeconds + 1f);
            return loose;
        }

        private static GameObject DestroyInWorld(GameObject prefab, EffectList list)
        {
            if (_world == null || Standin.IsDown(_world) || !Falling.Ready(Stage.Layer)) return null;

            var seconds = Falling.DebrisSeconds(list);
            var left = Falling.Breaks(prefab, list) ? Falling.Break(prefab, _world, null, -1, Stage.Layer) : null;
            if (left == null)
            {
                var player = Player.m_localPlayer;
                var away = player != null ? Vector3.ProjectOnPlane(_world.transform.position - player.transform.position, Vector3.up).normalized : Vector3.forward;
                left = Falling.Fell(prefab, _world, null, -1, Stage.Layer, away);
                if (left != null) seconds = 10f;
            }
            if (left == null) left = new GameObject("Scry destroyed");
            var from = Player.m_localPlayer != null ? Vector3.ProjectOnPlane(_world.transform.position - Player.m_localPlayer.transform.position, Vector3.up).normalized : Vector3.forward;
            if (Falling.Leave(prefab, _world, left.transform, -1, Stage.Layer, from)) seconds = Mathf.Max(seconds, 8f);

            Standin.For(left, _world, seconds, null, onStage: false);
            Remember(left, seconds + 1f);
            return left;
        }
    }
}
