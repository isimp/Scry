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
        /// still, and notes the file; notes why when none came. The search box is left first, so
        /// its help does not cover the list.
        /// </summary>
        private static IEnumerator Picture(Probe p, string what)
        {
            ScryPanel.LeaveBoxes();
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

        /// <summary>Goes to a section or a topic of In the game on the page shown, as a click on a section's name on the line does, and takes a picture of it.</summary>
        private static IEnumerator PictureOf(Probe p, string section, string what)
        {
            yield return GoTo(section);
            if (!ScryPanel.StandsAt(section)) p.Note($"{what}: {section} was not gone to");
            yield return Picture(p, what);
        }

        /// <summary>Goes to a section or a topic of In the game on the page shown, and waits for the details to stand there.</summary>
        private static IEnumerator GoTo(string section)
        {
            ScryPanel.GoToSection(section);
            yield return Until(() => ScryPanel.StandsAt(section), 2);
        }

        private static float? _stageScaleWas;
        private static bool? _listHiddenWas;

        /// <summary>Draws the stage as tall as it goes, the list folded away, for pictures of what it shows; <see cref="StageAsItWas"/> puts both back.</summary>
        private static void BigStage()
        {
            if (_stageScaleWas == null)
            {
                _stageScaleWas = ScryPanel.StageScale;
                _listHiddenWas = ScryPanel.ListHidden;
            }
            ScryPanel.StageScale = 2.4f;
            ScryPanel.ListHidden = true;
        }

        /// <summary>Puts the stage's height and the list back as they were before <see cref="BigStage"/>.</summary>
        private static void StageAsItWas()
        {
            if (_stageScaleWas is float scale) ScryPanel.StageScale = scale;
            if (_listHiddenWas is bool hidden) ScryPanel.ListHidden = hidden;
            _stageScaleWas = null;
            _listHiddenWas = null;
        }
    }
}
