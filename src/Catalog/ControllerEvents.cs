using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What an animation controller's clips send, read once per controller: how many events,
    /// those Scry does not answer (<see cref="AnimationEvents"/>), and the effects and props the
    /// clips name to play or hold, by clip. The catalog reads it for every creature it links;
    /// a copy's ears ask it too, so showing a person need not read its hundreds of clips again.
    /// </summary>
    internal static class ControllerEvents
    {
        static ControllerEvents() => WorldCaches.Register(nameof(ControllerEvents), Forget);

        internal sealed class Read
        {
            public int Events;
            public readonly SortedSet<string> Unknown = new SortedSet<string>(StringComparer.Ordinal);
            public readonly List<(string Clip, GameObject Thing)> Named = new List<(string, GameObject)>();
        }

        private static readonly Dictionary<RuntimeAnimatorController, Read> Known = new Dictionary<RuntimeAnimatorController, Read>();

        public static Read Of(RuntimeAnimatorController controller)
        {
            if (controller == null) return new Read();
            if (Known.TryGetValue(controller, out var known)) return known;
            var read = new Read();
            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                foreach (var e in clip.events)
                {
                    read.Events++;
                    if (!AnimationEvents.Answers(e.functionName)) read.Unknown.Add(e.functionName);
                    if ((e.functionName == "Effect" || e.functionName == "Attach") && e.objectReferenceParameter is GameObject thing && thing != null)
                    {
                        read.Named.Add((clip.name, thing));
                    }
                }
            }
            Known[controller] = read;
            return read;
        }

        /// <summary>Lets go of what was read, for a world that was left: the controllers can be gone or changed.</summary>
        public static void Forget() => Known.Clear();
    }
}
