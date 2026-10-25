using System;

namespace Scry
{
    /// <summary>What the game lists of recipes for the character, which what it shows of a thing hinges on.</summary>
    internal sealed partial class Explorer
    {
        private Func<string, bool> _recipeListed;

        /// <summary>How the game's listing of a recipe is read for the character (<c>Player.m_knownRecipes</c>); none reads every recipe as listed.</summary>
        public Func<string, bool> RecipeListedBy
        {
            set
            {
                _recipeListed = value;
                if (_view != null) _view.RecipeListedBy = value;
            }
        }

        /// <summary>
        /// Whether the game lists a recipe for the character, by the token it lists it by (an
        /// item's name token, a piece's name): the crafting menu and the hammer show a recipe's
        /// station, level and materials only then, the station known and every material known.
        /// A fact with no recipe of its own (a store's wares) is listed always.
        /// </summary>
        public bool RecipeListed(string token) => token == null || _recipeListed == null || _recipeListed(token);

        /// <summary>
        /// Whether a game-shown row waits for the game to list its recipe for the character
        /// (<see cref="RecipeListed"/>): with the compendium on, on a known page not shown whole,
        /// under either rules, as the guide has it: what the game's menus show comes with the thing
        /// once the game lists it.
        /// </summary>
        public bool RecipeWaits(string recipe) =>
            recipe != null && CompendiumOn && _selected != null && _view.Of(_selected) == KnownLevel.Known && !_view.Whole(_selected) && !RecipeListed(recipe);
    }
}
