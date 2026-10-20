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
        UnseenName,
        ListLeftOut,
        PeekStrip,
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
    }

    internal static partial class ScryPanel
    {
        private static readonly int[] DrawnCounts = new int[Enum.GetValues(typeof(PanelPart)).Length];

        /// <summary>How many times a part of the panel has been drawn this session, for the self-test to see it draw.</summary>
        public static int Drawn(PanelPart part) => DrawnCounts[(int)part];

        /// <summary>Counts a part as drawn, once for each repaint it is drawn in.</summary>
        private static void CountDrawn(PanelPart part)
        {
            if (Event.current.type == EventType.Repaint) DrawnCounts[(int)part]++;
        }
    }
}
