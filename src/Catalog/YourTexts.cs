using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The texts the character has read, as the game keeps them (<c>Player.m_knownTexts</c>: a
    /// runestone's label and its text, added as it is read, <c>RuneStone.Interact</c>), read at
    /// most once a second while asked, each text as kept and as the game shows it. A stone's text
    /// with no label is never kept by the game, so it never counts as read.
    /// </summary>
    internal static class YourTexts
    {
        static YourTexts() => WorldCaches.Register(nameof(YourTexts), Forget);

        private static readonly AccessTools.FieldRef<Player, Dictionary<string, string>> Known = AccessTools.FieldRefAccess<Player, Dictionary<string, string>>("m_knownTexts");
        private static readonly HashSet<string> Read = new HashSet<string>(StringComparer.Ordinal);
        private static float _readAt = -1f;
        private static int _count = -1;

        /// <summary>Whether the character has read a text, as a runestone holds it.</summary>
        public static bool HasRead(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (_readAt < 0f || Time.unscaledTime - _readAt >= 1f)
            {
                _readAt = Time.unscaledTime;
                Guard.Run(Feature.CompendiumTextsRead, "reading the texts read", ReadNowStep);
            }
            return Read.Contains(text);
        }

        private static readonly Action ReadNowStep = ReadNow;

        private static void ReadNow()
        {
            var player = Player.m_localPlayer;
            var known = player != null ? Known(player) : null;
            if (known == null || known.Count == _count) return;
            _count = known.Count;
            Read.Clear();
            foreach (var text in known.Values)
            {
                if (string.IsNullOrEmpty(text)) continue;
                Read.Add(text);
                var shown = CatalogBuilder.Localize(text);
                if (shown.Length > 0) Read.Add(shown);
            }
        }

        /// <summary>Forgets the texts read, for another world or character.</summary>
        public static void Forget()
        {
            Read.Clear();
            _readAt = -1f;
            _count = -1;
        }
    }
}
