using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Scry
{
    /// <summary>
    /// Pictures the self-test takes of what it shows, to be looked at after a run: the screen as
    /// the player sees it, saved by name under Scry's cache folder. A quick, deep or full run
    /// first moves the last run's pictures into <c>previous</c> under it, those there before
    /// let go, so the folder holds only pictures of views that still are; a run of a few parts
    /// takes its pictures over those in place. The log names each picture a run took. A full run
    /// also takes a few of them again clean, for the README and the player's guide (<see cref="ReadmeShots"/>).
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Where the pictures go.</summary>
        internal static string PictureFolder => Path.Combine(Settings.CacheFolder, "selftest-pictures");

        /// <summary>Moves the last run's pictures into <c>previous</c>, letting go of those there before; told in the log.</summary>
        private static void PicturesAside()
        {
            var aside = Path.Combine(PictureFolder, "previous");
            var failure = Steps.Run(() =>
            {
                if (!Directory.Exists(PictureFolder)) return;
                if (Directory.Exists(aside)) Directory.Delete(aside, true);
                Directory.CreateDirectory(aside);
                foreach (var file in Directory.GetFiles(PictureFolder, "*.png")) File.Move(file, Path.Combine(aside, Path.GetFileName(file)));
            }, null);
            Write(failure == null ? "The last run's pictures are in selftest-pictures/previous." : $"The last run's pictures could not be moved aside: {failure.Message}");
        }

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
            yield return Shot(p, what);
            if (_readme && ReadmeShots.FirstOrDefault(s => s.Shows(what)) is var shot && shot.Name != null && ReadmeTaken.Add(shot.Name)) yield return CleanPicture(p, shot.Name, shot.KeepsHud);
        }

        /// <summary>
        /// The pictures a full run also takes clean, for the README and the player's guide, by
        /// what a part pictures: the self-test's strip and, but for the large map, which closes
        /// with it, the game's HUD hidden for the shot, saved as "readme" and a name, the first
        /// time a part pictures it in the run.
        /// </summary>
        private static readonly (Func<string, bool> Shows, string Name, bool KeepsHud)[] ReadmeShots =
        {
            (w => w == "you Troll, first seen, came to know and killed you", "readme troll page", false),
            (w => w == "learned by day, Troll selected", "readme learned by day", false),
            (w => w == "compendium box pins", "readme compendium box", false),
            (w => w.StartsWith("plan ", StringComparison.Ordinal) && w.EndsWith(" Above deepest gone into", StringComparison.Ordinal), "readme dungeon plan", false),
            (w => w.StartsWith("the marks of ", StringComparison.Ordinal) && w.EndsWith(", the hollow one's tip shown", StringComparison.Ordinal), "readme map marks", true),
            (w => w.StartsWith("camp example ", StringComparison.Ordinal), "readme camp example", false),
            (w => w == "compact header", "readme compact view", false),
            (w => w == "skill Swords", "readme skill page", false),
            (w => w == "skill lines SwordIron", "readme item skill lines", false),
        };

        /// <summary>Whether this run takes the README's pictures, a full one; and those it took.</summary>
        private static bool _readme;
        private static readonly HashSet<string> ReadmeTaken = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>As a run starts: whether it takes the README's pictures, by its scope, none taken yet.</summary>
        private static void ReadmeFor(RunScope scope)
        {
            _readme = scope == RunScope.Full;
            ReadmeTaken.Clear();
        }

        /// <summary>Takes a picture with the self-test's strip put away, and the game's HUD unless it is kept, both back as they were after.</summary>
        private static IEnumerator CleanPicture(Probe p, string name, bool keepsHud)
        {
            var hud = keepsHud ? null : Hud.instance;
            var hudWas = hud != null && hud.m_userHidden;
            SelfTestPanel.HiddenForPicture = true;
            if (hud != null) hud.m_userHidden = true;
            yield return null;
            yield return null;
            yield return Shot(p, name);
            if (hud != null) hud.m_userHidden = hudWas;
            SelfTestPanel.HiddenForPicture = false;
        }

        /// <summary>A full run took each of the README's pictures clean (<see cref="ReadmeShots"/>), by the parts that picture what each shows; the strip back after.</summary>
        private static IEnumerator ReadmePictures(Probe p)
        {
            if (!_readme)
            {
                p.Skip("only a full run takes the README's pictures");
                yield break;
            }
            var missing = ReadmeShots.Select(s => s.Name).Where(n => !ReadmeTaken.Contains(n)).ToList();
            p.Check(missing.Count == 0, "each of the README's pictures was taken clean", missing.Count > 0 ? "not taken: " + string.Join(", ", missing) : $"{Numbers.Count(ReadmeTaken.Count)} taken");
            p.Check(!SelfTestPanel.HiddenForPicture, "the self-test's strip shows again");
        }

        /// <summary>Saves the screen as it is drawn now, named for what it shows, and notes the file; notes why when none came.</summary>
        private static IEnumerator Shot(Probe p, string what)
        {
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
