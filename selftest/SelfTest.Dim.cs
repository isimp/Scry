using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>The dungeons the inside view is judged by: a cave, a stack of tall rooms, a flat crypt and two that wind down.</summary>
        private static readonly string[] ReferenceDungeons = { "MountainCave02", "MorkBorg", "SunkenCrypt4", "TheHole01", "Hildir_cave" };

        /// <summary>
        /// Dimmed rooms show darker on the stage. For each reference dungeon, with the roof on, the
        /// stage is filmed with every room put away, whole and dimmed, and the rooms' pixels read
        /// (<see cref="DimLook"/>); each must keep at most 60% of its brightness. The shaders its
        /// rooms are drawn with are told, each with how many of their parts the dimming reached,
        /// those whose shaders take a colour (<see cref="ShaderTally"/>).
        /// </summary>
        private static IEnumerator DimShows(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var pale = new List<string>();
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.CreaturesWaiting == 0, 60);
                if (CopyOf(entry) == null || Stage.ExampleRoomsTotal == 0)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.OpenLevel(Stage.FloorHeights.Count);
                yield return Until(() => !Stage.Gliding, 5);

                var tally = new ShaderTally();
                foreach (var copy in Stage.ExampleRoomCopies())
                {
                    foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material == null || material.shader == null) continue;
                            tally.Add(material.shader.name, Dim.Reaches(material), Colours(material.shader));
                        }
                    }
                }
                p.Note($"{entry.Name} ({entry.DisplayName}) is drawn with: " + string.Join("; ", tally.Lines()));

                float[] empty = null, whole = null, dimmed = null;
                yield return Film(PlanRoomShown.None, film => empty = film);
                yield return Film(PlanRoomShown.Whole, film => whole = film);
                yield return Picture(p, $"dim {entry.Name} whole");
                yield return Film(PlanRoomShown.Faint, film => dimmed = film);
                yield return Picture(p, $"dim {entry.Name} dimmed");

                var (pixels, share) = DimLook.Read(empty, whole, dimmed);
                var told = share is float kept
                    ? $"{entry.Name}: dimmed, its rooms keep {Numbers.Percent(kept)} of their brightness, over {Numbers.Count(pixels)} pixels"
                    : $"{entry.Name}: none of its rooms' pixels were read";
                p.Note(told);
                if (!DimLook.DarkEnough(share)) pale.Add(told);
                Stage.ExampleHoldLook(null);
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(pale.Count == 0, "dimmed rooms show at least 40% darker on the stage", string.Join("; ", pale));
        }

        /// <summary>Puts the example's rooms back to what the floor opened has, after <see cref="DimShows"/>.</summary>
        private static void LookAsItWas() => Stage.ExampleHoldLook(null);

        /// <summary>Holds the example's rooms to a look, waits until every room has it and the stage has been filmed twice since, and hands over the film's brightness.</summary>
        private static IEnumerator Film(PlanRoomShown look, Action<float[]> read)
        {
            Stage.ExampleHoldLook(look);
            yield return Until(() => Stage.ExampleRoomsKept, 5);
            var filmed = Stage.Filmed;
            yield return Until(() => Stage.Filmed >= filmed + 2, 3);
            read(StageBrightness());
        }

        /// <summary>Each pixel's brightness in the stage's last film, row by row; null without one.</summary>
        private static float[] StageBrightness()
        {
            if (!(Stage.Texture is RenderTexture film)) return null;
            var was = RenderTexture.active;
            var picture = new Texture2D(film.width, film.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = film;
                picture.ReadPixels(new Rect(0, 0, film.width, film.height), 0, 0, false);
                var pixels = picture.GetPixels32();
                var brightness = new float[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) brightness[i] = DimLook.Brightness(pixels[i].r, pixels[i].g, pixels[i].b);
                return brightness;
            }
            finally
            {
                RenderTexture.active = was;
                UnityEngine.Object.Destroy(picture);
            }
        }

        /// <summary>The colours a shader takes, by name.</summary>
        private static List<string> Colours(Shader shader)
        {
            var colours = new List<string>();
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color) colours.Add(shader.GetPropertyName(i));
            }
            return colours;
        }
    }
}
