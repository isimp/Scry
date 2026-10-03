using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>What plays on the stage: an effect list on the model, what hangs on it, falls from it or breaks off it, and the ground what falls lands on.</summary>
    internal static partial class Stage
    {
        /// <summary>
        /// Plays an effect list on the stage copy, as the game would play it on the prefab: on the
        /// named part of its body when there is one, attached when the list says so. Heard as if
        /// beside you.
        /// </summary>
        public static List<(string, GameObject)> PlayList(EffectList list, Transform part = null, string skip = null, Vector3? point = null)
        {
            var made = new List<(string, GameObject)>();
            if (_subject == null || list?.m_effectPrefabs == null) return made;

            var center = point ?? (part != null ? part.position : Origin + (_bounds.center - Origin) * _scale);
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null || data.m_prefab.name == skip) continue;
                var debris = PrefabShapes.IsDebris(data.m_prefab);
                if (!debris && PrefabShapes.IsWholeModel(data.m_prefab)) continue;

                // The game looks for the named part and hangs the effect on it only when it
                // gives a parent; what it places at a point (a hit, a thrown thing's release) it
                // gives none (EffectList.Create).
                var anchor = part != null ? part : _subject.transform;
                var at = center;
                if (point == null && !string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(anchor, data.m_childTransform);
                    if (child != null)
                    {
                        anchor = child;
                        at = child.position;
                    }
                }

                GameObject copy;
                if (debris)
                {
                    if (!Falling.Ready(_layer)) continue;
                    PlaceGround();
                    copy = Falling.Debris(data.m_prefab, _root.transform, at, anchor.rotation, _layer, _layer);
                }
                else
                {
                    copy = data.m_attach && point == null
                        ? Ghost.MakeOn(data.m_prefab, anchor, at, anchor.rotation, _layer)
                        : Ghost.Make(data.m_prefab, _root.transform, at, anchor.rotation, _layer);
                }
                if (copy == null) continue;
                if (!debris) Ghost.Magnify(copy, _scale);
                Tune(copy, audible: !Previews.WorldHeard);
                Played.Add(new KeyValuePair<GameObject, float>(copy, Time.unscaledTime + PlayedSeconds));
                made.Add((data.m_prefab.name, copy));
            }
            return made;
        }

        /// <summary>
        /// Hangs a copy of a prefab on one of the subject's bones, heard as if beside you. It
        /// goes with the subject, so nothing else has to clear it.
        /// </summary>
        public static GameObject Hang(GameObject prefab, Transform joint)
        {
            if (_subject == null || joint == null) return null;
            var copy = Ghost.MakeOn(prefab, joint, joint.position, joint.rotation, _layer);
            if (copy != null) Tune(copy, audible: !Previews.WorldHeard);
            return copy;
        }

        /// <summary>
        /// Lets the stage copy die as the game lets it: its ragdoll falls where it stood, in its
        /// level's colours and its armour, and lies there as long as the game leaves it before
        /// its parting effect plays and the creature stands again.
        /// </summary>
        public static GameObject Fall(global::Ragdoll ragdoll, GameObject creature, int level, IList<GameObject> gear)
        {
            if (_subject == null || ragdoll == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();

            var fallen = Falling.Ragdoll(ragdoll, creature, _subject, level, _scale, gear, _root.transform, _layer, _layer);
            if (fallen == null) return null;
            Tune(fallen, audible: !Previews.WorldHeard);

            var seconds = Mathf.Clamp(ragdoll.m_ttl, 3f, 12f);
            Standin.For(fallen, _subject, seconds, ragdoll.m_removeEffect, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(fallen, Time.unscaledTime + seconds + 1f));
            return fallen;
        }

        /// <summary>
        /// Destroys the stage copy as the game destroys the prefab: it is gone while what it
        /// leaves behind falls (its parts, its log and stump, the debris its list throws), and then
        /// it stands again.
        /// </summary>
        public static GameObject Destroy(GameObject prefab, EffectList list)
        {
            if (_subject == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();

            var seconds = Falling.DebrisSeconds(list);
            var away = _camera != null ? Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            var left = Falling.Breaks(prefab, list) ? Falling.Break(prefab, _subject, _root.transform, _layer, _layer) : null;
            if (left == null)
            {
                left = Falling.Fell(prefab, _subject, _root.transform, _layer, _layer, away);
                if (left != null) seconds = 10f;
            }
            if (left == null)
            {
                left = new GameObject("Scry destroyed");
                left.transform.SetParent(_root.transform, false);
            }
            if (Falling.Leave(prefab, _subject, left.transform, _layer, _layer, away)) seconds = Mathf.Max(seconds, 8f);
            // What falls is heard as if beside you, the log's creak and the parts' clatter, as anything on the stage is.
            Tune(left, audible: !Previews.WorldHeard);

            Standin.For(left, _subject, seconds, null, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(left, Time.unscaledTime + seconds + 1f));
            return left;
        }

        /// <summary>
        /// Lets the stage copy of something the game leaves to physics (a log, an item) fall from
        /// where it stands and roll or tumble on the ground; it stands again after a while.
        /// </summary>
        public static GameObject LetFall(GameObject prefab)
        {
            if (_subject == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();
            var away = _camera != null ? Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized : Vector3.right;
            var loose = Falling.Loose(prefab, _subject, _root.transform, _layer, _layer, away);
            if (loose == null) return null;
            Tune(loose, audible: !Previews.WorldHeard);
            Standin.For(loose, _subject, LooseSeconds, null, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(loose, Time.unscaledTime + LooseSeconds + 1f));
            return loose;
        }

        /// <summary>How long something let fall lies before the copy stands again.</summary>
        public const float LooseSeconds = 8f;

        /// <summary>The invisible ground falling copies land on, level with the floor under the model.</summary>
        private static void PlaceGround()
        {
            if (_ground == null) return;
            var subject = new Bounds(Origin + (_bounds.center - Origin) * _scale, _bounds.size * _scale);
            var reach = Mathf.Max(2f, subject.extents.magnitude * 10f);
            _ground.transform.position = new Vector3(subject.center.x, FloorY - 0.5f, subject.center.z);
            _ground.transform.localScale = new Vector3(reach, 1f, reach);
        }

        public static void Adopt(GameObject copy, float seconds)
        {
            if (copy == null || _root == null) return;
            copy.transform.SetParent(_root.transform, true);
            Ghost.SetLayer(copy.transform, _layer);
            Tune(copy, audible: !Previews.WorldHeard);
            Played.Add(new KeyValuePair<GameObject, float>(copy, Time.unscaledTime + seconds));
        }
    }
}
