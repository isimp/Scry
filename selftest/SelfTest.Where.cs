using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's trace of the place the game says the character stands in.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Where the trace is written, beside the self-test's results.</summary>
        public static string WhereFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-where.log");

        private static float _whereAt;
        private static string _whereLast;
        private static KnownLevel _whereLevel;

        /// <summary>
        /// While no part runs, once a second: the place the game says the character stands in, as
        /// the compendium asks it (<c>Location.GetLocation</c>), written to <see cref="WhereFile"/>
        /// each time it changes or the book's level of it does, with how far the character stands
        /// from its middle and how far it reaches; standing in none, the nearest place loaded and its
        /// reach. For finding out why a place stood in is not counted.
        /// </summary>
        private static void TraceWhere(Player player)
        {
            if (Time.unscaledTime - _whereAt < 1f) return;
            _whereAt = Time.unscaledTime;
            var at = player.transform.position;
            var place = Location.GetLocation(at);
            var book = CompendiumKeeper.Book;
            string name;
            var level = KnownLevel.Unknown;
            Location nearest = null;
            var away = 0f;
            if (place != null)
            {
                name = Utils.GetPrefabName(place.gameObject);
                level = book?.Level(EntryKeys.For(Kind.Location, name)) ?? KnownLevel.Unknown;
            }
            else
            {
                // In none: told again as the nearest changes, and as one comes within its reach, where the game should have named it.
                nearest = Nearest(at, out away);
                var within = nearest != null && away < nearest.GetMaxRadius();
                name = nearest == null ? "none" : "none near " + Utils.GetPrefabName(nearest.gameObject) + (within ? ", within its reach" : "");
            }
            if (name == _whereLast && level == _whereLevel) return;
            _whereLast = name;
            _whereLevel = level;
            var when = EnvMan.instance != null ? $"day {Numbers.Count(EnvMan.instance.GetDay())} at {Numbers.Fixed(EnvMan.instance.GetDayFraction() * 24f, 2)} h" : "no day";
            var where = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}, {when}, at {Numbers.Fixed(at.x, 1)} {Numbers.Fixed(at.y, 1)} {Numbers.Fixed(at.z, 1)}";
            string line;
            if (place != null)
            {
                var from = Utils.DistanceXZ(place.transform.position, at);
                line = $"{where}: in {name}, {Numbers.Fixed(from, 1)} m from its middle, which reaches {Numbers.Fixed(place.GetMaxRadius(), 1)} m{(place.m_hasInterior ? " (it has an inside; " + (player.InInterior() ? "inside it" : "at its entrance") + ")" : "")}; the book holds it {level}";
            }
            else
            {
                line = nearest == null ? $"{where}: in no place, none loaded"
                    : $"{where}: in no place; the nearest, {Utils.GetPrefabName(nearest.gameObject)}, {Numbers.Fixed(away, 1)} m from its middle, reaches {Numbers.Fixed(nearest.GetMaxRadius(), 1)} m{(away < nearest.GetMaxRadius() ? " (within its reach: the game names no place here)" : "")}";
            }
            File.AppendAllText(WhereFile, line + System.Environment.NewLine);
        }

        /// <summary>The location standing in the world whose middle stands nearest a point across, and how far.</summary>
        private static Location Nearest(Vector3 at, out float away)
        {
            away = float.MaxValue;
            Location nearest = null;
            foreach (var location in Object.FindObjectsByType<Location>(FindObjectsSortMode.None))
            {
                if (location == null) continue;
                var d = Utils.DistanceXZ(location.transform.position, at);
                if (d >= away) continue;
                away = d;
                nearest = location;
            }
            return nearest;
        }
    }
}
