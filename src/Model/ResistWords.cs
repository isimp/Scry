namespace Scry
{
    /// <summary>How hard a damage type lands, in the order and with the numbers of the game's own <c>HitData.DamageModifier</c>.</summary>
    internal enum Degree
    {
        Normal,
        Resistant,
        Weak,
        Immune,
        Ignore,
        VeryResistant,
        VeryWeak,
        SlightlyResistant,
        SlightlyWeak,
    }

    /// <summary>The colour a grid cell is drawn in: plain, resisting, weak, or taking nothing.</summary>
    internal enum Tone
    {
        Plain,
        Resists,
        Weak,
        Immune,
    }

    /// <summary>One damage type in the resistance grid: its name, the share of it taken, and the words on hover.</summary>
    internal struct ResistCell
    {
        public string Type, Value, Tip;
        public Tone Tone;
    }

    /// <summary>
    /// Resistances as a grid of every damage type, so whatever a creature, piece or rock
    /// resists, every one of them shows the same cells. Each is the share of that damage taken,
    /// as <c>HitData.ApplyModifier</c> scales it: a quarter, half or three quarters resisted, a
    /// quarter, half or all again for weakness, none for immune or unaffected.
    /// </summary>
    internal static class ResistWords
    {
        /// <summary>The damage types, in the order of the game's <c>HitData.DamageModifiers</c>.</summary>
        public static readonly string[] Types = { "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit" };

        public static ResistCell Cell(string type, Degree degree)
        {
            string value, word;
            var tone = Tone.Plain;
            switch (degree)
            {
                case Degree.Normal: value = "100%"; word = "takes it in full"; break;
                case Degree.SlightlyResistant: value = "75%"; word = "slightly resists it: takes three quarters"; tone = Tone.Resists; break;
                case Degree.Resistant: value = "50%"; word = "resists it: takes half"; tone = Tone.Resists; break;
                case Degree.VeryResistant: value = "25%"; word = "strongly resists it: takes a quarter"; tone = Tone.Resists; break;
                case Degree.SlightlyWeak: value = "125%"; word = "slightly weak to it: takes a quarter more"; tone = Tone.Weak; break;
                case Degree.Weak: value = "150%"; word = "weak to it: takes half again"; tone = Tone.Weak; break;
                case Degree.VeryWeak: value = "200%"; word = "very weak to it: takes double"; tone = Tone.Weak; break;
                case Degree.Immune: value = "0%"; word = "immune to it: takes none"; tone = Tone.Immune; break;
                case Degree.Ignore: value = "0%"; word = "unaffected by it: takes none"; tone = Tone.Immune; break;
                default: value = "?"; word = $"a degree Scry does not know ({Numbers.Count((int)degree)})"; break;
            }
            return new ResistCell { Type = type, Value = value, Tip = $"{type}: {word}", Tone = tone };
        }
    }
}
