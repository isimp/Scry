using System;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// TargetPortal's map of portals to choose from (<see cref="PortalMap"/>): while a player stands
    /// in a portal, the mod opens the large map and keeps <c>TargetPortal.Map.Teleporting</c> set
    /// until the map closes, and its *Hide map pins* (<c>TargetPortal.hidePinsDuringPortal</c>)
    /// hides the map's pins there. Read by name, as Scry carries no reference to the mod; with it
    /// not loaded, or either member gone, nothing steps aside.
    /// </summary>
    internal static class TargetPortalMap
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static TargetPortalMap() => WorldCaches.Register(nameof(TargetPortalMap), Forget);

        /// <summary>Lets go of the members found and what was last read, looked for again in the next world.</summary>
        public static void Forget()
        {
            _looked = false;
            _choosing = null;
            _hidesPins = null;
            _askedIn = -1;
            _aside = false;
            _choosingInstead = null;
        }

        /// <summary>TargetPortal's plugin id.</summary>
        public const string Guid = "org.bepinex.plugins.targetportal";

        /// <summary>The members read, by type and name, for the startup check.</summary>
        public const string MapType = "TargetPortal.Map", ChoosingField = "Teleporting";
        public const string PluginType = "TargetPortal.TargetPortal", HidesPinsField = "hidePinsDuringPortal";

        private static bool _looked;
        private static Func<bool> _choosing;
        private static FieldInfo _hidesPins;

        /// <summary>The frame the marks' standing was last worked out in, and what it was, as each layer asks every frame.</summary>
        private static int _askedIn = -1;
        private static bool _aside;

        /// <summary>
        /// Whether Scry's marks step aside on the large map now: TargetPortal's map of portals is
        /// open and its *Hide map pins* is on. The switch is read only while the map is open for a
        /// portal, once a frame.
        /// </summary>
        public static bool MarksAside
        {
            get
            {
                var frame = UnityEngine.Time.frameCount;
                if (frame == _askedIn) return _aside;
                _askedIn = frame;
                // A failure reading the mod is its own part's, not the marks', which then stay.
                _aside = Guard.Run(Feature.MarksAsideOnPortalMap, "reading TargetPortal's map of portals", ReadAside, out var aside) && aside;
                return _aside;
            }
        }

        /// <summary>Whether the marks step aside, read from TargetPortal now.</summary>
        private static readonly Func<bool> ReadAside = () =>
        {
            Look();
            var choosing = _choosingInstead ?? ChoosingNow();
            return PortalMap.MarksAside(choosing, choosing && PortalMap.HidesPins(HidesPinsWord()));
        };

        /// <summary>Whether TargetPortal's map of portals is open now, by its own flag; false where it cannot be read.</summary>
        private static bool ChoosingNow() => _choosing != null && _choosing();

        /// <summary>Whether TargetPortal's map of portals is open now, for the self-test to see the flag is read.</summary>
        [ForTest]
        public static bool ChoosingForTest
        {
            get
            {
                Look();
                return ChoosingNow();
            }
        }

        /// <summary>A map of portals open or not in place of TargetPortal's own flag, as the self-test stands in for a portal; none reads the flag.</summary>
        private static bool? _choosingInstead;

        /// <summary>Puts a map of portals open or not in place of TargetPortal's own flag, as standing in a portal sets it, for the self-test; none reads the flag again.</summary>
        [ForTest]
        public static void ChoosingInsteadForTest(bool? choosing)
        {
            _choosingInstead = choosing;
            _askedIn = -1;
        }

        /// <summary>Whether TargetPortal is loaded, for the startup check to ask after its members only then.</summary>
        public static bool Loaded => Chainloader.PluginInfos.ContainsKey(Guid);

        /// <summary>A member of TargetPortal by its type and name, static; none where the mod or the member is not there.</summary>
        public static FieldInfo Member(string type, string name)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info == null || info.Instance == null) return null;
            // Looked up in TargetPortal's own assembly: a search by the name alone goes through every
            // loaded mod's types, which held the large map's first frame for a good part of a second.
            return TypeFields.StaticNamed(info.Instance.GetType().Assembly.GetType(type, false), name);
        }

        /// <summary>Finds the members once, the flag made a getter that reads it without boxing it each frame.</summary>
        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            var choosing = Member(MapType, ChoosingField);
            if (choosing != null && choosing.FieldType == typeof(bool)) _choosing = Expression.Lambda<Func<bool>>(Expression.Field(null, choosing)).Compile();
            var hides = Member(PluginType, HidesPinsField);
            if (hides != null && typeof(ConfigEntryBase).IsAssignableFrom(hides.FieldType)) _hidesPins = hides;
        }

        /// <summary>Whether TargetPortal's *Hide map pins* hides the pins now, for the self-test to tell what the marks should do.</summary>
        [ForTest]
        public static bool HidesPinsForTest
        {
            get
            {
                Look();
                return PortalMap.HidesPins(HidesPinsWord());
            }
        }

        /// <summary>The word *Hide map pins* reads now ("On" or "Off"); none where it is not there.</summary>
        private static string HidesPinsWord() => _hidesPins != null ? (TypeFields.Value(_hidesPins, null) as ConfigEntryBase)?.BoxedValue?.ToString() : null;
    }
}
