using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the stage: each kind shown and told, its controls and
    /// adjustments, switching fast and leaving nothing behind, and its ground, its light and
    /// its picture, with what reads the picture back.
    /// </summary>
    internal static partial class SelfTest
    {
        // ----- Each kind on the stage -----

        private static IEnumerator Shows(Probe p, Entry entry, bool facts)
        {
            if (entry == null) p.Skip("there is no entry of that kind to show");
            p.Note($"{entry.Name} ({entry.DisplayName})");
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);

            var copy = CopyOf(entry);
            p.Check(copy != null, "a copy stands on the stage");
            if (entry.Kind != Kind.Effect) p.Check(Renderers(copy) > 0, "it has something to draw", $"{Numbers.Count(Renderers(copy))} renderers");
            if (ScryPanel.Compact) p.Note("the panel is in its compact view, which has no stage to draw");
            else
            {
                yield return Until(() => Stage.Texture != null, 3);
                p.Check(Stage.Texture != null, "the stage is drawn");
            }

            if (!facts) yield break;
            var told = Facts.For(entry);
            p.Check(!told.IsEmpty, "its details tell something");
            p.Note($"{Numbers.Count(told.Pairs.Count)} facts, {Numbers.Count(told.Rows.Count)} rows: {Pairs(told).Substring(0, Math.Min(160, Pairs(told).Length))}");
        }

        private static Entry StatusOnPerson() =>
            X.Catalog.FirstOrDefault(e => e.Kind == Kind.StatusEffect && e.Name == "Burning" && Looks.ShowsOnPerson(e))
            ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.StatusEffect && Looks.ShowsOnPerson(e));

        // ----- Controls and adjustments -----

        /// <summary>Every lighting, backdrop and view the stage offers draws, and the person and the spin come and go.</summary>
        private static IEnumerator StageControls(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Boar");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null, 10);
            if (!p.Check(CopyOf(troll) != null, "a copy stands on the stage")) yield break;

            var lighting = Stage.LightingIndex;
            var backdrop = Stage.BackdropIndex;
            var person = Stage.ShowPerson;
            var spin = Stage.Spin;
            var pitch = Stage.Pitch;
            var yaw = Stage.Yaw;
            try
            {
                for (var i = 0; i < Stage.LightingNames.Length; i++)
                {
                    Stage.LightingIndex = i;
                    yield return null;
                }
                p.Check(Stage.LightingIndex == Stage.LightingNames.Length - 1, "every lighting can be set", string.Join(", ", Stage.LightingNames));
                for (var i = 0; i < Stage.BackdropNames.Length; i++)
                {
                    Stage.BackdropIndex = i;
                    yield return null;
                }
                p.Check(Stage.BackdropIndex == Stage.BackdropNames.Length - 1, "every backdrop can be set", string.Join(", ", Stage.BackdropNames));
                Stage.ShowPerson = !person;
                yield return null;
                Stage.ShowPerson = person;
                Stage.Spin = !spin;
                yield return null;
                Stage.Spin = spin;
                Stage.View("Top");
                yield return null;
                p.Check(Stage.Pitch > 60f, "Top looks down on it", $"{Numbers.Amount(Stage.Pitch, 0)} degrees");
                Stage.View("Side");
                yield return null;
                p.Check(Mathf.Abs(Mathf.DeltaAngle(Stage.Yaw, 90f)) < 1f, "Side looks at it from the side", $"{Numbers.Amount(Stage.Yaw, 0)} degrees");
                Stage.View("Front");
                yield return null;
                Stage.View("Fit");
                yield return null;
                p.Check(CopyOf(troll) != null, "the copy stays through it all");
            }
            finally
            {
                Stage.LightingIndex = lighting;
                Stage.BackdropIndex = backdrop;
                Stage.ShowPerson = person;
                Stage.Spin = spin;
                Stage.Pitch = pitch;
                Stage.Yaw = yaw;
            }
        }

        /// <summary>Making the model bigger makes its copy bigger, and putting it back puts it back.</summary>
        private static IEnumerator SizeAdjusts(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar", "Deer");
            if (boar == null) p.Skip("there is no boar");
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            var copy = CopyOf(boar);
            if (!p.Check(copy != null, "a copy stands on the stage")) yield break;
            var before = Stage.SubjectSize.magnitude;
            var scale = X.Modifiers.Scale;
            X.Modifiers.Scale = scale * 2f;
            yield return null;
            yield return null;
            var bigger = Stage.SubjectSize.magnitude;
            p.Check(bigger > before * 1.8f, "twice the size makes it about twice as big", $"{Numbers.Fixed(before, 1)} m, then {Numbers.Fixed(bigger, 1)} m");
            X.Modifiers.Scale = scale;
            yield return null;
            yield return null;
            p.Check(Mathf.Abs(Stage.SubjectSize.magnitude - before) < before * 0.05f + 0.01f, "and back again", $"{Numbers.Fixed(Stage.SubjectSize.magnitude, 1)} m");
        }

        /// <summary>A creature shown with stars, and in each of its looks, still stands; its details tell what stars do.</summary>
        private static IEnumerator StarsAndLooks(Probe p)
        {
            var creature = Pick(Kind.Creature, "Greydwarf", "Boar");
            if (creature == null) p.Skip("there is no creature");
            Select(creature);
            yield return Until(() => CopyOf(creature) != null, 10);
            if (!p.Check(CopyOf(creature) != null, "a copy stands on the stage")) yield break;
            var mods = X.Modifiers;
            p.Note($"{creature.Name}: up to {Numbers.Count(mods.MaxLevel)} levels, {Numbers.Count(mods.LookNames.Length)} looks");
            if (mods.MaxLevel > 1)
            {
                var before = CopyOf(creature);
                mods.Level = mods.MaxLevel;
                yield return Until(() => CopyOf(creature) != null && CopyOf(creature) != before, 5);
                p.Check(CopyOf(creature) != null, $"it stands with {Numbers.Count(mods.MaxLevel - 1)} stars");
                p.Check(Tells(Facts.For(creature), "Health with stars"), "its details tell what stars add");
                mods.Level = 1;
            }
            var looks = mods.LookNames.Length;
            for (var look = 0; look < Math.Min(looks, 4); look++)
            {
                mods.Look = look;
                yield return new Wait(0.2);
                p.Check(CopyOf(creature) != null, $"it stands in the look \"{mods.LookNames[look]}\"");
            }
        }

        /// <summary>A slower animation slows the copy's animator, and a piece stands in every state of wear.</summary>
        private static IEnumerator SpeedAndWear(Probe p)
        {
            var creature = Pick(Kind.Creature, "Boar", "Deer");
            if (creature == null) p.Skip("there is no creature");
            Select(creature);
            yield return Until(() => CopyOf(creature) != null, 10);
            X.Modifiers.AnimationSpeed = 0.5f;
            yield return null;
            yield return null;
            var animator = CopyOf(creature)?.GetComponentInChildren<Animator>();
            p.Check(animator != null && Mathf.Abs(animator.speed - 0.5f) < 0.01f, "half the speed slows its animator to half", animator != null ? $"{Numbers.Amount(animator.speed)}" : "no animator");
            X.Modifiers.AnimationSpeed = 1f;

            var wall = Pick(Kind.Piece, "wood_wall_half", "wood_wall", "stone_wall_1x1");
            if (wall == null) yield break;
            Select(wall);
            yield return Until(() => CopyOf(wall) != null, 10);
            if (!X.Modifiers.WearAvailable)
            {
                p.Note($"{wall.Name} shows no wear");
                yield break;
            }
            foreach (Wear wear in Enum.GetValues(typeof(Wear)))
            {
                X.Modifiers.Wear = wear;
                yield return new Wait(0.2);
                p.Check(CopyOf(wall) != null, $"{wall.Name} stands {wear.ToString().ToLowerInvariant()}");
            }
        }

        /// <summary>Loudness follows the adjustment, and a new selection starts every adjustment afresh.</summary>
        private static IEnumerator AdjustmentsStartAfresh(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar");
            var neck = Pick(Kind.Creature, "Neck", "Deer");
            if (boar == null || neck == null) p.Skip("there is no boar or neck");
            Select(boar);
            yield return null;
            X.Modifiers.Volume = 0.5f;
            X.Modifiers.Scale = 2f;
            yield return null;
            p.Check(Mathf.Abs(Loudness.Gain - 0.5f) < 0.01f, "half the loudness is what previews play at", $"{Numbers.Amount(Loudness.Gain)}");
            Select(neck);
            yield return null;
            p.Check(Mathf.Abs(X.Modifiers.Scale - 1f) < 0.001f && Mathf.Abs(X.Modifiers.Volume - 1f) < 0.001f, "selecting another starts at its own size and loudness", $"size {Numbers.Amount(X.Modifiers.Scale)}, loudness {Numbers.Amount(X.Modifiers.Volume)}");
            yield return null;
            p.Check(Mathf.Abs(Loudness.Gain - 1f) < 0.01f, "and previews play at the game's own loudness again");
        }

        /// <summary>Selecting a different entry every frame, across every kind, leaves nothing broken and the last one shown.</summary>
        private static IEnumerator FastSwitching(Probe p)
        {
            var all = X.Catalog.Where(e => e.Kind != Kind.Location && e.Kind != Kind.Mod).OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
            var picks = Spread(all, 60);
            foreach (var entry in picks)
            {
                Select(entry);
                yield return null;
            }
            var last = picks.LastOrDefault(e => Stage.IsStaged(e) && e.Source is GameObject);
            if (last != null)
            {
                Select(last);
                yield return Until(() => CopyOf(last) != null, 5);
                p.Check(CopyOf(last) != null, $"after {Numbers.Count(picks.Count)} in as many frames, the last shows");
            }
            else p.Check(X.Selected == picks.Last(), $"after {Numbers.Count(picks.Count)} in as many frames, the last is selected");
        }

        /// <summary>After many other entries, the stage holds no more than it held before.</summary>
        private static IEnumerator NoLeftovers(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar", "Deer");
            if (boar == null) p.Skip("there is no creature");
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            yield return Until(() => Stage.PlayedCount == 0, 10);
            yield return null;
            var held = Stage.Held;
            var others = Spread(X.Catalog.Where(e => e.Kind != Kind.Location && e.Kind != Kind.Mod).OrderBy(e => e.Key, StringComparer.Ordinal).ToList(), 40);
            foreach (var entry in others)
            {
                Select(entry);
                yield return null;
                yield return null;
            }
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            yield return Until(() => Stage.Held <= held, 10);
            p.Check(Stage.Held == held, $"after {Numbers.Count(others.Count)} others, the stage holds what it held before", $"{Numbers.Count(Stage.Held)} things, {Numbers.Count(held)} before");
        }

        // ----- The ground, the light and the picture -----

        /// <summary>
        /// Every biome is an entry whose page tells its weathers and what lives there; every biome
        /// an entry names has a page to go to; a biome's music plays and stops.
        /// </summary>
        /// <summary>
        /// What the world's ground is drawn with (Heightmap.m_material): its shader and every
        /// texture it takes, by name, for laying a biome's ground under the stage.
        /// </summary>
        private static IEnumerator TerrainMaterial(Probe p)
        {
            var ground = Player.m_localPlayer != null ? Heightmap.FindHeightmap(Player.m_localPlayer.transform.position) : null;
            if (ground == null) ground = UnityEngine.Object.FindAnyObjectByType<Heightmap>();
            if (!p.Check(ground != null && ground.m_material != null, "the world's ground has a material")) yield break;
            var material = ground.m_material;
            var textures = material.GetTexturePropertyNames()
                .Select(name => { var texture = material.GetTexture(name); return $"{name}: {(texture != null ? $"{texture.name} ({texture.GetType().Name}, {Numbers.Count(texture.width)}x{Numbers.Count(texture.height)})" : "none")}"; });
            p.Note($"shader {(material.shader != null ? material.shader.name : "none")}; textures {string.Join("; ", textures)}");
            yield break;
        }

        /// <summary>
        /// The world's lights stay off the stage: a directional light made in the world, as a
        /// lightning strike's flash is, leaves the stage's picture as it was. The stage stands
        /// still on the plain backdrop meanwhile, as grass and water move with the wind; what
        /// changes between two pictures with no light made is noted, and the light may change
        /// no more than that and a hundredth.
        /// </summary>
        private static IEnumerator WorldLightsOff(Probe p)
        {
            var entry = Pick(Kind.Piece, "piece_workbench", "wood_wall_half");
            if (entry == null) p.Skip("there is no piece to stand on the stage");
            var spin = Stage.Spin;
            var backdrop = Stage.BackdropIndex;
            Stage.Spin = false;
            Stage.BackdropIndex = 0;
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);
            var settled = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - settled > 0.6f && !Stage.Gliding, 4);
            var middle = new Rect(0.3f, 0.3f, 0.4f, 0.4f);
            var before = PictureWithin(middle, out _, out _);
            yield return null;
            yield return null;
            var again = PictureWithin(middle, out _, out _);

            var flash = new GameObject("Scry self-test flash");
            var light = flash.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.red;
            light.intensity = 8f;
            flash.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            yield return null;
            yield return null;
            var lit = PictureWithin(middle, out _, out _);
            UnityEngine.Object.Destroy(flash);
            Stage.Spin = spin;
            Stage.BackdropIndex = backdrop;

            int Changed(Color32[] a, Color32[] b) => a != null && b != null && a.Length == b.Length ? a.Where((c, i) => Apart(c, b[i])).Count() : -1;
            var still = Changed(before, again);
            var differ = Changed(again, lit);
            var shown = before?.Count(c => c.r + c.g + c.b > 60) ?? 0;
            p.Check(before != null && shown > 0, "the piece shows in the picture", $"{Numbers.Count(shown)} bright points");
            p.Note($"with no light made, {Numbers.Count(still)} of {Numbers.Count(before?.Length ?? 0)} points changed between two pictures");
            p.Check(still >= 0 && differ >= 0 && differ <= still + (before?.Length ?? 0) / 100, "a directional light of the world, as a lightning flash is, leaves the stage's picture as it was", $"{Numbers.Count(differ)} of {Numbers.Count(before?.Length ?? 0)} points changed");
        }

        /// <summary>
        /// The Ground backdrop: a piece stands on its own biome's ground, and each biome's ground,
        /// laid in turn with the sky, looks its own, each told by the colour of the ground before
        /// the model, its grass and water, and saved as a picture of the stage beside the log; the
        /// Meadows grow grass, the sea's floor has water over it, a place paints its paths on the
        /// ground, and a dungeon room keeps the plain floor.
        /// </summary>
        private static IEnumerator GroundBackdrop(Probe p)
        {
            var entry = Pick(Kind.Piece, "piece_workbench", "wood_wall_half");
            if (entry == null) p.Skip("there is no piece to stand on the stage");
            var backdrop = Stage.BackdropIndex;
            var spin = Stage.Spin;
            var choice = Stage.GroundChoice;
            Stage.Spin = false;
            Stage.GroundChoice = "Auto";
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);
            Stage.BackdropIndex = 4;
            yield return Until(() => Stage.GroundShown != null, 3);
            if (!p.Check(Stage.GroundShown != null, "with the Ground backdrop it stands on ground", "the world's terrain had no material to borrow"))
            {
                Stage.BackdropIndex = backdrop;
                Stage.Spin = spin;
                Stage.GroundChoice = choice;
                yield break;
            }
            p.Check(Stage.GroundShown == StageGround.BiomeFor(entry.Biomes), "the ground of its own biome", $"{Stage.GroundShown} for {string.Join(", ", entry.Biomes)}");
            p.Note("its material: " + Stage.GroundMaterialTold());

            // Whether it draws with each distance its shader may hide it by: the material's own, none, and far.
            var hide = Stage.GroundHideDistance;
            var tried = new List<string>();
            foreach (var distance in new float?[] { null, 0f, 200f, 100000f })
            {
                Stage.GroundHideDistance = distance;
                yield return null;
                yield return null;
                yield return null;
                var below = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var bare = below?.Count(c => !Apart(c, below[0])) ?? 0;
                tried.Add($"{(distance.HasValue ? Numbers.Amount(distance.Value) : "the material's own")}: {(below == null ? "no picture" : below.Length - bare > below.Length / 10 ? "draws" : "nothing")}");
            }
            // And with the stage's camera rendering each way.
            foreach (var path in new[] { RenderingPath.DeferredShading, RenderingPath.Forward })
            {
                Stage.PathOverride = path;
                Stage.GroundHideDistance = 100000f;
                yield return null;
                yield return null;
                yield return null;
                var below = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var bare = below?.Count(c => !Apart(c, below[0])) ?? 0;
                tried.Add($"{path}: {(below == null ? "no picture" : below.Length - bare > below.Length / 10 ? "draws" : "nothing")}");
            }
            Stage.PathOverride = null;
            Stage.GroundHideDistance = hide;
            p.Note("hidden by distance, and by the camera's path: " + string.Join("; ", tried));
            p.Note("after: " + Stage.GroundMaterialTold());

            var folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest-ground");
            System.IO.Directory.CreateDirectory(folder);
            var told = new List<string>();
            var looks = new List<Color32>();
            var grass = new Dictionary<string, int>();
            var water = new Dictionary<string, bool>();
            Stage.BackdropIndex = 5;
            foreach (var biome in new[] { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" })
            {
                Stage.GroundBiomeOverride = biome;
                yield return null;
                yield return null;
                yield return null;
                grass[biome] = Stage.GrassCount;
                water[biome] = Stage.WaterShown;
                var front = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var whole = PictureWithin(new Rect(0f, 0f, 1f, 1f), out var width, out var height);
                if (whole != null) WriteTga(System.IO.Path.Combine(folder, $"stage-{biome}.tga"), width, height, whole);
                if (front == null || front.Length == 0) continue;
                long r = 0, g = 0, b = 0;
                foreach (var pixel in front)
                {
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                }
                var look = new Color32((byte)(r / front.Length), (byte)(g / front.Length), (byte)(b / front.Length), 255);
                looks.Add(look);
                told.Add($"{biome} #{Numbers.Hex(look.r, 2)}{Numbers.Hex(look.g, 2)}{Numbers.Hex(look.b, 2)}, {Numbers.Count(grass[biome])} grass{(water[biome] ? ", water" : "")}");
            }
            Stage.GroundBiomeOverride = null;
            p.Check(grass["Meadows"] > 0, "grass grows on the Meadows' ground", $"{Numbers.Count(grass["Meadows"])}");
            p.Check(water["Ocean"] && !water["Meadows"], "the sea's floor has water over it, and only it");
            p.Note($"the ground before it, by biome: {string.Join("; ", told)}; pictures in {folder}");
            var distinct = looks.Where((look, i) => !looks.Take(i).Any(other => !Apart(look, other))).Count();
            p.Check(distinct >= 5, "each biome's ground looks its own", $"{Numbers.Count(distinct)} looks among {Numbers.Count(looks.Count)} biomes");

            // A place paints its paths, dirt and paving on the ground under it.
            var painted = new List<string>();
            foreach (var name in new[] { "Vendor_BlackForest", "WoodVillage1", "WoodFarm1", "StartTemple" })
            {
                var place = X.Catalog.FirstOrDefault(e => e.Name == name && PlaceOf(e) != null && !PlaceOf(e).IsRoom);
                if (place == null) continue;
                Select(place);
                yield return Until(() => CopyOf(place) != null && Stage.GroundShown != null, 20);
                yield return null;
                yield return null;
                painted.Add($"{name} {Numbers.Count(Stage.GroundPaintCount)}");
                if (Stage.GroundPaintCount == 0) continue;
                var whole = PictureWithin(new Rect(0f, 0f, 1f, 1f), out var width, out var height);
                if (whole != null) WriteTga(System.IO.Path.Combine(folder, $"stage-paths-{name}.tga"), width, height, whole);
                // Another biome over the painted ground, its mask grown past its plain size.
                var faults = Faults.Count;
                Stage.GroundBiomeOverride = "Swamp";
                yield return null;
                yield return null;
                Stage.GroundBiomeOverride = null;
                yield return null;
                p.Check(Faults.Count == faults && Stage.GroundShown != null, "another biome over a place's painted ground lays it all the same", $"{Numbers.Count(Faults.Count - faults)} faults");
                break;
            }
            p.Note("paints on their ground: " + string.Join(", ", painted));
            p.Check(painted.Any(t => !t.EndsWith(" 0", StringComparison.Ordinal)), "a place paints its paths on the ground under it", string.Join(", ", painted));

            // Underground there is no ground of the world's.
            var room = X.Catalog.Where(e => PlaceOf(e) != null && PlaceOf(e).IsRoom).OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault();
            if (room != null)
            {
                Select(room);
                yield return Until(() => CopyOf(room) != null, 20);
                yield return null;
                yield return null;
                p.Check(CopyOf(room) != null && Stage.GroundShown == null, "a dungeon room keeps the plain floor", $"{room.Name}: {Stage.GroundShown ?? "no ground"}");
            }

            Stage.BackdropIndex = backdrop;
            Stage.Spin = spin;
            Stage.GroundChoice = choice;
        }

        /// <summary>Waits until the stage's camera stops gliding to what it frames.</summary>
        private static IEnumerator Settled()
        {
            var from = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - from > 0.2f && !Stage.Gliding, 8);
        }

        /// <summary>Where in the stage's picture the part of bounds above a height shows, 0 to 1 across and up; null where any of it is behind the camera.</summary>
        private static Rect? AboveIn(Bounds bounds, float above)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
            foreach (var y in new[] { above, bounds.max.y })
            foreach (var z in new[] { bounds.min.z, bounds.max.z })
            {
                if (!(Stage.PictureOf(new Vector3(x, y, z)) is Vector2 at)) return null;
                minX = Mathf.Min(minX, at.x);
                minY = Mathf.Min(minY, at.y);
                maxX = Mathf.Max(maxX, at.x);
                maxY = Mathf.Max(maxY, at.y);
            }
            return Rect.MinMaxRect(Mathf.Clamp01(minX), Mathf.Clamp01(minY), Mathf.Clamp01(maxX), Mathf.Clamp01(maxY));
        }

        /// <summary>The stage's picture within a part of it (0 to 1 across and up), as read back; null with no picture.</summary>
        private static Color32[] PictureWithin(Rect part, out int width, out int height)
        {
            width = height = 0;
            if (!(Stage.Texture is RenderTexture picture)) return null;
            var x = Mathf.Clamp(Mathf.FloorToInt(part.xMin * picture.width), 0, picture.width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(part.yMin * picture.height), 0, picture.height - 1);
            width = Mathf.Clamp(Mathf.CeilToInt(part.xMax * picture.width), x + 1, picture.width) - x;
            height = Mathf.Clamp(Mathf.CeilToInt(part.yMax * picture.height), y + 1, picture.height) - y;
            var was = RenderTexture.active;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = picture;
                read.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = was;
                UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>Whether two points of a picture differ to the eye.</summary>
        private static bool Apart(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 24;

        /// <summary>A picture saved as an uncompressed TGA, true colour of 32 bits, its rows from the bottom up as read.</summary>
        private static void WriteTga(string path, int width, int height, Color32[] pixels)
        {
            using (var file = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                file.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                file.Write((ushort)width);
                file.Write((ushort)height);
                file.Write((byte)32);
                file.Write((byte)8);
                foreach (var pixel in pixels) file.Write(new[] { pixel.b, pixel.g, pixel.r, pixel.a });
            }
        }
    }
}
