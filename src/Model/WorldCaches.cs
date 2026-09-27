using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Everything kept about one world that has to go when the world is left: each class that
    /// keeps such things registers how it forgets them, once, as it is first used, and leaving a
    /// world forgets them all. One that fails does not keep the others from being forgotten.
    /// </summary>
    public static class WorldCaches
    {
        private static readonly List<(string Name, Action Forget)> All = new List<(string, Action)>();

        /// <summary>Registers how a class forgets what it keeps of a world; again under the same name, it replaces it.</summary>
        public static void Register(string name, Action forget)
        {
            if (string.IsNullOrEmpty(name) || forget == null) return;
            lock (All)
            {
                All.RemoveAll(c => c.Name == name);
                All.Add((name, forget));
            }
        }

        /// <summary>The names registered, in the order they were.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                lock (All) return All.ConvertAll(c => c.Name);
            }
        }

        /// <summary>Forgets everything registered, each on its own; a failure is told through <paramref name="failed"/>.</summary>
        public static void ForgetAll(Action<string, Exception> failed)
        {
            (string Name, Action Forget)[] all;
            lock (All) all = All.ToArray();
            foreach (var (name, forget) in all)
            {
                try
                {
                    forget();
                }
                catch (Exception ex)
                {
                    failed?.Invoke(name, ex);
                }
            }
        }
    }
}
