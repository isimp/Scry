using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The game's water in a dungeon's example, as the flood in a sunken crypt's rooms: drawn with
    /// the game's water shader it stands as a flat, lit slab over the floor, as if a floor of its
    /// own. Inside an example it is drawn instead as a faint, see-through sheet of a water's colour
    /// at its own level, so the room's floor shows through it and the water still shows where it
    /// lies; dimmed with its room as anything else is.
    /// </summary>
    internal static class WaterParts
    {
        /// <summary>The game's water shader, by name.</summary>
        public const string Shader = "Custom/Water";

        /// <summary>The sheet's colour: a muddy water's green, a third of it over what is under it.</summary>
        public static readonly Color Sheet = new Color(0.38f, 0.52f, 0.46f, 0.35f);

        /// <summary>The shaders the sheet may be drawn with, the first the game has loaded, each blending its colour over what is under it.</summary>
        private static readonly string[] Shaders = { "Sprites/Default", "Unlit/Transparent", "Legacy Shaders/Particles/Alpha Blended" };

        private static readonly List<Renderer> Found = new List<Renderer>();
        private static Material _sheet;
        private static bool _tried;

        /// <summary>The sheet's material, made once; none where the game has no such shader loaded.</summary>
        private static Material SheetMaterial()
        {
            if (_tried) return _sheet;
            _tried = true;
            foreach (var name in Shaders)
            {
                var shader = UnityEngine.Shader.Find(name);
                if (shader == null) continue;
                _sheet = Kept.Add(new Material(shader) { name = "Scry water sheet", color = Sheet, renderQueue = 3000 });
                return _sheet;
            }
            Log.Warn("Scry found no shader for a dungeon's water; it is drawn as the game draws it.");
            return null;
        }

        /// <summary>Draws a copy's water as the faint sheet, every part drawn with the game's water shader; true where it held any.</summary>
        public static bool MakeFaint(GameObject copy)
        {
            if (copy == null) return false;
            var sheet = SheetMaterial();
            var any = false;
            copy.GetComponentsInChildren(true, Found);
            try
            {
                foreach (var renderer in Found)
                {
                    if (renderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null || materials[i].shader == null || materials[i].shader.name != Shader) continue;
                        any = true;
                        if (sheet == null) continue;
                        materials[i] = sheet;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
                return any;
            }
            finally
            {
                Found.Clear();
            }
        }

        /// <summary>How many of a copy's parts are drawn as the faint sheet, and how many still with the game's water shader, for the self-test.</summary>
        [Diagnostic, ForTest]
        public static (int Sheets, int Game) Counted(GameObject copy)
        {
            var sheets = 0;
            var game = 0;
            if (copy == null) return (0, 0);
            copy.GetComponentsInChildren(true, Found);
            try
            {
                foreach (var renderer in Found)
                {
                    if (renderer == null) continue;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        if (_sheet != null && material == _sheet) sheets++;
                        else if (material.shader != null && material.shader.name == Shader) game++;
                    }
                }
                return (sheets, game);
            }
            finally
            {
                Found.Clear();
            }
        }
    }
}
