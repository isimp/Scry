using System.Collections.Generic;

namespace Scry
{
    /// <summary>A component on a preview copy, described by what decides whether it stays.</summary>
    internal struct ComponentFacts
    {
        /// <summary>Full type name, e.g. <c>UnityEngine.MeshRenderer</c> or <c>Character</c>.</summary>
        public string TypeName;

        /// <summary>A script (MonoBehaviour) rather than a built-in engine component.</summary>
        public bool IsScript;

        /// <summary>A physics joint, which has to go before the body it holds on to.</summary>
        public bool IsJoint;

        public ComponentFacts(string typeName, bool isScript = false, bool isJoint = false)
        {
            TypeName = typeName;
            IsScript = isScript;
            IsJoint = isJoint;
        }
    }

    /// <summary>
    /// What a copy made only to be looked at keeps. A copy has no network view and nothing in the
    /// world knows it exists, so anything that could be hit, picked up, fought, interacted with or
    /// saved has to go. Kept is a short list of what draws, animates, lights or sounds; everything
    /// else is removed, including any script a mod adds, since its needs cannot be known.
    /// </summary>
    internal static class StripPolicy
    {
        /// <summary>Kept on the copy.</summary>
        public const int Keep = 0;

        /// <summary>Removal passes, in order: scripts, then joints, then everything else.</summary>
        public const int ScriptsPass = 1;
        public const int JointsPass = 2;
        public const int RestPass = 3;

        /// <summary>Engine components that only draw, animate, light or sound.</summary>
        private static readonly HashSet<string> KeptEngine = new HashSet<string>
        {
            "UnityEngine.Transform",
            "UnityEngine.RectTransform",
            "UnityEngine.MeshFilter",
            "UnityEngine.MeshRenderer",
            "UnityEngine.SkinnedMeshRenderer",
            "UnityEngine.SpriteRenderer",
            "UnityEngine.BillboardRenderer",
            "UnityEngine.LODGroup",
            "UnityEngine.Animator",
            "UnityEngine.Animation",
            "UnityEngine.ParticleSystem",
            "UnityEngine.ParticleSystemRenderer",
            "UnityEngine.TrailRenderer",
            "UnityEngine.LineRenderer",
            "UnityEngine.Light",
            "UnityEngine.AudioSource",
            "UnityEngine.AudioLowPassFilter",
            "UnityEngine.AudioReverbFilter",
            "UnityEngine.Cloth",
            "UnityEngine.Projector",
        };

        /// <summary>
        /// The game's own scripts that only make an effect look, sound or end as it should. Each
        /// was read in the game's code and needs neither a network view nor a character: they flicker
        /// a light, play a random clip, destroy the object after a while, fade or face the camera.
        /// </summary>
        private static readonly HashSet<string> KeptScripts = new HashSet<string>
        {
            "LightFlicker",
            "ZSFX",
            "TimedDestruction",
            "EffectFade",
            "Billboard",
            "LodFadeInOut",
            "ParticleDecal",
        };

        /// <summary>
        /// The cloth physics the game ships (MagicaCloth 2), which moves Gjall's tentacles, a
        /// Wolf's body, the Ulv's and Neck's tails, a Seeker's jiggling bones, Hildir's skirt and a
        /// ship's sail as their prefabs carry it: the cloth and the shapes it rests against. It
        /// builds itself as the copy wakes, needs no network view, and on a copy simulates always,
        /// as it follows the animator's culling, which copies set to animate always; no prefab of
        /// the game culls it by distance. Its wind zone and settings act on the whole world's cloth and go.
        /// </summary>
        private static readonly HashSet<string> KeptCloth = new HashSet<string>
        {
            "MagicaCloth2.MagicaCloth",
            "MagicaCloth2.MagicaCapsuleCollider",
            "MagicaCloth2.MagicaSphereCollider",
            "MagicaCloth2.MagicaPlaneCollider",
        };

        /// <summary>
        /// What a copy that falls keeps as well: its bodies, their colliders and the joints that
        /// hold them together, so a ragdoll slumps and the parts of a broken piece tumble. Where
        /// such a copy stands, its colliders only meet the ground, never a player or a creature.
        /// </summary>
        private static readonly HashSet<string> KeptFalling = new HashSet<string>
        {
            "UnityEngine.Rigidbody",
            "UnityEngine.BoxCollider",
            "UnityEngine.SphereCollider",
            "UnityEngine.CapsuleCollider",
            "UnityEngine.MeshCollider",
        };

        /// <summary>
        /// How fast the game's scripts let their body be pushed out of whatever it overlaps, set
        /// on waking (<c>ItemDrop</c>, <c>TreeLog</c> and the rest read in the game's code). A copy
        /// loses the script, so without this a log's halves or the items that fall out, made
        /// inside one another, would fly apart.
        /// </summary>
        private static readonly Dictionary<string, float> PushLimits = new Dictionary<string, float>
        {
            { "ItemDrop", 1f },
            { "TreeLog", 1f },
            { "TombStone", 1f },
            { "Smoke", 1f },
            { "Character", 2f },
            { "Humanoid", 2f },
            { "Player", 2f },
            { "Ship", 2f },
            { "Vagon", 2f },
        };

        /// <summary>
        /// The most a falling copy's own body may be pushed apart from what it overlaps, by the
        /// scripts its prefab has, the gentler of two; none where no script sets one.
        /// </summary>
        public static float? PushApartLimit(IEnumerable<string> scriptNames)
        {
            float? limit = null;
            foreach (var name in scriptNames)
            {
                if (name != null && PushLimits.TryGetValue(name, out var each) && (limit == null || each < limit)) limit = each;
            }
            return limit;
        }

        /// <summary>The game's scripts copies keep, by name.</summary>
        public static IEnumerable<string> KeptScriptNames => KeptScripts;

        /// <summary>The cloth physics' scripts copies keep, by full name.</summary>
        public static IEnumerable<string> KeptClothNames => KeptCloth;

        /// <summary>
        /// Whether a component stays, and if not, in which pass it goes. A part copied to be
        /// <paramref name="skinned"/> onto a body loses its cloth: the game binds worn cloth to the
        /// wearer's bones and the shapes it rests against as it puts it on (<c>VisEquipment.SetupCloth</c>),
        /// and built without that it would follow bones the part no longer moves with.
        /// </summary>
        public static int PassFor(ComponentFacts facts, bool falling = false, bool skinned = false)
        {
            var name = facts.TypeName ?? "";

            if (facts.IsScript) return KeptScripts.Contains(name) || !skinned && KeptCloth.Contains(name) ? Keep : ScriptsPass;
            if (facts.IsJoint) return falling ? Keep : JointsPass;
            if (falling && KeptFalling.Contains(name)) return Keep;
            return KeptEngine.Contains(name) ? Keep : RestPass;
        }
    }
}
