using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Scry
{
    /// <summary>
    /// Pictures the self-test takes of what it shows, to be looked at after a run: the screen as
    /// the player sees it, saved by name under Scry's cache folder, each run's over the last's.
    /// The log names each picture a run took.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Where the pictures go.</summary>
        internal static string PictureFolder => Path.Combine(Settings.CacheFolder, "selftest-pictures");

        private static MethodInfo _capture;
        private static bool _lookedForCapture;

        /// <summary>
        /// The game's own screen capture (<c>ScreenCapture.CaptureScreenshot</c>), found by name:
        /// Scry builds without its module. It saves the screen once the frame is drawn, the panel
        /// with it.
        /// </summary>
        private static MethodInfo Capture
        {
            get
            {
                if (!_lookedForCapture)
                {
                    _lookedForCapture = true;
                    _capture = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", false)?.GetMethod("CaptureScreenshot", new[] { typeof(string) });
                }
                return _capture;
            }
        }

        /// <summary>A picture's file name from what it shows, with what a file name cannot hold made a dash.</summary>
        private static string PictureName(string what) =>
            new string(what.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray()) + ".png";

        /// <summary>
        /// Takes a picture of the screen, named for what it shows, once the stage's view stands
        /// still, and notes the file; notes why when none came.
        /// </summary>
        private static IEnumerator Picture(Probe p, string what)
        {
            yield return Until(() => !Stage.Gliding, 3);
            yield return null;
            if (Capture == null)
            {
                p.Note($"no picture of {what}: the game's screen capture was not found");
                yield break;
            }
            var file = Path.Combine(PictureFolder, PictureName(what));
            var before = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            var failure = Steps.Run(() =>
            {
                Directory.CreateDirectory(PictureFolder);
                Capture.Invoke(null, new object[] { file });
            }, null);
            if (failure != null)
            {
                p.Note($"no picture of {what}: {(failure.InnerException ?? failure).Message}");
                yield break;
            }
            yield return Until(() => File.Exists(file) && File.GetLastWriteTimeUtc(file) > before, 3);
            p.Note(File.Exists(file) && File.GetLastWriteTimeUtc(file) > before ? $"pictured {what}: {Path.GetFileName(file)}" : $"no picture of {what} came");
        }

        /// <summary>Goes to a section of the page shown, as a click on its name on the section line does, and takes a picture of it.</summary>
        private static IEnumerator PictureOf(Probe p, string section, string what)
        {
            ScryPanel.GoToSection(section);
            yield return Until(() => ScryPanel.SectionLit == section, 2);
            if (ScryPanel.SectionLit != section) p.Note($"{what}: its section ({section}) was not gone to");
            yield return Picture(p, what);
        }
    }
}
