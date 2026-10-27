using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the catalog reads of the game for one skill's page, as plain figures: which of the
    /// game's rules use the skill, and the figures each works on. A rule nothing uses is left
    /// out of the page (<see cref="SkillPageWords.Changes"/>).
    /// </summary>
    internal sealed class SkillInputs
    {
        /// <summary>The skill's name in the game's list ("Swords", "WoodCutting"), or a mod's skill's number.</summary>
        public string Name = "";

        /// <summary>What a hit on a creature with each thing training it rises the skill by before its step, from its first attack (<see cref="SkillMath.RisePerHit"/>), by prefab.</summary>
        public readonly Dictionary<string, float> HitRises = new Dictionary<string, float>();

        /// <summary>Attacks of what trains it deal damage, and some cost stamina, eitr or health.</summary>
        public bool Hits;
        public bool Costs;

        /// <summary>Axes hit trees by this skill rather than their own (<c>Attack.m_specialHitSkill</c>).</summary>
        public bool OnTrees;

        /// <summary>The bows' full draw times, each once, and whether drawing drains; the crossbows' reload times.</summary>
        public readonly List<float> DrawTimes = new List<float>();
        public bool DrawDrains;
        public readonly List<float> ReloadTimes = new List<float>();

        /// <summary>The player's own figures: running speed and drain, swimming's drain at no skill and at the top, a dodge's cost, sneaking's drain.</summary>
        public float RunSpeed = 7f, RunDrain = 8f, SwimAtNone = 6f, SwimAtTop = 3f, Dodge = 15f, Sneak = 5f;

        /// <summary>A jump's speed up at no skill (<c>Character.m_jumpForce</c>) and the world's gravity, how much speed it takes away each second; no gravity, no height told.</summary>
        public float JumpForce = 8f, Gravity;

        /// <summary>The seconds the game's physics moves a body in at once; none where not read, the height then told by speed and gravity alone.</summary>
        public float PhysicsStep;

        /// <summary>The fishing float's: stamina a second with a fish on, the share of the pull's drain at the top, the line reeled in a second.</summary>
        public float HookedAtNone = 1f, HookedAtTop = 0.2f, PullAtTop = 0.2f, LineAtNone = 2f, LineAtTop = 6f;

        /// <summary>The fishing float's reach for a fish, and when its line breaks: so far past the line, so far out in all; none where no rod was read.</summary>
        public float FloatRange, LineBreakPast, LineMost;

        /// <summary>Stations that train it make things faster, by the most share saved, with a bonus this likely at the top for what stacks.</summary>
        public bool Crafts;
        public float CraftMostSaved, CraftBonus;

        /// <summary>The bonus at the top for food taken off a cooking station that trains it; nothing for none.</summary>
        public float CookBonus;

        /// <summary>The bonus at the top for picking what trains it; nothing for none.</summary>
        public float PickBonus;

        /// <summary>A building tool's table trains it, so building costs less stamina; the most a tool's wear placing is cut by.</summary>
        public bool BuildStamina;
        public float WearSaved;

        /// <summary>A scythe's reach at no skill and at the top; nothing for none.</summary>
        public float ScytheAtNone, ScytheAtTop;

        /// <summary>A shield this skill's casting gives: what it absorbs and what each level adds; nothing for none.</summary>
        public float ShieldListed, ShieldPerLevel;

        /// <summary>Each staff's summons by this skill: the levels each step starts at and how many it keeps at once; and what each level adds to their hits.</summary>
        public readonly List<(string Staff, int[] Levels, int[] Counts)> Summons = new List<(string, int[], int[])>();
        public float SummonPerLevel;
    }
}
