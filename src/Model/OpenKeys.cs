using System;
using System.Text;

namespace Scry
{
    /// <summary>The modifiers a key opening Scry may come with, each side of the keyboard alike.</summary>
    [Flags]
    internal enum KeyMods
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4,
    }

    /// <summary>
    /// The key opening Scry with its modifiers: when it opens, how it is named, and when a key
    /// typed into the search is text rather than the key. Keys held besides, as one walking holds
    /// them, never stop it, unlike a shortcut that wants no other key down.
    /// </summary>
    internal static class OpenKeys
    {
        /// <summary>Whether the key opens: pressed now, every modifier it names held, others held besides or not.</summary>
        public static bool Fires(bool pressed, KeyMods wanted, KeyMods held) => pressed && (held & wanted) == wanted;

        /// <summary>A key named as a player says it: its modifiers first, Ctrl, Alt then Shift, then the key, a digit for a number key.</summary>
        public static string Name(string key, KeyMods mods)
        {
            var name = new StringBuilder();
            if ((mods & KeyMods.Control) != 0) name.Append("Ctrl+");
            if ((mods & KeyMods.Alt) != 0) name.Append("Alt+");
            if ((mods & KeyMods.Shift) != 0) name.Append("Shift+");
            if (key.StartsWith("Alpha", StringComparison.Ordinal) && key.Length == 6) name.Append(key[5]);
            else if (key.StartsWith("Keypad", StringComparison.Ordinal) && key.Length > 6) name.Append("Keypad ").Append(key.Substring(6));
            else name.Append(key);
            return name.ToString();
        }

        /// <summary>Whether a key typed into the search writes text there: one that types, with neither Ctrl nor Alt held.</summary>
        public static bool TypesText(bool typesAlone, KeyMods held) => typesAlone && (held & (KeyMods.Control | KeyMods.Alt)) == 0;
    }
}
