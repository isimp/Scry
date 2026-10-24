using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>The notice under the header telling a lasting file that could not be read was set aside (<see cref="SetAsideNotice"/>).</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The file the notice's line was made for, and the line.</summary>
        private static (SetAsideKind Kind, string Path)? _asideFor;

        private static string _asideLine;

        /// <summary>How tall the notice of a file set aside is: none while none is to be told.</summary>
        private static float SetAsideNoticeHeight() => SetAsideNotice.Files.Count > 0 ? U(32f) : 0f;

        /// <summary>
        /// A strip telling the first file set aside not yet put away: what it held and what it was
        /// set aside as, Copy path copying where it lies, and a cross putting it away, the next told
        /// after it. The compact view's step at its end where given. What was clicked.
        /// </summary>
        private static StripClick SetAsideStrip(Rect rect, string step = null)
        {
            if (!(SetAsideNotice.Files.First is (SetAsideKind, string) told)) return StripClick.None;
            if (_asideFor == null || _asideFor.Value != told)
            {
                _asideFor = told;
                _asideLine = CompendiumWords.SetAsideNotice(told.Kind, Path.GetFileName(told.Path));
            }
            CountDrawn(PanelPart.SetAsideNotice);
            var clicked = NoticeStrip(rect, Skin.Accent, _asideLine, Skin.Label, false, CompendiumWords.CopyPath, false, true,
                chipTip: CompendiumWords.CopyPathTip, crossTip: CompendiumWords.PutSetAsideAway, lineTip: _asideLine, step: step);
            if (clicked == StripClick.Chip)
            {
                GUIUtility.systemCopyBuffer = told.Path;
                Say(PanelWords.Copied(told.Path));
            }
            if (clicked == StripClick.Cross) SetAsideNotice.Files.PutAway();
            return clicked;
        }

        /// <summary>The notice's line as last drawn, for the self-test; none while none is up.</summary>
        [Diagnostic]
        public static string SetAsideLineForTest => SetAsideNoticeHeight() > 0f ? _asideLine : null;
    }
}
