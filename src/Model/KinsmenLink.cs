using System;
using System.Reflection;

namespace Scry
{
    /// <summary>
    /// What a skill counts as on a server running the Kinsmen mod, which counts a character far
    /// ahead of the group in a skill lower than the level kept, before effects add. Kinsmen answers
    /// through <c>Kinsmen.KinsmenApi.TryHolder</c>, asked by name as Scry carries no reference to
    /// it; a game without Kinsmen, or a Kinsmen whose <c>Version</c> is not the one read here or
    /// whose method has another shape, gives no link, and every skill then counts as kept.
    /// </summary>
    internal sealed class KinsmenLink
    {
        /// <summary>Kinsmen's plugin id, its type answering other mods, the method asked and the version of its answers read here.</summary>
        public const string Guid = "isimp.Kinsmen";
        public const string TypeName = "Kinsmen.KinsmenApi";
        public const string MethodName = "TryHolder";
        public const string VersionField = "Version";
        public const int VersionRead = 1;

        /// <summary>The method's shape: the counted level before effects, and the holder's name and level, or no name and the cap for a cap the server set by hand.</summary>
        public delegate bool HolderAsk(int skill, float savedLevel, out float counted, out string holder, out float holderLevel);

        private readonly HolderAsk _ask;

        private KinsmenLink(HolderAsk ask) => _ask = ask;

        /// <summary>
        /// The link to Kinsmen's type answering other mods, given the value of its version field as
        /// read (<c>TypeFields</c> reads fields); none for no type, another version or a method of
        /// another shape.
        /// </summary>
        public static KinsmenLink From(Type api, object version)
        {
            if (api == null || !(version is int read) || read != VersionRead) return null;
            var method = api.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int), typeof(float), typeof(float).MakeByRefType(), typeof(string).MakeByRefType(), typeof(float).MakeByRefType() }, null);
            if (method == null) return null;
            return Delegate.CreateDelegate(typeof(HolderAsk), method, false) is HolderAsk ask ? new KinsmenLink(ask) : null;
        }

        /// <summary>What a skill kept at a level counts as here, and whose level holds it where that is a whole level or more lower.</summary>
        public SkillCount Count(int skill, float savedLevel)
        {
            if (!_ask(skill, savedLevel, out var counted, out var holder, out var holderLevel)) return SkillCount.InFull(savedLevel);
            var held = Math.Floor(counted) < Math.Floor(savedLevel);
            return new SkillCount(counted, held, holder, holderLevel);
        }
    }

    /// <summary>
    /// What a skill counts as before effects add, and, where that is a whole level or more below
    /// the level kept, whose level holds it: a character's name and level, or no name for a cap
    /// the server set by hand.
    /// </summary>
    internal readonly struct SkillCount
    {
        public SkillCount(float counted, bool held, string holder, float holderLevel)
        {
            Counted = counted;
            Held = held;
            Holder = held ? holder : null;
            HolderLevel = held ? holderLevel : 0f;
        }

        /// <summary>A skill counting in full, at the level kept.</summary>
        public static SkillCount InFull(float savedLevel) => new SkillCount(savedLevel, false, null, 0f);

        public float Counted { get; }

        public bool Held { get; }

        public string Holder { get; }

        public float HolderLevel { get; }
    }
}
