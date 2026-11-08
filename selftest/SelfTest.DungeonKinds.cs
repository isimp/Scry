using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for dungeons on the kinds list and for an On the map list longer than fits.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Every kind of dungeon kept, entrances of two Burial Chambers kinds and of another dungeon
        /// seen: the kinds list holds the family under one chip and the other under its own, by their
        /// marks; the chip picked, the other dungeon's mark and its words fade while the family's stand
        /// as they did, the pointer on the faded mark brings it back, and letting go brings all back.
        /// </summary>
        private static IEnumerator DungeonChipsTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            var world = CompendiumMap.World;
            var crypts = DungeonKinds.All.Where(k => k.StartsWith("Crypt", System.StringComparison.Ordinal)).OrderBy(k => k, System.StringComparer.Ordinal).Take(2).ToList();
            var other = DungeonKinds.All.FirstOrDefault(k => k.StartsWith("Mistlands_DvergrTown", System.StringComparison.Ordinal)) ?? DungeonKinds.All.FirstOrDefault(k => !k.StartsWith("Crypt", System.StringComparison.Ordinal));
            if (book == null || player == null || map == null || world == null || Game.m_noMap || !MapKept.Remembered || crypts.Count == 0 || other == null)
            {
                p.Skip("there is no character, no map remembering what is seen, or no two kinds of dungeon in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            NoteMapAsItWas(map);
            Settings.KindsStrip = true;
            yield return MapCodeCompiled(p);
            var clear = ClearGround(map, player.transform.position);
            if (clear == null) p.Note("no explored ground lies away from the map's pins; the marks stand by you");
            var at = clear ?? new Vector2(player.transform.position.x, player.transform.position.z);
            var (cx, cz) = ((int)at.x, (int)at.y);
            var seen = new List<(string, string, int, int)>();
            for (var i = 0; i < crypts.Count; i++) seen.Add((DungeonMaps.EntranceIdOf(world, cx - 200 + i * 100, cz), crypts[i], cx - 200 + i * 100, cz));
            seen.Add((DungeonMaps.EntranceIdOf(world, cx + 200, cz), other, cx + 200, cz));
            var cryptKey = EntryKeys.For(Kind.Location, crypts[0]);
            var otherKey = EntryKeys.For(Kind.Location, other);
            MapSpots.SpotsInsteadForTest(k => new List<(int X, int Z)>());
            MapMarkButtons.SeenInsteadForTest(seen);
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(CompendiumBook.Dungeons, true);
            Session.Hide();
            MapSpots.Hide();
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = 0.08f;
            CentreMapOn(map, at);
            yield return Until(() => MapMarkButtons.MarkOfKindForTest(cryptKey) >= 0 && MapMarkButtons.MarkOfKindForTest(otherKey) >= 0 && MapSpots.KindChips().Count >= 2, 5);

            var chips = MapSpots.KindChips();
            var cryptChip = chips.FirstOrDefault(c => c.Kinds != null && c.Kinds.Contains(cryptKey));
            var otherChip = chips.FirstOrDefault(c => c.Kinds != null && c.Kinds.Contains(otherKey));
            if (cryptChip.Kinds == null || otherChip.Kinds == null)
            {
                p.Check(false, "the kinds list holds a chip for each kind of dungeon by its marks alone", string.Join(", ", chips.Select(c => c.Name)));
                yield break;
            }
            p.Check(cryptChip.Seen >= crypts.Count && otherChip.Seen >= 1, "the kinds list counts each dungeon kind's marks", $"{cryptChip.Name} {Numbers.Count(cryptChip.Seen)}, {otherChip.Name} {Numbers.Count(otherChip.Seen)}");
            p.Check(crypts.All(k => cryptChip.Kinds.Contains(EntryKeys.For(Kind.Location, k))) && !cryptChip.Kinds.Contains(otherKey), "a dungeon's numbered kinds stand under one chip, another dungeon under its own", cryptChip.Name);
            yield return Picture(p, "dungeon marks on the kinds list");

            float Alpha(string kind) => MapMarkButtons.AlphaForTest(MapMarkButtons.MarkOfKindForTest(kind));
            var (cryptWas, otherWas) = (Alpha(cryptKey), Alpha(otherKey));
            MapSpots.ShowAlone(cryptChip.Kinds);
            yield return null;
            yield return null;
            p.Check(Mathf.Approximately(Alpha(cryptKey), cryptWas), "the chip picked, its dungeon's mark stands as it did", $"{Numbers.Fixed(Alpha(cryptKey), 2)} from {Numbers.Fixed(cryptWas, 2)}");
            p.Check(Mathf.Approximately(Alpha(otherKey), SeenOnMap.SoloOthers), "the other dungeon's mark fades as any kind not picked does", Numbers.Fixed(Alpha(otherKey), 2));
            yield return Picture(p, "dungeon marks, one kind picked");

            if (MapMarkButtons.MarkCenterForTest(MapMarkButtons.MarkOfKindForTest(otherKey)) is Vector2 onOther)
            {
                PointerTo(onOther);
                yield return Until(() => MapMarkButtons.NameShownForTest && Alpha(otherKey) >= 0.99f, 3);
                p.Check(MapMarkButtons.NameShownForTest && Alpha(otherKey) >= 0.99f, "the pointer on a faded mark brings it back to read", Numbers.Fixed(Alpha(otherKey), 2));
                if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
                yield return Until(() => !MapMarkButtons.NameShownForTest, 3);
            }

            MapSpots.ShowAlone(cryptChip.Kinds);
            yield return null;
            yield return null;
            p.Check(Mathf.Approximately(Alpha(otherKey), otherWas), "the chip let go, every mark stands as it did", Numbers.Fixed(Alpha(otherKey), 2));

            // Taken off the map, its kinds leave the list.
            ScryPanel.KeepOnMap(CompendiumBook.Dungeons, false);
            yield return Until(() => MapSpots.MarkedKindsForTest == 0, 3);
            p.Check(MapSpots.MarkedKindsForTest == 0, "every kind of dungeon taken off, the list holds none of them");
            DungeonChipsAsItWas();
        }

        /// <summary>What the part changed put back, should it have stopped before it did.</summary>
        private static void DungeonChipsAsItWas()
        {
            MapMarkButtons.SeenInsteadForTest(null);
            CrowdedAsItWas();
        }

        /// <summary>
        /// Copper, tin and a place kept: each in its category's shape, the kinds list and the On the
        /// map list under the same headings. A heading picks every kind under it and fades the rest,
        /// its arrow folds it, and on the list its × takes the category off. Pictured.
        /// </summary>
        private static IEnumerator CategoriesTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            const string copper = "rock4_copper", tin = "MineRock_Tin";
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && !e.IsRoom && !DungeonKinds.Is(e.Name));
            if (book == null || player == null || map == null || Game.m_noMap || X.Find(copper) == null || X.Find(tin) == null || place == null)
            {
                p.Skip("there is no character, no map, or no copper, tin or place in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            NoteMapAsItWas(map);
            Settings.KindsStrip = true;
            Settings.FarMarks = FarMarks.Full;
            yield return MapCodeCompiled(p);
            var clear = ClearGround(map, player.transform.position);
            var at = clear ?? new Vector2(player.transform.position.x, player.transform.position.z);
            var (cx, cz) = ((int)at.x, (int)at.y);
            var placeKey = place.Key;
            MapSpots.SpotsInsteadForTest(k => k == copper ? new List<(int X, int Z)> { (cx - 120, cz), (cx - 120, cz + 60) } : k == tin ? new List<(int X, int Z)> { (cx - 60, cz) } : k == placeKey ? new List<(int X, int Z)> { (cx + 60, cz), (cx + 120, cz) } : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(copper, true);
            ScryPanel.KeepOnMap(tin, true);
            ScryPanel.KeepOnMap(placeKey, true);
            p.Check(KeptMarks.ShapeOf(book.MarkOf(copper) ?? 0) == MarkShape.Triangle && KeptMarks.ShapeOf(book.MarkOf(tin) ?? 0) == MarkShape.Triangle && book.MarkOf(copper) != book.MarkOf(tin), "what is found stands in triangles, each kind its colour");
            p.Check(KeptMarks.ShapeOf(book.MarkOf(placeKey) ?? 24) == MarkShape.Circle, "a place stands in a circle");
            Session.Hide();
            MapSpots.Hide();
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = 0.08f;
            CentreMapOn(map, at);
            // What was kept before is gone from the list a frame after it is taken off.
            yield return Until(() => MapSpots.KindGroups().Count == 2 && MapSpots.KindGroups()[0].Category == KeptCategory.Locations, 5);
            var groups = MapSpots.KindGroups();
            p.Check(groups.Count == 2 && groups[0].Category == KeptCategory.Locations && groups[1].Category == KeptCategory.Resources && groups[1].Kinds.Length == 2, "the kinds list stands each under its category", string.Join(", ", groups.Select(g => SpotWords.CategoryName(g.Category))));
            yield return Picture(p, "the kinds list by category");

            if (groups.Count == 2)
            {
                MapSpots.ShowAlone(groups[1].Kinds);
                yield return Until(() => MapSpots.FadedDotsForTest >= 2, 3);
                p.Check(MapSpots.IsPicked(copper) && MapSpots.IsPicked(tin) && !MapSpots.IsPicked(placeKey) && MapSpots.FadedDotsForTest >= 2, "a heading picks every kind under it and fades the rest", Numbers.Count(MapSpots.FadedDotsForTest));
                yield return Picture(p, "the kinds list, a category picked");
                MapSpots.ShowAlone(groups[1].Kinds);
                yield return Until(() => MapSpots.FadedDotsForTest == 0, 3);
                p.Check(MapSpots.PickedCount == 0, "picked again, its kinds are let go");
            }
            var was = ScryPanel.KindCategoryFoldedForTest(KeptCategory.Resources);
            ScryPanel.FoldKindCategoryForTest(KeptCategory.Resources);
            p.Check(ScryPanel.KindCategoryFoldedForTest(KeptCategory.Resources) != was, "its arrow folds a category");
            yield return Picture(p, "the kinds list, a category folded");
            ScryPanel.FoldKindCategoryForTest(KeptCategory.Resources);

            map.SetMapMode(Minimap.MapMode.Small);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var lists = ScryPanel.Drawn(PanelPart.KeptList);
            ScryPanel.OpenKeptListForTest(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.KeptList) > lists + 1, 3);
            var lines = ScryPanel.KeptLinesForTest;
            p.Check(ScryPanel.KeptLineForTest(KeptLineLook.Heading, KeptCategory.Locations) >= 0 && ScryPanel.KeptLineForTest(KeptLineLook.Heading, KeptCategory.Resources) >= 0 && ScryPanel.KeptLineForTest(KeptLineLook.Kept, KeptCategory.Resources) >= 0, "the On the map list stands the same categories", Numbers.Count(lines));
            yield return Picture(p, "the On the map list by category");
            ScryPanel.FoldKeptCategoryForTest(KeptCategory.Resources);
            yield return Until(() => ScryPanel.KeptLineForTest(KeptLineLook.Kept, KeptCategory.Resources) < 0, 3);
            p.Check(ScryPanel.KeptLineForTest(KeptLineLook.Kept, KeptCategory.Resources) < 0 && ScryPanel.KeptLineForTest(KeptLineLook.Heading, KeptCategory.Resources) >= 0, "folded, a category stands its heading alone");
            ScryPanel.FoldKeptCategoryForTest(KeptCategory.Resources);
            ScryPanel.TakeCategoryOffForTest(KeptCategory.Resources);
            p.Check(!book.Keeps(copper) && !book.Keeps(tin) && book.Keeps(placeKey), "a heading's × takes its whole category off, no other");
            CrowdedAsItWas();
        }

        /// <summary>
        /// More kinds kept than the On the map list stands: it stands as many as fit, with no line
        /// asking any taken off, the wheel moving it a step and stopping at its end, and with few
        /// left it comes back to its top. Pictured scrolled.
        /// </summary>
        private static IEnumerator KeptListScrollsTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var places = X.Catalog.Where(e => e.Kind == Kind.Location).Select(e => EntryKeys.For(Kind.Location, e.Name)).Distinct().Take(80).ToList();
            if (book == null || Minimap.instance == null || Game.m_noMap || places.Count < 80)
            {
                p.Skip("there is no character, no map, or too few places in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            MapSpots.SpotsInsteadForTest(k => new List<(int X, int Z)>());
            foreach (var place in places) book.Keep(place, true);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var lists = ScryPanel.Drawn(PanelPart.KeptList);
            ScryPanel.OpenKeptListForTest(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.KeptList) > lists + 1, 3);
            var (lines, shown) = (ScryPanel.KeptLinesForTest, ScryPanel.KeptShownForTest);
            p.Check(lines >= 80 && shown > 0 && shown < lines, "more kept than fit, the list stands as many as fit", $"{Numbers.Count(shown)} of {Numbers.Count(lines)}");
            p.Check(ScryPanel.KeptFirstForTest == 0, "it opens at its top");
            ScryPanel.WheelKeptForTest(3f);
            yield return null;
            p.Check(ScryPanel.KeptFirstForTest == RowScroll.WheelRows, "the wheel moves it down", Numbers.Count(ScryPanel.KeptFirstForTest));
            yield return Picture(p, "the On the map list scrolled");
            ScryPanel.WheelKeptForTest(-3f);
            ScryPanel.WheelKeptForTest(-3f);
            yield return null;
            p.Check(ScryPanel.KeptFirstForTest == 0, "turned back it stops at its top", Numbers.Count(ScryPanel.KeptFirstForTest));
            for (var turn = 0; turn < lines; turn++) ScryPanel.WheelKeptForTest(3f);
            yield return null;
            p.Check(ScryPanel.KeptFirstForTest == lines - shown, "turned on it stops with its last line standing", $"{Numbers.Count(ScryPanel.KeptFirstForTest)} of {Numbers.Count(lines - shown)}");
            yield return Picture(p, "the On the map list at its end");
            foreach (var place in places.Skip(1)) book.Keep(place, false);
            yield return Until(() => ScryPanel.KeptLinesForTest <= ScryPanel.KeptShownForTest, 3);
            p.Check(ScryPanel.KeptFirstForTest == 0, "with few left it comes back to its top", Numbers.Count(ScryPanel.KeptFirstForTest));
            KeptOnMapAsItWas();
        }
    }
}
