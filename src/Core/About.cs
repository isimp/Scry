using System.Linq;

namespace Scry
{
    /// <summary>Scry itself: its id and version, as BepInEx, Harmony and its own files know it, and its own types.</summary>
    internal static class About
    {
        public const string Guid = "isimp.Scry";
        public const string Name = "Scry";
        public const string Version = "0.2.0";

        /// <summary>
        /// Scry's own types, those that load. One naming a game type an update removed cannot be
        /// loaded; the rest still are, so that one costs only itself.
        /// </summary>
        public static System.Type[] OwnTypes()
        {
            try
            {
                return typeof(About).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                Faults.Tell(Feature.World, Numbers.Count(ex.Types.Count(t => t == null)) + " of Scry's own parts", ex.LoaderExceptions.FirstOrDefault(e => e != null) ?? ex);
                return ex.Types.Where(t => t != null).ToArray();
            }
        }
    }
}
