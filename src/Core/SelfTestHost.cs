using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where the in-game self-test plugs in. The self-test is a plugin of its own
    /// (<c>Scry.SelfTest.dll</c>), deployed with a development build of Scry and never packaged
    /// with a release, so everything of it lives there: its runner, its words, the /scry words it
    /// answers and how it is drawn in the panel. As it loads it registers here, and Scry reaches
    /// it only through here: each frame, the words typed after /scry, and its strip under the
    /// panel's header with its details in the list's place, which it draws itself. Without it
    /// none of these do anything, and nothing of it shows.
    /// </summary>
    internal static class SelfTestHost
    {
        /// <summary>What the self-test offers Scry.</summary>
        public interface IRunner
        {
            /// <summary>Called every frame.</summary>
            void Tick();

            /// <summary>The reply to the words typed after /scry where they are the self-test's; null where they are Scry's.</summary>
            string Answer(string typed);

            /// <summary>Whether it has a strip to show under the panel's header.</summary>
            bool StripShown { get; }

            void DrawStrip(Rect rect);

            /// <summary>Whether its details stand in the list's place.</summary>
            bool CardShown { get; }

            void DrawCard(Rect rect);
        }

        private static IRunner _runner;

        /// <summary>The self-test registers itself as it loads.</summary>
        public static void Register(IRunner runner) => _runner = runner;

        public static void Tick() => _runner?.Tick();
        public static string Answer(string typed) => _runner?.Answer(typed);
        public static bool StripShown => _runner?.StripShown ?? false;
        public static void DrawStrip(Rect rect) => _runner?.DrawStrip(rect);
        public static bool CardShown => _runner?.CardShown ?? false;
        public static void DrawCard(Rect rect) => _runner?.DrawCard(rect);
    }
}
