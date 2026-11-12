using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the menu a right click opens on a mark offers.</summary>
    internal enum MapMenuChoice
    {
        Cross,
        Highlight,
        Plain,
        ZoomIn,
        HideKind,
        HideAll,
        OpenPage,
    }

    /// <summary>
    /// The small menu a right click opens on a mark on Valheim's large map, as a left click goes
    /// through its modes (<see cref="SpotModes"/>): the modes it does not stand in, zooming in on a
    /// merged mark, hiding its kind or every mark where it is a kept kind's, and last its page.
    /// </summary>
    internal static class MapMenu
    {
        /// <summary>What it offers on a mark of a mode standing for so many spots, hiding where its kind may be hidden, in the order it stands them.</summary>
        public static List<MapMenuChoice> Of(SpotMode mode, int spots, bool hideable = false)
        {
            var choices = new List<MapMenuChoice>();
            if (mode != SpotMode.Crossed) choices.Add(MapMenuChoice.Cross);
            if (mode != SpotMode.Highlighted) choices.Add(MapMenuChoice.Highlight);
            if (mode != SpotMode.Plain) choices.Add(MapMenuChoice.Plain);
            if (spots > 1) choices.Add(MapMenuChoice.ZoomIn);
            if (hideable)
            {
                choices.Add(MapMenuChoice.HideKind);
                choices.Add(MapMenuChoice.HideAll);
            }
            choices.Add(MapMenuChoice.OpenPage);
            return choices;
        }

        /// <summary>The mode a choice gives; none for one that gives no mode.</summary>
        public static SpotMode? ModeOf(MapMenuChoice choice) =>
            choice == MapMenuChoice.Cross ? SpotMode.Crossed
            : choice == MapMenuChoice.Highlight ? SpotMode.Highlighted
            : choice == MapMenuChoice.Plain ? SpotMode.Plain
            : (SpotMode?)null;
    }
}
