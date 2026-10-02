using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The textures, render textures and meshes Scry makes to keep (the stage's picture, its
    /// floors and skies, the ground's mask, the mods' icons, the panel's shapes), noted as they
    /// are made so the resource monitor can tell how many there are and what memory they take
    /// without searching all of Unity's objects. Those destroyed drop out as they are counted.
    /// </summary>
    internal static class Kept
    {
        private static readonly List<Object> Things = new List<Object>();

        /// <summary>Notes a thing Scry made and keeps; gives it back.</summary>
        public static T Add<T>(T thing) where T : Object
        {
            if (thing != null) Things.Add(thing);
            return thing;
        }

        /// <summary>How many textures, render textures and meshes Scry keeps, and their memory in bytes.</summary>
        public static (int Textures, long TextureBytes, int Renders, long RenderBytes, int Meshes, long MeshBytes) Tally()
        {
            Things.RemoveAll(thing => thing == null);
            int textures = 0, renders = 0, meshes = 0;
            long textureBytes = 0, renderBytes = 0, meshBytes = 0;
            foreach (var thing in Things)
            {
                var size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(thing);
                if (thing is RenderTexture)
                {
                    renders++;
                    renderBytes += size;
                }
                else if (thing is Texture)
                {
                    textures++;
                    textureBytes += size;
                }
                else if (thing is Mesh)
                {
                    meshes++;
                    meshBytes += size;
                }
            }
            return (textures, textureBytes, renders, renderBytes, meshes, meshBytes);
        }
    }
}
