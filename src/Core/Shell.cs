using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Opens a website outside the game as a click on the desktop would. Steam counts every
    /// process the game starts as part of the game, so a browser the game started itself (as
    /// Unity's <c>Application.OpenURL</c> does when none is running) kept Steam showing Valheim as
    /// running after it quit, and refusing to start it again, until that browser was closed. On
    /// Windows a short-lived explorer.exe, started directly, hands the address to the running
    /// desktop shell and ends, so the browser is the shell's, not the game's. Elsewhere there is no
    /// explorer.exe, and Unity's own way stays.
    /// </summary>
    internal static class Shell
    {
        /// <summary>
        /// Opens a website, only an http or https address with no space or quote in it
        /// (<see cref="ModWords.IsWebsite"/>), so nothing on the PC is run and the address stays one
        /// argument (<see cref="ModWords.Argument"/>); a failure is told as the panel's.
        /// </summary>
        public static void OpenWebsite(string address)
        {
            if (!ModWords.IsWebsite(address)) return;
            Guard.Run(Feature.Panel, "opening a website", Open, address);
        }

        private static void Open(string address)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            {
                Application.OpenURL(address);
                return;
            }
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            using (Process.Start(new ProcessStartInfo(explorer, ModWords.Argument(address)) { UseShellExecute = false }))
            {
            }
        }
    }
}
