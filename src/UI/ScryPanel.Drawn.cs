using System;
using UnityEngine;

namespace Scry
{
    /// <summary>The parts of the panel the self-test watches draw: each counted as it is drawn.</summary>
    internal enum PanelPart
    {
        Help,
        ModReport,
        Monitor,
        Plan,
        PlanTab,
        Readme,
        Ruler,
        RoofButton,
        Runes,
        ResistanceGrid,
        SectionLine,
        TestNotice,
        Slider,
        StageChip,
        ViewMenu,
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
