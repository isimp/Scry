using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The game's prefabs by name, looked up one way: the scene's (<c>ZNetScene.GetPrefab</c>, by
    /// name or by the hash of it a thing in the world keeps),
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

        /// <summary>A prefab of the scene by the hash of its name, as a thing in the world keeps it (<c>ZDO.GetPrefab</c>).</summary>
        public static GameObject Hashed(int hash) => ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;

        /// <summary>
        /// An item by the hash of its name, as a creature's data keeps what it holds and wears
        /// (<c>VisEquipment.SetRightItem</c>): from the object database first, as the game draws it
        /// (<c>VisEquipment.AttachItem</c> asks <c>ObjectDB.GetItemPrefab</c>), else the scene. A
        /// creature's own attack items, as a Dvergr mage's summon, are in the database alone.
        /// </summary>
        public static GameObject ItemHashed(int hash)
        {
            if (hash == 0) return null;
            var item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(hash) : null;
            return item != null ? item : Hashed(hash);
        }

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
