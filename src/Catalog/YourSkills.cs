using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The character's own skills, read from the game at most once a second while something asks:
    /// each skill's level as the game works with it (<see cref="SkillMath.Level"/>, the one kept
    /// with what effects add), the level kept, its progress to the next, and what effects add.
    /// The game's own <c>Skills.GetSkillLevel</c> is not asked, as it gives the character a skill
    /// it has never trained, which its save then keeps; the levels are worked out as it does from
    /// what the character has (<c>Skills.GetSkillList</c>) and its effects (<c>SEMan.ModifySkillLevel</c>),
    /// every skill of the game's list too, as an effect can lift one never trained. On a server
    /// running Kinsmen a skill far ahead of the group counts lower than the level kept; the level
    /// the game works with is then the one Kinsmen counts, with what effects add
    /// (<see cref="KinsmenLink"/>), and without Kinsmen every skill counts as kept.
    /// </summary>
    internal static class YourSkills
    {
        /// <summary>Leaving a world forgets the character read (<see cref="WorldCaches"/>).</summary>
        static YourSkills() => WorldCaches.Register(nameof(YourSkills), Forget);

        private sealed class Read
        {
            public float Kept;
            public float Progress;
            public float FromEffects;
            public float Level;
            public SkillCount Count;
        }

        /// <summary>Every skill of the game's own list, which effects can lift though never trained.</summary>
        private static readonly Skills.SkillType[] GameSkills = Array.FindAll((Skills.SkillType[])Enum.GetValues(typeof(Skills.SkillType)),
            s => s != Skills.SkillType.None && s != Skills.SkillType.All);

        private static readonly Dictionary<int, Read> ByType = new Dictionary<int, Read>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly HashSet<int> LearnedTypes = new HashSet<int>();
        private static float _readAt = -1f;
        private static int _version;
        private static int _learnedVersion;
        private static bool _known;

        /// <summary>Counts every change of a skill's level as the game works with it, so what is told at the character's levels is told again.</summary>
        public static int Version
        {
            get
            {
                ReadIfDue();
                return _version;
            }
        }

        /// <summary>
        /// Counts every change of which skills the character has learned (<see cref="SkillMath.Learned"/>),
        /// for the compendium (<see cref="CompendiumView.SkillsLearned"/>). A moment without a
        /// character, dead or loading, keeps those last read.
        /// </summary>
        public static int LearnedVersion
        {
            get
            {
                ReadIfDue();
                return _learnedVersion;
            }
        }

        /// <summary>Whether the character has learned a skill, by its number in the game's list, as last read.</summary>
        public static readonly Func<int, bool> Learned = type => LearnedTypes.Contains(type);

        /// <summary>Whether there is a character to read: in a world, not on a server.</summary>
        public static bool Known
        {
            get
            {
                ReadIfDue();
                return _known;
            }
        }

        /// <summary>A skill's level as the game works with it: the one kept with what effects add, whole.</summary>
        public static float LevelOf(Skills.SkillType skill)
        {
            ReadIfDue();
            return ByType.TryGetValue((int)skill, out var read) ? read.Level : 0f;
        }

        /// <summary>What a skill counts as before effects add, and whose level holds it where the server counts it a whole level or more lower than kept (<see cref="KinsmenLink"/>).</summary>
        public static SkillCount CountOf(Skills.SkillType skill)
        {
            ReadIfDue();
            return ByType.TryGetValue((int)skill, out var read) ? read.Count : SkillCount.InFull(0f);
        }

        /// <summary>The level the character keeps of a skill, what effects add to it now, and its progress to the next level.</summary>
        public static (float Kept, float FromEffects, float Progress) Detail(Skills.SkillType skill)
        {
            ReadIfDue();
            return ByType.TryGetValue((int)skill, out var read) ? (read.Kept, read.FromEffects, read.Progress) : (0f, 0f, 0f);
        }

        /// <summary>
        /// The You box's lines on a skill's page: the character's level, with what effects add,
        /// and how far the next is, with about how much of what raises it is left, rested too
        /// (<c>Player.RaiseSkill</c>: the Rested effect's share added to each rise). A skill the
        /// server counts lower tells the level counted here beside the one kept, and whose level
        /// holds it. None without a character.
        /// </summary>
        public static List<(string Label, string Value)> Lines(SkillSource skill)
        {
            var lines = new List<(string, string)>();
            if (!Known) return lines;
            var type = (Skills.SkillType)skill.Type;
            var (kept, fromEffects, progress) = Detail(type);
            var level = LevelOf(type);
            var count = CountOf(type);
            lines.Add((SkillPageWords.LevelLabel, SkillPageWords.Level(level, fromEffects, count.Held ? kept : (float?)null)));
            if (count.Held) lines.Add((SkillPageWords.HeldByLabel, SkillPageWords.HeldBy(Facts.SkillName(type), count.Holder, count.HolderLevel)));
            var next = SkillPageWords.ToNext(skill.Name, kept, progress, skill.Step, 1f + RestedShare(), Held(type));
            if (next != null) lines.Add((SkillPageWords.ToNextLabel(kept), next));
            return lines;
        }

        /// <summary>What a skill's card on the stage says: the character's level, and how far the next is; nothing more without a character.</summary>
        public static (string Level, string Next) Card(SkillSource skill)
        {
            var type = (Skills.SkillType)skill.Type;
            var (kept, fromEffects, progress) = Detail(type);
            if (!Known) return (SkillPageWords.Card(0f, 0f), null);
            var count = CountOf(type);
            return (SkillPageWords.Card(LevelOf(type), fromEffects, count.Held ? kept : (float?)null), SkillPageWords.ToNext(skill.Name, kept, progress, skill.Step, 1f + RestedShare(), Held(type)));
        }

        /// <summary>The weapon in the character's hand where it trains a skill, by name, with what a hit with its first attack rises the skill by (<see cref="SkillMath.RisePerHit"/>); none otherwise.</summary>
        private static (string Weapon, float PerHit)? Held(Skills.SkillType skill)
        {
            var weapon = Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentWeapon() : null;
            var shared = weapon?.m_shared;
            if (shared?.m_attack == null || shared.m_skillType != skill) return null;
            return (CatalogBuilder.Localize(shared.m_name), SkillMath.RisePerHit(shared.m_attack.m_attackType.ToString(), shared.m_attack.m_raiseSkillAmount));
        }

        /// <summary>How much more each rise gives while rested: the Rested effect's own share for every skill (<c>SE_Stats.ModifyRaiseSkill</c>).</summary>
        private static float RestedShare()
        {
            var rested = ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectRested) as SE_Stats : null;
            return rested != null && rested.m_raiseSkill == Skills.SkillType.All ? rested.m_raiseSkillModifier : 0f;
        }

        /// <summary>Reads the character's skills again once a second has passed since the last read.</summary>
        private static void ReadIfDue()
        {
            if (_readAt >= 0f && Time.unscaledTime - _readAt < 1f) return;
            _readAt = Time.unscaledTime;
            Guard.Run(Feature.YourSkillFigures, "reading your skills", ReadNowStep, "your skills read");
        }

        /// <summary>The read as a step, made once rather than at every read.</summary>
        private static readonly Action ReadNowStep = ReadNow;

        private static void ReadNow()
        {
            var player = Player.m_localPlayer;
            var skills = player != null ? player.GetSkills() : null;
            var effects = player != null ? player.GetSEMan() : null;
            var changed = _known != (skills != null);
            _known = skills != null;
            Seen.Clear();
            if (skills != null)
            {
                foreach (var skill in skills.GetSkillList())
                {
                    if (skill?.m_info == null) continue;
                    changed |= Note(skill.m_info.m_skill, skill.m_level, skill.m_accumulator, effects);
                }
                foreach (var skill in GameSkills)
                {
                    if (!Seen.Contains((int)skill)) changed |= Note(skill, 0f, 0f, effects);
                }
            }
            Gone.Clear();
            foreach (var key in ByType.Keys) if (!Seen.Contains(key)) Gone.Add(key);
            foreach (var key in Gone) ByType.Remove(key);
            if (changed || Gone.Count > 0) _version++;
            if (skills != null) NoteLearned();
        }

        /// <summary>Keeps which skills the character has learned, counting a change.</summary>
        private static void NoteLearned()
        {
            var moved = false;
            foreach (var pair in ByType)
            {
                var learned = SkillMath.Learned(pair.Value.Kept);
                if (learned == LearnedTypes.Contains(pair.Key)) continue;
                if (learned) LearnedTypes.Add(pair.Key);
                else LearnedTypes.Remove(pair.Key);
                moved = true;
            }
            if (moved) _learnedVersion++;
        }

        /// <summary>
        /// Keeps one skill as read, with what it counts as here and what the character's effects
        /// add to it now; true when its level or whose level holds it changed.
        /// </summary>
        private static bool Note(Skills.SkillType type, float kept, float progress, SEMan effects)
        {
            var key = (int)type;
            Seen.Add(key);
            var fromEffects = 0f;
            effects?.ModifySkillLevel(type, ref fromEffects);
            var count = Counted(key, kept);
            var level = SkillMath.Level(count.Counted, fromEffects);
            var changed = false;
            if (!ByType.TryGetValue(key, out var read))
            {
                ByType[key] = read = new Read();
                changed = true;
            }
            if (read.Level != level || read.Count.Held != count.Held || read.Count.Holder != count.Holder
                || Math.Floor(read.Count.HolderLevel) != Math.Floor(count.HolderLevel)) changed = true;
            read.Kept = kept;
            read.Progress = progress;
            read.FromEffects = fromEffects;
            read.Level = level;
            read.Count = count;
            return changed;
        }

        /// <summary>Kinsmen's answers, found once a world; none without it or once asking it failed there.</summary>
        private static KinsmenLink _kinsmen;
        private static bool _kinsmenLooked;

        /// <summary>The link to Kinsmen, looked for the first time a character's skills are read in a world.</summary>
        private static KinsmenLink Kinsmen
        {
            get
            {
                if (_kinsmenLooked) return _kinsmen;
                _kinsmenLooked = true;
                Guard.Run(Feature.KinsmenCounting, "finding Kinsmen", FindKinsmen, out _kinsmen);
                return _kinsmen;
            }
        }

        /// <summary>
        /// Kinsmen's type answering other mods, from the assembly of the Kinsmen loaded, with its
        /// version; none where Kinsmen is not loaded or answers another way. Looks again each
        /// time, for the startup check.
        /// </summary>
        public static readonly Func<KinsmenLink> FindKinsmen = () =>
        {
            if (!Chainloader.PluginInfos.TryGetValue(KinsmenLink.Guid, out var info) || info?.Instance == null) return null;
            var api = info.Instance.GetType().Assembly.GetType(KinsmenLink.TypeName, false);
            var version = TypeFields.StaticNamed(api, KinsmenLink.VersionField);
            return version != null ? KinsmenLink.From(api, TypeFields.Value(version, null)) : null;
        };

        /// <summary>Whether Kinsmen is loaded, for the startup check to ask after its answers only then.</summary>
        public static bool KinsmenLoaded => Chainloader.PluginInfos.ContainsKey(KinsmenLink.Guid);

        /// <summary>Whether Scry asks Kinsmen what the character's skills count as, for the self-test.</summary>
        [ForTest]
        public static bool KinsmenLinkedForTest => Kinsmen != null;

        /// <summary>The question put to Kinsmen, kept so asking makes nothing new.</summary>
        private sealed class Asking
        {
            public KinsmenLink Link;
            public int Skill;
            public float Kept;
            public SkillCount Count;
        }

        private static readonly Asking Ask = new Asking();
        private static readonly Action<Asking> AskStep = ask => ask.Count = ask.Link.Count(ask.Skill, ask.Kept);

        /// <summary>What a skill kept at a level counts as here: as kept without Kinsmen, and from then on once asking it failed.</summary>
        private static SkillCount Counted(int skill, float kept)
        {
            var link = Kinsmen;
            if (link == null) return SkillCount.InFull(kept);
            Ask.Link = link;
            Ask.Skill = skill;
            Ask.Kept = kept;
            Ask.Count = SkillCount.InFull(kept);
            if (Guard.Run(Feature.KinsmenCounting, "asking Kinsmen what your skills count as", AskStep, Ask)) return Ask.Count;
            _kinsmen = null;
            return SkillCount.InFull(kept);
        }

        /// <summary>Forgets the character read, for another world.</summary>
        public static void Forget()
        {
            ByType.Clear();
            LearnedTypes.Clear();
            Seen.Clear();
            Gone.Clear();
            _readAt = -1f;
            _known = false;
            _kinsmen = null;
            _kinsmenLooked = false;
            _version++;
            _learnedVersion++;
        }
    }
}
