using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The stage's sea: over the ocean's ground the water holds what the game holds in it (<see cref="SeaHold"/>).
    /// A creature that swims keeps its swim depth under the surface, its animator told it is in
    /// water (<see cref="KeepMotion"/>), the person beside it swimming too; a Leviathan rides the surface; the sea under
    /// them is as deep as the world places them, and the camera frames what shows over the
    /// surface, so it looks down onto open water. What the sea does not hold stands on its floor
    /// in the shallows.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>How the sea holds what is shown, read as it is shown.</summary>
        private static SeaHold _sea;

        /// <summary>Whether the sea's ground lies under what is shown this frame.</summary>
        private static bool _seaShown;

        /// <summary>A fault in the sea is told once and lets go of it, what is shown standing as on any ground.</summary>
        private const string SeaPart = "holding what is shown in the sea";

        /// <summary>Lets go of how the sea holds what is shown, after a fault.</summary>
        private static void ForgetSea()
        {
            _sea = SeaHold.Not;
            _seaShown = false;
        }

        /// <summary>Whether the sea holds what is shown this frame.</summary>
        private static bool HeldInSea => _seaShown && _sea.Held != SeaHeld.Not;

        /// <summary>The height of the sea's surface on the stage while it holds what is shown, over its root as the game keeps it.</summary>
        private static float SurfaceY => Origin.y + _sea.SurfaceOverRoot * _scale;

        /// <summary>The height of the sea's surface as if at size one, which the framing takes in what shows over.</summary>
        private static float SurfaceUnscaled => Origin.y + _sea.SurfaceOverRoot;

        /// <summary>For the self-test: how the sea holds what is shown, and whether its ground lies under it.</summary>
        [ForTest]
        public static (SeaHeld Held, bool Shown) SeaHolds => (_sea.Held, _seaShown);

        /// <summary>For the self-test: the sea's surface and floor on the stage, NaN where no sea lies under what is shown.</summary>
        [ForTest]
        public static (float Surface, float Floor) SeaLevels => _seaLevels;

        private static (float Surface, float Floor) _seaLevels = (float.NaN, float.NaN);

        /// <summary>How deep the water over the sea's floor is this frame (<see cref="SeaGround"/>); null for ground in the open.</summary>
        private static float? _waterOver;

        /// <summary>
        /// Reads how the sea holds a copy newly shown: a creature that swims (<c>m_canSwim</c>, not
        /// flying) by its swim depth, something kept at the water's level (<c>Leviathan</c>) by its
        /// float offset, each over the shallowest sea the world places it in; anything else, a
        /// place or the person wearing what is shown not at all.
        /// </summary>
        private static void ReadSea(Entry entry)
        {
            _sea = SeaHold.Not;
            var prefab = entry?.Source as GameObject;
            if (prefab == null || _subjectIsPerson || _subject == null) return;

            var placed = Knowledge.SeaDepth(prefab.name);
            var character = prefab.GetComponent<Character>();
            var leviathan = prefab.GetComponent<Leviathan>();
            if (character != null && character.m_canSwim && !character.m_flying) _sea = SeaHold.Swims(character.m_swimDepth, placed);
            else if (leviathan != null && leviathan.m_alignToWaterLevel) _sea = SeaHold.AtSurface(leviathan.m_floatOffset, placed);
            else if (prefab.GetComponent<Ship>() is Ship ship && ship != null) _sea = ShipHold(prefab, ship, placed);
        }

        /// <summary>
        /// How the sea holds a ship (<see cref="SeaHold.Floats"/>): by its float settings, the
        /// world's gravity and its centre of mass, which Unity works out from its hull's colliders.
        /// </summary>
        private static SeaHold ShipHold(GameObject prefab, Ship ship, float? placed) =>
            SeaHold.Floats(ship.m_waterLevelOffset, ship.m_force, ship.m_forceDistance, ship.m_disableLevel, -Physics.gravity.y, CentreOfMassOverRoot(prefab), placed);

        /// <summary>
        /// How far over its root a prefab's centre of mass lies, as Unity works it out from its
        /// colliders: read once from a copy that keeps its body and colliders, made far off,
        /// held still and taken away at once, so it is never drawn and never falls. None for a
        /// prefab without a body.
        /// </summary>
        private static float CentreOfMassOverRoot(GameObject prefab)
        {
            if (CentresOfMass.TryGetValue(prefab, out var known)) return known;
            var over = 0f;
            var copy = Ghost.Make(prefab, null, new Vector3(0f, -10000f, 0f), Quaternion.identity, falling: true);
            if (copy != null)
            {
                var body = copy.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    over = body.centerOfMass.y;
                }
                copy.SetActive(false);
                Object.Destroy(copy);
            }
            CentresOfMass[prefab] = over;
            return over;
        }

        /// <summary>Prefabs' centres of mass over their roots, worked out once each.</summary>
        private static readonly System.Collections.Generic.Dictionary<GameObject, float> CentresOfMass = new System.Collections.Generic.Dictionary<GameObject, float>();

        /// <summary>For the self-test: how far over its root the shown ship's centre of mass lies; NaN for none worked out.</summary>
        [ForTest]
        public static float ShownCentreOfMass => _lastShown?.Source is GameObject prefab && prefab != null && CentresOfMass.TryGetValue(prefab, out var over) ? over : float.NaN;

        /// <summary>Each frame, once the ground is kept: whether the sea lies under what is shown, the person beside it placed again as it comes or goes; how they move is told after (<see cref="KeepMotion"/>).</summary>
        private static void KeepSea()
        {
            var was = _seaShown;
            _seaShown = TheGround.On && StageGround.WaterOver(TheGround.Biome) != null && _subject != null;
            if (_seaShown != was) PlacePerson();
        }

        /// <summary>
        /// The ground's height under the model this frame, where it and the person beside it stand,
        /// or the sea's floor under what the sea holds, the water's depth over it kept for the ground.
        /// </summary>
        private static float GroundUnderModel()
        {
            var groundY = Mathf.Min(FloorY, _person != null && _person.activeSelf && !HeldInSea ? _personBounds.min.y : FloorY);
            if (!Finite(groundY)) groundY = Origin.y;
            (groundY, _waterOver) = SeaGround(groundY);
            return groundY;
        }

        /// <summary>
        /// The ground's height on the stage and the water's depth over it this frame: under what
        /// the sea holds, its floor as deep under the surface as the sea is (<see cref="SeaHold.Depth"/>);
        /// under anything else, the ground it stands on with the shallows over it; no water where
        /// the sea's ground does not lie.
        /// </summary>
        private static (float GroundY, float? WaterOver) SeaGround(float groundY)
        {
            if (!_seaShown || _subject == null)
            {
                _seaLevels = (float.NaN, float.NaN);
                return (groundY, null);
            }
            if (HeldInSea)
            {
                var depth = _sea.Depth(_sea.SurfaceOverRoot - (_bounds.min.y - Origin.y), _bounds.size.y) * _scale;
                var surface = SurfaceY;
                _seaLevels = (surface, surface - depth);
                return (surface - depth, depth);
            }
            var shallows = SeaHold.Not.Depth(0f, Shown.size.y) * _scale;
            _seaLevels = (groundY + shallows, groundY);
            return (groundY, shallows);
        }

        /// <summary>Where the person's root goes beside what the sea holds: swimming at its own swim depth under the surface, as the game holds a player; null where it stands on the ground.</summary>
        private static float? PersonSwimsAt()
        {
            if (!HeldInSea) return null;
            var person = GamePrefabs.Person.OrNull()?.GetComponent<Character>();
            return SurfaceY - (person != null ? person.m_swimDepth : 0f);
        }
    }
}
