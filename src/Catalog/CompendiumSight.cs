using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character sees, for the compendium, looked at once a second: creatures within
    /// <see cref="CreatureReach"/> in the camera's view with nothing solid between, places whose
    /// centre is in view within their own reach and <see cref="CreatureReach"/> beyond it, and
    /// items on the ground within the character's reach (<c>Player.m_maxInteractDistance</c>) are
    /// seen; a container opened shows what it holds, seen, and the container itself is known.
    /// </summary>
    internal static class CompendiumSight
    {
        /// <summary>How far a creature in view is seen.</summary>
        private const float CreatureReach = 30f;

        /// <summary>Half the angle of the view taken as seen, about the width the game's camera shows.</summary>
        private const float HalfView = 50f;

        private static AccessTools.FieldRef<List<ItemDrop>> _items;
        private static AccessTools.FieldRef<List<Location>> _places;
        private static AccessTools.FieldRef<InventoryGui, Container> _container;
        private static int _solid;

        /// <summary>Looks around the character, learning what it sees.</summary>
        public static void Look(Player player, CompendiumCatchUp catchUp, CompendiumBook book)
        {
            Bind();
            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            foreach (var creature in Character.GetAllCharacters())
            {
                if (creature == null || creature.IsPlayer()) continue;
                var centre = creature.GetCenterPoint();
                if (Vector3.Distance(player.transform.position, centre) <= CreatureReach && InView(view, centre) && Clear(view.position, centre))
                {
                    catchUp.Met(Utils.GetPrefabName(creature.gameObject), KnownLevel.Seen, book);
                }
            }
            foreach (var place in _places())
            {
                if (place == null) continue;
                var centre = place.transform.position;
                if (Vector3.Distance(player.transform.position, centre) <= place.m_exteriorRadius + CreatureReach && InView(view, centre))
                {
                    catchUp.Met(EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)), KnownLevel.Seen, book);
                }
            }
            foreach (var item in _items())
            {
                if (item != null && Vector3.Distance(player.transform.position, item.transform.position) <= player.m_maxInteractDistance)
                {
                    catchUp.Met(Utils.GetPrefabName(item.gameObject), KnownLevel.Seen, book);
                }
            }
            var gui = InventoryGui.instance;
            var container = gui != null && gui.IsContainerOpen() ? _container(gui) : null;
            if (container == null) return;
            catchUp.Met(Utils.GetPrefabName(container.gameObject), KnownLevel.Known, book);
            foreach (var held in container.GetInventory().GetAllItems())
            {
                if (held?.m_dropPrefab != null) catchUp.Met(held.m_dropPrefab.name, KnownLevel.Seen, book);
            }
        }

        private static bool InView(Transform view, Vector3 point) => Vector3.Angle(view.forward, point - view.position) <= HalfView;

        /// <summary>Whether nothing solid (ground, rock, a building) stands between the eye and a point.</summary>
        private static bool Clear(Vector3 eye, Vector3 point) => !Physics.Linecast(eye, point, _solid);

        private static void Bind()
        {
            if (_container != null) return;
            _items = AccessTools.StaticFieldRefAccess<List<ItemDrop>>(AccessTools.Field(typeof(ItemDrop), "s_instances"));
            _places = AccessTools.StaticFieldRefAccess<List<Location>>(AccessTools.Field(typeof(Location), "s_allLocations"));
            _solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            _container = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        }
    }
}
