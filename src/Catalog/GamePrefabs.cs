using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The game's prefabs by name, looked up one way: the scene's (<c>ZNetScene.GetPrefab</c>),
    /// an item from the object database first (<c>ObjectDB.GetItemPrefab</c>), and the person,
    /// the game's own player model, beside which a model is shown and on which gear is worn.
    /// Nothing is found before the game's scene or database is up, nor for no name.
    /// </summary>
    internal static class GamePrefabs
    {
        /// <summary>The name of the game's player prefab.</summary>
        public const string PersonName = "Player";

        /// <summary>A prefab of the scene by its name.</summary>
        public static GameObject Named(string name) =>
            ZNetScene.instance != null && !string.IsNullOrEmpty(name) ? ZNetScene.instance.GetPrefab(name) : null;

        /// <summary>An item by its name, from the object database, else the scene.</summary>
        public static GameObject Item(string name)
        {
            var item = ObjectDB.instance != null && !string.IsNullOrEmpty(name) ? ObjectDB.instance.GetItemPrefab(name) : null;
            return item != null ? item : Named(name);
        }

        /// <summary>The person: the game's own player model.</summary>
        public static GameObject Person => Named(PersonName);
    }
}
