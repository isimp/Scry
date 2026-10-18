using System;
using System.Collections.Generic;
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
    /// every skill of the game's list too, as an effect can lift one never trained.
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
        }

        /// <summary>Every skill of the game's own list, which effects can lift though never trained.</summary>
        private static readonly Skills.SkillType[] GameSkills = Array.FindAll((Skills.SkillType[])Enum.GetValues(typeof(Skills.SkillType)),
            s => s != Skills.SkillType.None && s != Skills.SkillType.All);

        private static readonly Dictionary<int, Read> ByType = new Dictionary<int, Read>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly List<int> Gone = new List<int>();
        private static float _readAt = -1f;
        private static int _version;
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

        /// <summary>The level the character keeps of a skill, what effects add to it now, and its progress to the next level.</summary>
        public static (float Kept, float FromEffects, float Progress) Detail(Skills.SkillType skill)
        {
            ReadIfDue();
            return ByType.TryGetValue((int)skill, out var read) ? (read.Kept, read.FromEffects, read.Progress) : (0f, 0f, 0f);
        }

        /// <summary>
        /// The You box's lines on a skill's page: the character's level, with what effects add,
        /// and how far the next is, with about how much of what raises it is left, rested too
        /// (<c>Player.RaiseSkill</c>: the Rested effect's share added to each rise). None
        /// without a character.
        /// </summary>
        public static List<(string Label, string Value)> Lines(SkillSource skill)
        {
            var lines = new List<(string, string)>();
            if (!Known) return lines;
            var type = (Skills.SkillType)skill.Type;
            var (kept, fromEffects, progress) = Detail(type);
            var level = LevelOf(type);
            lines.Add((SkillPageWords.LevelLabel, SkillPageWords.Level(level, fromEffects)));
            var next = SkillPageWords.ToNext(skill.Name, kept, progress, skill.Step, 1f + RestedShare());
            if (next != null) lines.Add((SkillPageWords.ToNextLabel(kept), next));
            return lines;
        }

        /// <summary>What a skill's card on the stage says: the character's level, and how far the next is; nothing more without a character.</summary>
        public static (string Level, string Next) Card(SkillSource skill)
        {
            var type = (Skills.SkillType)skill.Type;
            var (kept, fromEffects, progress) = Detail(type);
            if (!Known) return (SkillPageWords.Card(0f, 0f), null);
            return (SkillPageWords.Card(LevelOf(type), fromEffects), SkillPageWords.ToNext(skill.Name, kept, progress, skill.Step, 1f + RestedShare()));
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
        }

        /// <summary>Keeps one skill as read, with what the character's effects add to it now; true when its level changed.</summary>
        private static bool Note(Skills.SkillType type, float kept, float progress, SEMan effects)
        {
            var key = (int)type;
            Seen.Add(key);
            var fromEffects = 0f;
            effects?.ModifySkillLevel(type, ref fromEffects);
            var level = SkillMath.Level(kept, fromEffects);
            var changed = false;
            if (!ByType.TryGetValue(key, out var read))
            {
                ByType[key] = read = new Read();
                changed = true;
            }
            if (read.Level != level) changed = true;
            read.Kept = kept;
            read.Progress = progress;
            read.FromEffects = fromEffects;
            read.Level = level;
            return changed;
        }

        /// <summary>Forgets the character read, for another world.</summary>
        public static void Forget()
        {
            ByType.Clear();
            _readAt = -1f;
            _known = false;
            _version++;
        }
    }
}
