using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Tells in the log, once for each effect list and animation clip, what playing it did: the
    /// copies it made, whether each sounded, showed particles or was drawn, where it played, and
    /// for a clip the events that arrived and the footsteps heard. For finding out why something
    /// seems to play nothing, without guessing.
    /// </summary>
    internal static class Listen
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Listen() => WorldCaches.Register(nameof(Listen), Forget);

        private sealed class Watch
        {
            public string What;
            public float Until;
            public readonly List<(GameObject Thing, string Where)> Things = new List<(GameObject, string)>();
            public readonly Dictionary<GameObject, (bool Sounded, int Particles, bool Drawn, bool Muted)> Seen = new Dictionary<GameObject, (bool, int, bool, bool)>();
            public readonly Dictionary<string, int> Notes = new Dictionary<string, int>();
        }

        private static readonly Dictionary<string, Watch> Watching = new Dictionary<string, Watch>();

        /// <summary>Stops watching what was played in the world left.</summary>
        public static void Forget() => Watching.Clear();
        private static readonly HashSet<string> Told = new HashSet<string>();

        // Filled again for each thing watched, so watching makes no garbage each frame.
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static readonly List<ParticleSystem> Systems = new List<ParticleSystem>();
        private static readonly List<Renderer> Renderers = new List<Renderer>();

        /// <summary>Starts watching what something plays, the first time it plays; later plays add to it while it is watched.</summary>
        public static void Start(string what, float seconds)
        {
            if (what == null || !Plugin.LogPreviews || Told.Contains(what) || Watching.ContainsKey(what)) return;
            Watching[what] = new Watch { What = what, Until = Time.unscaledTime + Mathf.Clamp(seconds, 1.5f, 8f) };
        }

        public static void Add(string what, IEnumerable<GameObject> things)
        {
            if (what == null || !Watching.TryGetValue(what, out var watch)) return;
            foreach (var thing in things)
            {
                if (thing != null && !watch.Things.Exists(t => t.Thing == thing)) watch.Things.Add((thing, Where(thing)));
            }
        }

        public static void Note(string what, string note)
        {
            if (what == null || !Watching.TryGetValue(what, out var watch)) return;
            watch.Notes[note] = watch.Notes.TryGetValue(note, out var n) ? n + 1 : 1;
        }

        public static void Update()
        {
            if (Watching.Count == 0) return;
            var now = Time.unscaledTime;
            foreach (var watch in Watching.Values.ToList())
            {
                foreach (var (thing, _) in watch.Things)
                {
                    if (thing == null) continue;
                    watch.Seen.TryGetValue(thing, out var seen);
                    thing.GetComponentsInChildren(true, Sources);
                    seen.Sounded |= Sources.Exists(s => s.isPlaying && !s.mute && s.volume > 0f);
                    seen.Muted |= Sources.Count > 0 && Sources.TrueForAll(s => s.mute);
                    Sources.Clear();
                    thing.GetComponentsInChildren(false, Systems);
                    var particles = 0;
                    foreach (var system in Systems) particles += system.particleCount;
                    Systems.Clear();
                    seen.Particles = Mathf.Max(seen.Particles, particles);
                    if (!seen.Drawn)
                    {
                        thing.GetComponentsInChildren(false, Renderers);
                        seen.Drawn = Renderers.Exists(r => r.enabled && r.isVisible && !(r is ParticleSystemRenderer));
                        Renderers.Clear();
                    }
                    watch.Seen[thing] = seen;
                }
                if (now < watch.Until) continue;

                Watching.Remove(watch.What);
                Told.Add(watch.What);
                Plugin.Note(Report(watch));
            }
        }

        private static string Report(Watch watch)
        {
            var parts = new List<string>();
            foreach (var (thing, where) in watch.Things)
            {
                var name = thing != null ? thing.name : "a copy gone early";
                watch.Seen.TryGetValue(thing, out var seen);
                var what = new List<string>();
                if (seen.Sounded) what.Add("sounded");
                else if (seen.Muted) what.Add("muted");
                if (seen.Particles > 0) what.Add($"{seen.Particles} particles");
                if (seen.Drawn) what.Add("drawn");
                if (what.Count == 0) what.Add("nothing to see or hear" + (thing != null ? " (" + Inside(thing) + ")" : ""));
                parts.Add($"{name} {where}: {string.Join(", ", what)}");
            }
            var notes = watch.Notes.Select(n => n.Value > 1 ? $"{n.Key} ×{n.Value}" : n.Key).ToList();
            var made = parts.Count > 0 ? string.Join("; ", parts) : "no copies";
            return $"Scry played {watch.What}: {made}{(notes.Count > 0 ? ". " + string.Join("; ", notes) : "")}.";
        }

        /// <summary>
        /// What a copy that showed nothing has, to tell why: its renderers, how many are switched
        /// on and seen by a camera, its particle systems and how many play, and its sounds.
        /// </summary>
        private static string Inside(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToList();
            var on = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
            var seen = renderers.Count(r => r.isVisible);
            var systems = thing.GetComponentsInChildren<ParticleSystem>(true);
            var playing = systems.Count(p => p.isPlaying);
            var sounds = thing.GetComponentsInChildren<AudioSource>(true).Length;
            var unseen = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.isVisible)
                .Select(r => $"{r.GetType().Name} {r.bounds.size.magnitude:0.0} m{(r.GetComponentInParent<LODGroup>() != null ? " in a level-of-detail group" : "")}{(Stage.InView(r.bounds) ? ", in the stage's view" : ", outside the stage's view")}").ToList();
            var off = thing.activeInHierarchy ? "" : thing.activeSelf ? "; switched off above it" : "; switched off in itself, as saved";
            var listener = Object.FindAnyObjectByType<AudioListener>();
            var quiet = thing.GetComponentsInChildren<AudioSource>(true).Select(s =>
            {
                var sfx = s.GetComponent<ZSFX>();
                var far = listener != null ? $", {Vector3.Distance(listener.transform.position, s.transform.position):0} m from the ears of {s.maxDistance:0}" : "";
                return $"{(s.clip != null ? s.clip.name : "no clip")} {(s.isPlaying ? "playing" : "not playing")}{(s.enabled ? "" : ", off")}, volume {s.volume:0.##}{far}, {(sfx == null ? "no ZSFX" : (sfx.enabled ? "ZSFX on" : "ZSFX off") + (sfx.m_playOnAwake ? "" : ", not played on waking"))}";
            }).ToList();
            return $"{renderers.Count} renderers, {on} on, {seen} seen; {systems.Length} particle systems, {playing} playing; {sounds} sounds{off}{(unseen.Count > 0 ? "; unseen: " + string.Join(", ", unseen) : "")}{(quiet.Count > 0 ? "; sounds: " + string.Join(", ", quiet) : "")}";
        }

        /// <summary>On the stage or in the world, and how far from the camera there.</summary>
        private static string Where(GameObject thing)
        {
            if (thing.layer == Stage.Layer) return "on the stage";
            var camera = GameCamera.instance != null ? GameCamera.instance.transform.position : thing.transform.position;
            return $"in the world {Vector3.Distance(camera, thing.transform.position):0} m away";
        }
    }
}
