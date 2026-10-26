using System;
using UnityEngine;

namespace Scry
{
    /// <summary>The parts of the panel the self-test watches draw: each counted as it is drawn.</summary>
    internal enum PanelPart
    {
        Animations,
        Help,
        ModReport,
        PlacesBar,
        Monitor,
        Plan,
        PlanTab,
        Readme,
        Tooltip,
        Ruler,
        RoofButton,
        Runes,
        ResistanceGrid,
        FactTiles,
        FactTable,
        TopicLinks,
        SectionLine,
        Toolbar,
        TestNotice,
        Slider,
        StageChip,
        ViewMenu,
        ClosedPage,

        /// <summary>A page only seen telling what sight gave: its stars and gear seen up close, where it was seen.</summary>
        SeenLines,

        /// <summary>The switch above a kind's list choosing how it groups (<see cref="ListGrouping"/>).</summary>
        GroupBar,

        /// <summary>A family's page switching among the kinds the character met (<see cref="Explorer.MembersMet"/>).</summary>
        KindSwitch,

        /// <summary>A family's kinds side by side where they differ (<see cref="FamilyDiffers"/>).</summary>
        KindsDiffer,
        UnseenName,
        ListLeftOut,
        PeekStrip,
        PeekPageStrip,
        SetAsideNotice,
        FindsChip,
        LearnedStrip,
        FloorControl,
        CompendiumSwitch,
        CompendiumBox,
        CutChip,
        LearnedTab,

        /// <summary>The world's sources and their levels in the compendium's box.</summary>
        SourcesControl,

        /// <summary>The mark of what a source of the world shows, a folded map, on a row, under a title or after a chip or link.</summary>
        SourceMark,

        /// <summary>The pin at the end of a ground's row in the compendium's box, marking its dungeons on the big map.</summary>
        DungeonPins,

        /// <summary>With insides dimmed on a source's row, none of its places found having insides.</summary>
        InsidesDimmed,

        /// <summary>A place's line under its title of how many the world holds and lie in your ground.</summary>
        CensusLine,

        /// <summary>A lock or a dimmed choice in the compendium's box of what the world's server sets.</summary>
        WorldLocked,

        /// <summary>The notice under the header naming the players whose games lack Scry.</summary>
        PlayingWithNotice,

        /// <summary>The compendium box's row of the players played with, whether each one's game has Scry.</summary>
        PlayingWithRow,
        PeekButton,
        MapBar,
        FloorMark,
        FloorLine,
        StrictLeftOut,

        /// <summary>A line telling what is still to find, set apart from a page's footnotes (<see cref="ScryPanel.HintLine"/>).</summary>
        HintLine,

        /// <summary>The count of wares waiting for a boss not yet fallen: under a row's title, or at a section's end.</summary>
        ToUnlock,
        DamageKeptBack,
        FactKeptBack,
        FloorLink,
        StillToFindOut,
        WeakSpotsLeftOut,
        TableKeptBack,

        /// <summary>The one chip counting an attack group's clips kept back under the strict rules.</summary>
        ClipKeptBack,

        /// <summary>A value kept back under the strict rules, drawn as a soft smudge.</summary>
        Smudge,

        /// <summary>The faint line under an attacks table counting the attacks not yet seen begun or taken.</summary>
        AttacksWait,

        /// <summary>A row or line counted over every character's play, drawn faint under the strict rules.</summary>
        AllPlayFaint,

        /// <summary>A recipe's row left out whole under the strict rules, the game not listing the recipe for the character yet.</summary>
        RecipeWaits,
        TallyCounted,
        YouBox,
        YourSkillLine,
        EmptyRowLeftOut,
        RoomCard,
        RoomCardWay,
        RoomCardLevel,
        RoomCardLine,
        PlanBox,
        PlanName,
        StageKeys,
        Trail,
        TrailMenu,
        MapLine,
        Command,

        /// <summary>The compact view's box of every kind, opened from its kind pill.</summary>
        KindMenu,

        /// <summary>The compact view's box of where the list is taken from, opened from its chip.</summary>
        OriginMenu,

        /// <summary>The compact view's one notice strip, the notices up sharing it.</summary>
        NoticeLine,

        /// <summary>The gear in the title row opening the settings card.</summary>
        SettingsGear,

        /// <summary>The settings card, in the list's place.</summary>
        SettingsCard,

        /// <summary>The compendium's guide, in the list's place.</summary>
        GuideCard,

        /// <summary>The ? in the compendium's box opening its guide.</summary>
        GuideButton,

        /// <summary>The question asked once of a character whose compendium is off, in the list's place.</summary>
        WelcomeCard,

        /// <summary>The notice told once on joining a world whose server sets any of the compendium's rules.</summary>
        RulesNotice,

        /// <summary>A row of the settings card whose choices could not stand on one line beside its label.</summary>
        SettingsRowWrapped,

        /// <summary>Look again above the Locations list, offered once the places are read.</summary>
        LookAgain,
    }

    internal static partial class ScryPanel
    {
        private static readonly int[] DrawnCounts = new int[Enum.GetValues(typeof(PanelPart)).Length];

        /// <summary>How many times a part of the panel has been drawn this session, for the self-test to see it draw.</summary>
        [ForTest]
        public static int Drawn(PanelPart part) => DrawnCounts[(int)part];

        /// <summary>Counts a part as drawn, once for each repaint it is drawn in.</summary>
        private static void CountDrawn(PanelPart part)
        {
            if (Event.current.type == EventType.Repaint) DrawnCounts[(int)part]++;
        }
    }
}
