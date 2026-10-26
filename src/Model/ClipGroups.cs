using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a clip is for, as the clip list groups it, in the order the groups are listed.</summary>
    internal enum ClipGroup
    {
        Attacks,
        Moving,
        Blocking,
        Airborne,
        Hurt,
        Emotes,
        Resting,
        Working,
        Other,
    }

    /// <summary>
    /// Groups a selection's clips by what the game sends its animator there for: an attack; moving,
    /// crouching or laden in the stance it holds; blocking; jumping, falling or swimming; getting
    /// hurt or dying; an emote; riding, sitting or lying down; working at a station, eating or
    /// equipping. The stance sweep's runs tell the first few, every switch and trigger of the
    /// animator pulled once on the hidden copy the rest, each by its name. An attack stays an
    /// attack and what it moves in stays moving, whatever else also leads there.
    /// </summary>
    internal static class ClipGroups
    {
        /// <summary>The sweep's runs, by what was done: standing, crouching and laden in each stance; blocking; jumping.</summary>
        public const string MoveVisit = "move";
        public const string BlockVisit = "block";
        public const string JumpVisit = "jump";

        /// <summary>The switches and triggers sorted by their whole name.</summary>
        private static readonly Dictionary<string, ClipGroup> Named = new Dictionary<string, ClipGroup>(StringComparer.Ordinal)
        {
            ["lying_down"] = ClipGroup.Resting, ["sleep"] = ClipGroup.Resting, ["sleeping"] = ClipGroup.Resting, ["wake"] = ClipGroup.Resting, ["wakeup"] = ClipGroup.Resting,
            ["crafting"] = ClipGroup.Working, ["interact"] = ClipGroup.Working, ["eat"] = ClipGroup.Working, ["consume"] = ClipGroup.Working, ["gpower"] = ClipGroup.Working,
            ["equipping"] = ClipGroup.Working, ["place_feast"] = ClipGroup.Working,
            ["stagger"] = ClipGroup.Hurt, ["dodge"] = ClipGroup.Hurt, ["knockdown"] = ClipGroup.Hurt, ["dead"] = ClipGroup.Hurt,
            ["jump"] = ClipGroup.Airborne, ["falling"] = ClipGroup.Airborne, ["onGround"] = ClipGroup.Airborne, ["slipping"] = ClipGroup.Airborne, ["inWater"] = ClipGroup.Airborne,
            ["water"] = ClipGroup.Airborne, ["swim"] = ClipGroup.Airborne, ["grappling"] = ClipGroup.Airborne, ["repell"] = ClipGroup.Airborne,
            ["fly"] = ClipGroup.Moving, ["flying"] = ClipGroup.Moving, ["crouching"] = ClipGroup.Moving, ["encumbered"] = ClipGroup.Moving,
        };

        /// <summary>The switches and triggers sorted by how their name begins: "emote_wave", "attach_chair", "equip_hip", "death1".</summary>
        private static readonly (string Start, ClipGroup Group)[] Started =
        {
            ("emote_", ClipGroup.Emotes), ("attach_", ClipGroup.Resting), ("equip_", ClipGroup.Working), ("unequip_", ClipGroup.Working), ("death", ClipGroup.Hurt),
        };

        /// <summary>The group a switch or trigger sends its clips to, by its name; null for one of no known use.</summary>
        public static ClipGroup? OfSwitch(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Named.TryGetValue(name, out var group)) return group;
            foreach (var (start, startedGroup) in Started) if (name.StartsWith(start, StringComparison.Ordinal)) return startedGroup;
            return null;
        }

        /// <summary>The action a clip found by its name goes with (<see cref="ClipByName"/>): a jump where its name says so, else swimming.</summary>
        public static string ByName(string clip) => clip != null && clip.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0 ? "jump" : "swim";

        /// <summary>The groups switches send clips to before others, where two lead to one clip.</summary>
        private static readonly ClipGroup[] SwitchOrder = { ClipGroup.Hurt, ClipGroup.Airborne, ClipGroup.Resting, ClipGroup.Working, ClipGroup.Emotes, ClipGroup.Moving };

        /// <param name="clips">The clips to group.</param>
        /// <param name="attacks">The clips its attacks play.</param>
        /// <param name="visits">The clips seen in each of the sweep's runs (<see cref="MoveVisit"/>, <see cref="BlockVisit"/>, <see cref="JumpVisit"/>); null where none was swept.</param>
        /// <param name="switched">The clips each switch or trigger was seen to lead to, by its name; null where none was pulled.</param>
        /// <param name="idles">The clips it plays left alone.</param>
        public static Dictionary<string, ClipGroup> Sort(IEnumerable<string> clips, ICollection<string> attacks, IReadOnlyDictionary<string, HashSet<string>> visits,
            IReadOnlyDictionary<string, HashSet<string>> switched, ICollection<string> idles)
        {
            HashSet<string> Visit(string name) => visits != null && visits.TryGetValue(name, out var seen) ? seen : null;
            var moving = Visit(MoveVisit);
            var blocking = Visit(BlockVisit);
            var jumping = Visit(JumpVisit);

            // Each switch's clips under its group, the groups in the order they win.
            var bySwitch = new Dictionary<string, ClipGroup>(StringComparer.Ordinal);
            if (switched != null)
            {
                foreach (var group in SwitchOrder)
                {
                    foreach (var pair in switched.Where(s => OfSwitch(s.Key) == group).OrderBy(s => s.Key, StringComparer.Ordinal))
                    {
                        foreach (var clip in pair.Value) if (!bySwitch.ContainsKey(clip)) bySwitch[clip] = group;
                    }
                }
            }

            var sorted = new Dictionary<string, ClipGroup>(StringComparer.Ordinal);
            foreach (var clip in clips)
            {
                if (clip == null || sorted.ContainsKey(clip)) continue;
                sorted[clip] = attacks != null && attacks.Contains(clip) ? ClipGroup.Attacks
                    : moving != null && moving.Contains(clip) ? ClipGroup.Moving
                    : blocking != null && blocking.Contains(clip) ? ClipGroup.Blocking
                    : bySwitch.TryGetValue(clip, out var group) ? group
                    : jumping != null && jumping.Contains(clip) ? ClipGroup.Airborne
                    : idles != null && idles.Contains(clip) ? ClipGroup.Moving
                    : ClipGroup.Other;
            }
            return sorted;
        }
    }
}
