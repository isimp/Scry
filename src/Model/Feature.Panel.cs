namespace Scry
{
    /// <summary>The panel and the game's input while it is open.</summary>
    internal sealed partial class Feature
    {
        public static readonly Feature KeysKeptFromGame = Listed("keeping the game's keys away while the panel is open");
        public static readonly Feature TabKeptFromInventory = Listed("keeping Tab from opening the inventory while typing in the panel");
        public static readonly Feature LookAndWalkWhileOpen = Listed("looking around and walking while the panel is open");
        public static readonly Feature NoCombatWhileOpen = Listed("keeping attacks, blocks and dodges from going off while the panel is open");
        public static readonly Feature CursorHeldWhileLooking = Listed("holding the cursor while looking around");
        public static readonly Feature WheelKeptFromCamera = Listed("keeping the mouse wheel from zooming the game camera while over the panel");
        public static readonly Feature Command = Listed("the /scry command");
        public static readonly Feature Panel = Listed("the panel");
        public static readonly Feature PanelPlace = Listed("the panel's place and size");
        public static readonly Feature OpenKey = Listed("opening the panel with its key");
        public static readonly Feature ResourceMonitor = Listed("the resource monitor");
        public static readonly Feature Favourites = Listed("favourites");
        public static readonly Feature SearchSuggestions = Listed("search suggestions");
        public static readonly Feature SearchTerms = Listed("search terms of what things are and do");
        public static readonly Feature GameNames = Listed("names as the game shows them");
        public static readonly Feature Groups = Listed("the list's groups");
        public static readonly Feature Links = Listed("links between entries");
        public static readonly Feature DetailsShown = Listed("the details");
    }
}
