using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What a mod manager puts beside a mod: the package folder it installs the mod in, named
    /// "Author-Name", with the package's manifest.json, icon.png and README.md. A mod dropped
    /// into the plugins folder by hand has none of these, and its page goes without them.
    /// </summary>
    internal static class ModFolders
    {
        /// <summary>The largest an icon is kept, in pixels a side: the list draws it small and the card a little bigger.</summary>
        private const int IconSize = 96;

        /// <summary>Each icon read, by its file, kept from world to world as the catalog is made again.</summary>
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The package folder a plugin was installed in: the nearest folder above its assembly
        /// holding a manifest.json, below the plugins folder itself; null when there is none.
        /// </summary>
        public static string PackageFolder(string location)
        {
            if (string.IsNullOrEmpty(location)) return null;
            Guard.Each(Feature.ModPages, "mods' folders", location, () =>
            {
                var root = Path.GetFullPath(BepInEx.Paths.PluginPath).TrimEnd('\\', '/');
                var folder = Path.GetDirectoryName(Path.GetFullPath(location));
                while (!string.IsNullOrEmpty(folder) && folder.Length > root.Length + 1 && folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(Path.Combine(folder, "manifest.json"))) return folder;
                    folder = Path.GetDirectoryName(folder);
                }
                return null;
            }, out string package);
            return package;
        }

        /// <summary>A package folder's manifest, or null when it cannot be read.</summary>
        public static ModManifest Manifest(string folder)
        {
            Guard.Each(Feature.ModPages, "mods' folders", folder, () => ModManifest.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json"))), out var manifest);
            return manifest;
        }

        /// <summary>A file of the folder by its name, whatever its case ("readme.md" too), or "" when there is none.</summary>
        public static string FileIn(string folder, string name)
        {
            Guard.Each(Feature.ModPages, "mods' folders", folder, () =>
                Directory.GetFiles(folder).FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase)), out var found);
            return found ?? "";
        }

        /// <summary>A mod's readme as plain text, at most so many characters, or "" when it cannot be read.</summary>
        public static string Readme(string path, int limit)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return Guard.Each(Feature.ModPages, "mods' readmes", path, () => ReadmeText.Plain(File.ReadAllText(path), limit), out var text) ? text : "";
        }

        /// <summary>
        /// A package's icon as a sprite, made smaller when it is big (a store's icon is 256
        /// pixels a side) so a hundred of them weigh little; null when it cannot be read.
        /// </summary>
        public static Sprite Icon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Icons.TryGetValue(path, out var known)) return known;
            Sprite sprite = null;
            Guard.Each(Feature.ModPages, "mods' icons", path, () =>
            {
                sprite = Read(path);
            });
            Icons[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// The game's own picture reader (<c>ImageConversion.LoadImage</c>), found by name: its
        /// assembly is built against a newer .NET Standard than Scry can reference.
        /// </summary>
        private static MethodInfo _loadImage;
        private static bool _lookedForLoadImage;

        private static bool LoadImage(Texture2D texture, byte[] bytes)
        {
            if (!_lookedForLoadImage)
            {
                _lookedForLoadImage = true;
                _loadImage = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false)?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                if (_loadImage == null) Faults.Tell(Feature.ModPages, "reading the mods' icons", new MissingMethodException("ImageConversion", "LoadImage"));
            }
            return _loadImage != null && _loadImage.Invoke(null, new object[] { texture, bytes }) is bool read && read;
        }

        private static Sprite Read(string path)
        {
            var file = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!LoadImage(file, File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(file);
                return null;
            }

            var texture = file;
            if (file.width > IconSize || file.height > IconSize)
            {
                var scale = (float)IconSize / Mathf.Max(file.width, file.height);
                var width = Mathf.Max(1, Mathf.RoundToInt(file.width * scale));
                var height = Mathf.Max(1, Mathf.RoundToInt(file.height * scale));
                var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var active = RenderTexture.active;
                try
                {
                    Graphics.Blit(file, target);
                    RenderTexture.active = target;
                    texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                }
                finally
                {
                    RenderTexture.active = active;
                    RenderTexture.ReleaseTemporary(target);
                    UnityEngine.Object.Destroy(file);
                }
            }
            // Kept from world to world: the game unloads what nothing in its scenes uses.
            texture.name = "Scry mod icon";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, true);
            var sprite = Sprite.Create(Kept.Add(texture), new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
