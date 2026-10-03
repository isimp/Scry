using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A page shown in the list's place (how to search, the mod report, what is off, the
    /// self-test's details): a panel with a scrolled body, its title at the top, and a row of
    /// buttons at its foot, Close on the right and any other to its left while there is room.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Where a card's body is drawn inside its scroll view: its left edge, its first line, the width its text takes, and how much of it shows.</summary>
        private struct CardBody
        {
            public float X, Y, Width;

            /// <summary>The part of the body in view, in the scroll view's own space.</summary>
            public Rect Visible;
        }

        private static float CardFootHeight => U(30f);

        /// <summary>The scrolled part of a card, on the screen.</summary>
        private static Rect CardArea(Rect rect) => new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - CardFootHeight - U(20f));

        /// <summary>The width a card's text takes, known before the card is begun, for working out its height.</summary>
        private static float CardWidth(Rect rect) => CardArea(rect).width - U(38f);

        /// <summary>Draws a card's panel and begins its scrolled body, as tall as given, under its title.</summary>
        private static CardBody BeginCard(Rect rect, ref Vector2 scroll, float height, string title)
        {
            Skin.Box(rect, Skin.Panel);
            var area = CardArea(rect);
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(height, area.height));
            scroll = GUI.BeginScrollView(area, scroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var x = U(14f);
            var y = U(10f);
            var width = view.width - U(24f);
            GUI.Label(new Rect(x, y, width, U(26f)), title, Skin.Big);
            return new CardBody { X = x, Y = y + U(34f), Width = width, Visible = new Rect(0f, scroll.y, view.width, area.height) };
        }

        /// <summary>Ends a card's body and draws its Close; true when it is clicked. Where Close stands, for buttons beside it.</summary>
        private static bool EndCard(Rect rect, out Rect close)
        {
            GUI.EndScrollView();
            var height = CardFootHeight;
            close = new Rect(rect.xMax - U(96f), rect.yMax - height - U(10f), U(80f), height);
            return GUI.Button(close, "Close", Skin.Button);
        }

        /// <summary>A button at a card's foot, left of the one given, while there is room; true when it is clicked.</summary>
        private static bool CardButton(Rect rect, Rect beside, string text, out Rect button)
        {
            var width = Skin.Width(Skin.Button, text) + U(10f);
            button = new Rect(beside.x - U(8f) - width, beside.y, width, beside.height);
            return button.x > rect.x + U(8f) && GUI.Button(button, text, Skin.Button);
        }

        /// <summary>While the locations are not read, Read all locations at a card's foot beside Close, with its tip.</summary>
        private static void ReadLocationsButton(Rect rect, Rect close, string tipKey)
        {
            if (Locations.Now != Locations.State.NotRead) return;
            if (CardButton(rect, close, LocationsButtonText, out var button)) StartReadingLocations();
            if (button.x > rect.x + U(8f) && button.Contains(Event.current.mousePosition)) AskTip(tipKey, LocationsButtonTip);
        }
    }
}
