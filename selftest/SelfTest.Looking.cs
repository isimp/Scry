using System.Collections;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for looking around with the panel open.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Looking around with the panel open holds the cursor captured and hidden frame after
        /// frame: the game's own capture, which with the panel open frees and shows it, never runs
        /// in between, so it is not shown in the middle of the window each frame. Let go, it is
        /// freed again.
        /// </summary>
        private static IEnumerator CursorHeldWhileLooking(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            yield return null;
            var changes = 0;
            void Changed() => changes++;
            ScryPanel.LookForTest(true);
            yield return null;
            yield return null;
            ZCursor.IsVisibleChanged += Changed;
            for (var i = 0; i < 30; i++) yield return null;
            ZCursor.IsVisibleChanged -= Changed;
            p.Check(changes == 0 && !ZCursor.IsVisible && ZCursor.LockState == CursorLockMode.Locked,
                "held looking around, the cursor stays captured and hidden, never shown in between",
                $"{Numbers.Count(changes)} changes in 30 frames, {(ZCursor.IsVisible ? "shown" : "hidden")}, {ZCursor.LockState}, mouse {(ZInput.IsMouseActive() ? "in use" : "not in use")}");
            ScryPanel.LookForTest(false);
            yield return Until(() => ZCursor.LockState != CursorLockMode.Locked, 3);
            p.Check(ZCursor.LockState != CursorLockMode.Locked, "let go, it is freed again", ZCursor.LockState.ToString());
        }
    }
}
