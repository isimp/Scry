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
        /// stage is filmed with every room put away, whole, dimmed, and dimmed with every colour
        /// black, and the rooms' pixels read (<see cref="DimLook"/>): dimmed, each must keep at most
        /// 60% of the brightness its colours give it, what is above the film with every colour
        /// black, as fog and highlights stay however dark the colours, most of dark stone's. The
        /// shaders its rooms are drawn with are told, each with how many of their parts the
        /// dimming reached, those whose shaders take a colour (<see cref="ShaderTally"/>).
        /// </summary>
        private static IEnumerator DimShows(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var pale = new List<string>();
            var unlike = new List<string>();
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

                float[] empty = null, whole = null, dimmed = null, half = null;
                yield return Film(RoomLook.Away, film => empty = film);
                yield return Film(RoomLook.Whole, film => whole = film);
                yield return Picture(p, $"dim {entry.Name} whole");
                yield return Film(RoomLook.Below, film => dimmed = film);
                yield return Picture(p, $"dim {entry.Name} dimmed");
                yield return Film(RoomLook.Near, film => half = film);
                yield return Picture(p, $"dim {entry.Name} half dimmed");

                // Every colour black: what is still bright, fog and highlights, no dimming takes away.
                float[] floor = null;
                yield return Film(RoomLook.Whole, film => { });
                Dim.HoldShare(0f);
                yield return Film(RoomLook.Below, film => floor = film);
                yield return Picture(p, $"dim {entry.Name} every colour black");
                yield return Film(RoomLook.Whole, film => { });
                Dim.HoldShare(null);
                Stage.ExampleHoldLook(null);

                var (pixels, share) = DimLook.Read(empty, whole, dimmed, floor);
                var all = DimLook.Read(empty, whole, dimmed).Share;
                var halfShare = DimLook.Read(empty, whole, half, floor).Share;
                var told = share is float kept
                    ? $"{entry.Name}: dimmed, its rooms keep {Numbers.Percent(kept)} of the brightness their colours give them, over {Numbers.Count(pixels)} pixels"
                      + (all is float ofAll ? $" ({Numbers.Percent(ofAll)} of all of it)" : "")
                      + (halfShare is float halfKept ? $"; half dimmed, {Numbers.Percent(halfKept)}" : "")
                    : $"{entry.Name}: none of its rooms' pixels were read";
                p.Note(told);
                if (!DimLook.DarkEnough(share)) pale.Add(told);
                // Half dimmed, on the level of a room gone into: darker than whole, lighter than below.
                if (!(halfShare is float h && share is float d && h > d && h < 0.95f)) unlike.Add(told);
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(pale.Count == 0, "dimmed rooms keep at most 60% of the brightness their colours give them", string.Join("; ", pale));
            p.Check(unlike.Count == 0, "rooms half dimmed are darker than whole and lighter than those dimmed", string.Join("; ", unlike));
        }

        /// <summary>Puts the example's rooms back to what the floor opened has, dimmed as the dimming does, after <see cref="DimShows"/>.</summary>
        private static void LookAsItWas()
        {
            Dim.HoldShare(null);
            Stage.ExampleHoldLook(null);
        }

        /// <summary>Holds the example's rooms to a look, waits until every room has it and the stage has been filmed twice since, and hands over the film's brightness.</summary>
        private static IEnumerator Film(RoomLook look, Action<float[]> read)
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
