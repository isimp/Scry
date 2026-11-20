using System;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Scry
{
    /// <summary>
    /// AdjustablePortals' map of portals to choose from (<see cref="PortalMap"/>): with its
    /// *PortalTargetingMode* at SelectOnMap, a player stepping into a portal gets the large map with
    /// a pin on every portal to travel to, and the mod keeps <c>PortalMapPicker.selecting</c> set
    /// until the map closes, taking every click on the map meanwhile. Read by name, as Scry carries
    /// no reference to the mod; with it not loaded, or the member gone, nothing steps aside.
    /// </summary>
    internal static class AdjustablePortalsMap
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static AdjustablePortalsMap() => WorldCaches.Register(nameof(AdjustablePortalsMap), Forget);

        /// <summary>Lets go of the member found and what was last read, looked for again in the next world.</summary>
        public static void Forget()
        {
            _looked = false;
            _picking = null;
            _askedIn = -1;
            _open = false;
            _pickingInstead = null;
        }

        /// <summary>AdjustablePortals' plugin id.</summary>
        public const string Guid = "MidnightsFX.AdjustablePortals";

        /// <summary>The member read, by type and name, for the startup check.</summary>
        public const string PickerType = "AdjustablePortals.ui.PortalMapPicker", PickingField = "selecting";

        private static bool _looked;
        private static Func<bool> _picking;

        /// <summary>The frame the picker was last read in, and what it read, as each layer asks every frame.</summary>
        private static int _askedIn = -1;
        private static bool _open;

        /// <summary>Whether AdjustablePortals' map picker is open now, read once a frame.</summary>
        public static bool Picking
        {
            get
            {
                var frame = UnityEngine.Time.frameCount;
                if (frame == _askedIn) return _open;
                _askedIn = frame;
                // A failure reading the mod is its own part's, not the marks', which then stay.
                _open = Guard.Run(Feature.MarksAsideOnPortalMap, "reading AdjustablePortals' map of portals", ReadPicking, out var open) && open;
                return _open;
            }
        }

        /// <summary>Whether the picker is open, read from AdjustablePortals now, or as the self-test stands in for it.</summary>
        private static readonly Func<bool> ReadPicking = () =>
        {
            Look();
            return _pickingInstead ?? PickingNow();
        };

        /// <summary>Whether the picker is open now, by the mod's own flag; false where it cannot be read.</summary>
        private static bool PickingNow() => _picking != null && _picking();

        /// <summary>Whether the picker is open now by the mod's own flag, for the self-test to see the flag is read.</summary>
        [ForTest]
        public static bool PickingForTest
        {
            get
            {
                Look();
                return PickingNow();
            }
        }

        /// <summary>A picker open or not in place of the mod's own flag, as the self-test stands in for a portal; none reads the flag.</summary>
        private static bool? _pickingInstead;

        /// <summary>Puts a picker open or not in place of the mod's own flag, as stepping into a portal sets it, for the self-test; none reads the flag again.</summary>
        [ForTest]
        public static void PickingInsteadForTest(bool? picking)
        {
            _pickingInstead = picking;
            _askedIn = -1;
        }

        /// <summary>Whether AdjustablePortals is loaded, for the startup check to ask after its member only then.</summary>
        public static bool Loaded => Chainloader.PluginInfos.ContainsKey(Guid);

        /// <summary>A static field of AdjustablePortals by its type and name; none where the mod or the field is not there.</summary>
        public static FieldInfo Member(string type, string name)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info == null || info.Instance == null) return null;
            // Looked up in the mod's own assembly, not by the name across every loaded mod's types.
            return TypeFields.StaticNamed(info.Instance.GetType().Assembly.GetType(type, false), name);
        }

        /// <summary>Finds the flag once, made a getter that reads it without boxing it each frame.</summary>
        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            var picking = Member(PickerType, PickingField);
            if (picking != null && picking.FieldType == typeof(bool)) _picking = Expression.Lambda<Func<bool>>(Expression.Field(null, picking)).Compile();
        }
    }
}
