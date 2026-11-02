using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// While Show on map shows a kind, the player's other pins step back: the large map's two pin
    /// layers, the pins and their names, stand at a third of their see-through
    /// (<see cref="SeenOnMap.Dimmed"/>), each by a group laid on the layer, which the game leaves be
    /// while it sets each pin's own colour as it places it. Scry's dots, squares and counts on
    /// those layers keep the map's own brightness by groups of their own that ignore the layer's
    /// (<see cref="SeenOnMap.AsShown"/>), and so do the markers of the character and its ship
    /// where the map keeps one on a pin layer. The map's picture itself lies under a dark veil
    /// (<see cref="SeenOnMap.MapDarkened"/>), laid first in it, so every mark stands over it. Put
    /// back as the kind is hidden or the map closes:
    /// each group's see-through as it was, a group Scry laid staying at full, so hiding and showing
    /// again within a frame finds the groups there and not yet destroyed.
    /// </summary>
    internal static class MapDim
    {
        /// <summary>Leaving a world puts the layers back (<see cref="WorldCaches"/>).</summary>
        static MapDim() => WorldCaches.Register(nameof(MapDim), Restore);

        /// <summary>A layer dimmed: its group and its see-through before.</summary>
        private sealed class Dimmed
        {
            public RectTransform Layer;
            public CanvasGroup Group;
            public float Before;
        }

        private static readonly List<Dimmed> Layers = new List<Dimmed>(2);

        /// <summary>What keeps its brightness on a dimmed layer: Scry's holders and the game's own markers there, each by a group of its own as it stood before, and the layer it stands on.</summary>
        private static readonly List<(CanvasGroup Group, float Before, bool Ignored, Dimmed On)> Kept = new List<(CanvasGroup, float, bool, Dimmed)>(5);

        /// <summary>The groups above each layer, from it up, read as they stand this frame.</summary>
        private static readonly List<(float Alpha, bool IgnoresParents)> Chain = new List<(float, bool)>(8);

        /// <summary>The veil over the map's picture, made once for each map and switched on and off.</summary>
        private static Image _veil;

        /// <summary>
        /// The veil over the map's picture, for the self-test: whether it stands, how dark, and
        /// whether it lies first in the map's picture, under all drawn after it.
        /// </summary>
        [Diagnostic]
        public static (bool Shown, float Alpha, bool FirstInPicture) VeilForTest()
        {
            var map = Minimap.instance;
            if (_veil == null || map == null || map.m_mapImageLarge == null) return (false, 0f, false);
            return (_veil.gameObject.activeInHierarchy, _veil.color.a, _veil.transform.parent == map.m_mapImageLarge.transform && _veil.transform.GetSiblingIndex() == 0);
        }

        /// <summary>Whether the layers stand dimmed now, for the self-test.</summary>
        [ForTest]
        public static bool Dimming => Layers.Count > 0;

        /// <summary>
        /// How bright the pins' layer, its names' layer and Scry's dots stand on the screen, each as
        /// every group from it up makes it, for the self-test; 1 for one not there.
        /// </summary>
        [Diagnostic]
        public static (float Pins, float Names, float Dots) BrightnessForTest(Transform dots)
        {
            var map = Minimap.instance;
            return map == null ? (1f, 1f, 1f) : (Brightness(map.m_pinRootLarge), Brightness(map.m_pinNameRootLarge), Brightness(dots));
        }

        /// <summary>How bright a thing stands on the screen, as Unity reckons it from every group from it up.</summary>
        private static float Brightness(Transform from)
        {
            if (from == null) return 1f;
            Chain.Clear();
            for (var at = from; at != null; at = at.parent)
            {
                var group = at.GetComponent<CanvasGroup>();
                if (group != null) Chain.Add((group.alpha, group.ignoreParentGroups));
            }
            return SeenOnMap.AsShown(Chain);
        }

        /// <summary>Each frame a kind is shown on the large map: its pin layers dimmed, and what is Scry's on them, or the game's own markers, at the map's brightness.</summary>
        public static void Hold(Minimap map, RectTransform dots, RectTransform squares, RectTransform counts)
        {
            if (Layers.Count == 0)
            {
                // As the player set them in the settings' Map section (MapLook); at full the pins' layers are left as the game has them.
                Veil(map.m_mapImageLarge, MapLook.Veil(Settings.MapDarken));
                if (!MapLook.Dims(Settings.MapOtherPins)) return;
                var share = MapLook.PinsShare(Settings.MapOtherPins);
                Dim(map.m_pinRootLarge, share);
                Dim(map.m_pinNameRootLarge, share);
                Keep(dots);
                Keep(squares);
                Keep(counts);
                Keep(map.m_largeMarker);
                Keep(map.m_largeShipMarker);
            }
            foreach (var (group, _, _, on) in Kept)
            {
                if (group == null || on.Layer == null) continue;
                // As the layer stood before, and whatever above it fades the map.
                Chain.Clear();
                Chain.Add((on.Before, on.Group != null && on.Group.ignoreParentGroups));
                for (var at = on.Layer.parent; at != null; at = at.parent)
                {
                    var above = at.GetComponent<CanvasGroup>();
                    if (above != null) Chain.Add((above.alpha, above.ignoreParentGroups));
                }
                var alpha = SeenOnMap.AsShown(Chain);
                if (group.alpha != alpha) group.alpha = alpha;
            }
        }

        /// <summary>The veil laid over the map's picture, first in it and as big as it, made once for each picture.</summary>
        private static void Veil(RawImage picture, float alpha)
        {
            if (picture == null) return;
            if (alpha <= 0f)
            {
                if (_veil != null && _veil.gameObject.activeSelf) _veil.gameObject.SetActive(false);
                return;
            }
            if (_veil == null || _veil.transform.parent != picture.transform)
            {
                if (_veil != null) Object.Destroy(_veil.gameObject);
                var rect = (RectTransform)new GameObject("Scry map veil", typeof(RectTransform)).transform;
                rect.SetParent(picture.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _veil = rect.gameObject.AddComponent<Image>();
                // Clicks go through to the map.
                _veil.raycastTarget = false;
            }
            if (!Mathf.Approximately(_veil.color.a, alpha)) _veil.color = Skin.Alpha(Skin.OnAccent, alpha);
            if (_veil.transform.GetSiblingIndex() != 0) _veil.transform.SetAsFirstSibling();
            if (!_veil.gameObject.activeSelf) _veil.gameObject.SetActive(true);
        }

        private static void Dim(RectTransform layer, float share)
        {
            if (layer == null) return;
            var group = Group(layer);
            var dimmed = new Dimmed { Layer = layer, Group = group, Before = group.alpha };
            group.alpha = SeenOnMap.Dimmed(dimmed.Before, share);
            Layers.Add(dimmed);
        }

        /// <summary>Something on a dimmed layer kept at the map's brightness: a group of its own ignoring the layer's.</summary>
        private static void Keep(RectTransform thing)
        {
            if (thing == null) return;
            Dimmed on = null;
            foreach (var layer in Layers) if (thing.IsChildOf(layer.Layer)) on = layer;
            if (on == null) return;
            var group = Group(thing);
            Kept.Add((group, group.alpha, group.ignoreParentGroups, on));
            group.ignoreParentGroups = true;
        }

        /// <summary>A thing's group, laid at full where it has none.</summary>
        private static CanvasGroup Group(RectTransform thing)
        {
            var group = thing.GetComponent<CanvasGroup>();
            return group != null ? group : thing.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>The layers as they were, and what was kept bright on them following them again; nothing to do while none is dimmed.</summary>
        public static void Restore()
        {
            if (_veil != null && _veil.gameObject.activeSelf) _veil.gameObject.SetActive(false);
            if (Layers.Count == 0) return;
            foreach (var (group, before, ignored, _) in Kept)
            {
                if (group == null) continue;
                group.alpha = before;
                group.ignoreParentGroups = ignored;
            }
            foreach (var layer in Layers) if (layer.Group != null) layer.Group.alpha = layer.Before;
            Kept.Clear();
            Layers.Clear();
        }
    }
}
