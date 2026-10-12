using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KeptPlacesTests
    {
        // What every location and dungeon room was read to hold is kept from one session to the
        // next, so a world's places are known as soon as its catalog is, without loading their
        // bundles again. The file holds what each place's bundle holds, as read, with the stamp of
        // the build of Scry that read it and what it was read from; a file Scry cannot read, or a
        // place in it cut short, costs only a reading of what is missing.

        private static PlaceRecord Crypt()
        {
            var contents = new PlaceContents
            {
                GameNameKey = "$location_crypt", TraderKey = "", BossPrefab = "Bonemass",
                Vegvisirs = { "Bonemass", "Vendor_BlackForest" },
                EnemyMinLevel = 2, EnemyMaxLevel = 3, EnemyLevelUpChance = 12.5f, LevelOverrideExceptions = true,
                NoBuild = true, NoBuildRadius = 24.75f,
                PartsRolled = true, StarsRolled = false, GroupsRolled = true,
                Weathers = { "Crypt", "SunkenCrypt" },
            };
            contents.Parts.Add(new PlacePart { Prefab = "TreasureChest_forestcrypt", Count = 3, Chance = 1f / 3f });
            contents.Parts.Add(new PlacePart { Prefab = "Pickable_SurtlingCoreStand", Count = 1, Chance = 0.1f });
            contents.Creatures.Add(new PlacePart { Prefab = "Skeleton", Count = 4, Chance = 0.25f });
            contents.OwnMusic.Add(new PlaceMusic { Name = "crypt", When = MusicWhen.Inside, Chance = 0.6f });
            contents.OwnMusic.Add(new PlaceMusic { Name = "MusicLocation_Crypt", When = MusicWhen.Near, Chance = 1f });
            var stone = new PlaceRunestone { Name = "$piece_lore" };
            stone.Texts.Add(new RuneText { Topic = "$lore_topic", Text = "$lore_text" });
            stone.Texts.Add(new RuneText { Topic = "", Text = "Line one\nline two" });
            contents.Stones.Add(stone);
            contents.Dungeon = new DungeonPlan
            {
                Algorithm = "Dungeon", Themes = 0x40001, MinRooms = 20, MaxRooms = 40, MinRequiredRooms = 1,
                RequiredRooms = { "crypt2_Room_skull" }, Weighted = true, DoorChance = 0.4f,
                Doors = { ("door", 0.5f, "dungeon_forestcrypt_door"), ("", 0f, "") },
                GridSize = 4, TileWidth = 8f, SpawnChance = 0.3f, CampRadiusMin = 15f, CampRadiusMax = 30f, PerimeterSections = 12, PerimeterBuffer = 2f,
                ZoneSize = new Vec3(64f, 128f, 64f), CustomInterior = true,
                ZoneFromGenerator = new Vec3(-1.5f, -0.25f, 3f), GeneratorAt = new Vec3(0f, 5000f, -2f), GeneratorTurn = new Quat(0f, 0.70710677f, 0f, 0.70710677f),
            };
            return new PlaceRecord
            {
                Prefab = "Crypt2", Room = false, Stamp = "bundle 7f3a91c2 3043 638640000000000000",
                Contents = contents, Themes = { 1, 0x40000 }, Names = { "TreasureChest_forestcrypt", "Skeleton", "vfx_skeleton_hit" }, Known = 2,
                Altars = { new PlaceAltar { Boss = "Bonemass", Item = "WitheredBone", Count = 10, OnStands = false } },
            };
        }

        private static PlaceRecord Room()
        {
            var contents = new PlaceContents
            {
                Room = new RoomShape
                {
                    Name = "crypt2_Room_skull", Theme = 1, Size = new Vec3(12f, 8f, 12f), Entrance = false, EndCap = true, Divider = false,
                    Perimeter = true, FaceCenter = true, EndCapPrio = 2, MinPlaceOrder = 3, Weight = 0.5f,
                },
            };
            contents.Room.Doorways.Add(new Doorway { Type = "crypt", Entrance = true, AllowDoor = false, DoorOnlyIfOtherAllows = true, Position = new Vec3(0f, 0f, -6f), Rotation = Quat.Yaw(180f) });
            return new PlaceRecord { Prefab = "crypt2_Room_skull", Room = true, Stamp = "bundle 1701dddf 3043 1", Contents = contents, Themes = { 1 } };
        }

        private static KeptPlaces Of(params PlaceRecord[] records)
        {
            var kept = new KeptPlaces();
            foreach (var record in records) kept.Put(record);
            return kept;
        }

        [Fact]
        public void WhatWasReadOfAPlaceComesBackWholeTheNextSession()
        {
            var text = Of(Crypt(), Room()).Save();
            var back = KeptPlaces.Load(text);

            Assert.Equal(2, back.Records.Count);
            var crypt = back.Find("Crypt2", false);
            Assert.NotNull(crypt);
            Assert.Equal("bundle 7f3a91c2 3043 638640000000000000", crypt.Stamp);
            Assert.Equal("$location_crypt", crypt.Contents.GameNameKey);
            Assert.Equal("Bonemass", crypt.Contents.BossPrefab);
            Assert.Equal(new[] { "TreasureChest_forestcrypt", "Pickable_SurtlingCoreStand" }, crypt.Contents.Parts.Select(p => p.Prefab));
            Assert.Equal(3, crypt.Contents.Parts[0].Count);
            Assert.Equal("Skeleton", Assert.Single(crypt.Contents.Creatures).Prefab);
            Assert.Equal(new[] { "Bonemass", "Vendor_BlackForest" }, crypt.Contents.Vegvisirs);
            Assert.Equal((2, 3, 12.5f, true), (crypt.Contents.EnemyMinLevel, crypt.Contents.EnemyMaxLevel, crypt.Contents.EnemyLevelUpChance, crypt.Contents.LevelOverrideExceptions));
            Assert.Equal((true, 24.75f), (crypt.Contents.NoBuild, crypt.Contents.NoBuildRadius));
            Assert.Equal((true, false, true), (crypt.Contents.PartsRolled, crypt.Contents.StarsRolled, crypt.Contents.GroupsRolled));
            Assert.Equal(new[] { MusicWhen.Inside, MusicWhen.Near }, crypt.Contents.OwnMusic.Select(m => m.When));
            Assert.Equal(new[] { "Crypt", "SunkenCrypt" }, crypt.Contents.Weathers);
            Assert.Equal(new[] { "$lore_text", "Line one\nline two" }, Assert.Single(crypt.Contents.Stones).Texts.Select(t => t.Text));
            Assert.Equal(new[] { 1, 0x40000 }, crypt.Themes);
            Assert.Equal(new[] { "TreasureChest_forestcrypt", "Skeleton", "vfx_skeleton_hit" }, crypt.Names);
            Assert.Equal(2, crypt.Known);
            var altar = Assert.Single(crypt.Altars);
            Assert.Equal(("Bonemass", "WitheredBone", 10, false), (altar.Boss, altar.Item, altar.Count, altar.OnStands));

            var plan = crypt.Contents.Dungeon;
            Assert.Equal(("Dungeon", 0x40001, 20, 40, 1, true), (plan.Algorithm, plan.Themes, plan.MinRooms, plan.MaxRooms, plan.MinRequiredRooms, plan.Weighted));
            Assert.Equal(new[] { "crypt2_Room_skull" }, plan.RequiredRooms);
            Assert.Equal(new[] { ("door", 0.5f, "dungeon_forestcrypt_door"), ("", 0f, "") }, plan.Doors);
            Assert.Equal(5000f, plan.GeneratorAt.Y);
            Assert.True(plan.CustomInterior);

            var room = back.Find("crypt2_Room_skull", true);
            Assert.True(room.Room);
            Assert.Null(room.Contents.Dungeon);
            Assert.Equal((true, true, 2, 3, 0.5f), (room.Contents.Room.EndCap, room.Contents.Room.FaceCenter, room.Contents.Room.EndCapPrio, room.Contents.Room.MinPlaceOrder, room.Contents.Room.Weight));
            var doorway = Assert.Single(room.Contents.Room.Doorways);
            Assert.Equal(("crypt", true, false, true), (doorway.Type, doorway.Entrance, doorway.AllowDoor, doorway.DoorOnlyIfOtherAllows));
            Assert.Null(crypt.Contents.Room);

            // Written again, it is the same file.
            Assert.Equal(text, back.Save());
        }

        [Fact]
        public void NumbersComeBackExactlyAsRead()
        {
            var record = Room();
            record.Contents.Parts.Add(new PlacePart { Prefab = "a", Count = 1, Chance = 1f / 3f });
            record.Contents.Parts.Add(new PlacePart { Prefab = "b", Count = 1, Chance = 1e-7f });
            record.Contents.Room.Doorways[0].Position = new Vec3(-2.5f, 0.1f, 123456.78f);
            var back = KeptPlaces.Load(Of(record).Save()).Find("crypt2_Room_skull", true);

            Assert.Equal(1f / 3f, back.Contents.Parts[0].Chance);
            Assert.Equal(1e-7f, back.Contents.Parts[1].Chance);
            Assert.Equal(new Vec3(-2.5f, 0.1f, 123456.78f), back.Contents.Room.Doorways[0].Position);
            Assert.Equal(Quat.Yaw(180f), back.Contents.Room.Doorways[0].Rotation);
        }

        [Fact]
        public void TextsComeBackAsWrittenWhateverTheyHold()
        {
            var odd = new[] { "tab\there", "line\nbreak", "return\r", "back\\slash", "\\t not a tab", "Mörkhalla", "", "end\\" };
            var record = Crypt();
            record.Contents.Vegvisirs = odd.ToList();
            record.Prefab = "Odd\tname\\";
            var back = KeptPlaces.Load(Of(record).Save()).Find("Odd\tname\\", false);
            Assert.NotNull(back);
            Assert.Equal(odd, back.Contents.Vegvisirs);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("kills\tBoar\t3\n")]
        [InlineData("Scry places 999\nplace\tx\t0\ts\nend\n")]
        public void AFileOfAnotherKindOrNoneGivesNothing(string text)
        {
            Assert.Empty(KeptPlaces.Load(text).Records);
        }

        [Fact]
        public void APlaceCutOffPartWayIsLeftOutAndTheRestKept()
        {
            var text = Of(Room(), Crypt()).Save();
            var cut = text.Substring(0, text.IndexOf("altar\t", System.StringComparison.Ordinal));
            var back = KeptPlaces.Load(cut);
            Assert.NotNull(back.Find("crypt2_Room_skull", true));
            Assert.Null(back.Find("Crypt2", false));
        }

        [Fact]
        public void APlaceWithALineThatCannotBeReadIsLeftOutAndTheRestKept()
        {
            var text = Of(Crypt(), Room()).Save();
            var broken = text.Replace("part\tTreasureChest_forestcrypt\t3\t", "part\tTreasureChest_forestcrypt\tthree\t");
            Assert.NotEqual(text, broken);
            var back = KeptPlaces.Load(broken);
            Assert.Null(back.Find("Crypt2", false));
            Assert.NotNull(back.Find("crypt2_Room_skull", true));

            var unknown = KeptPlaces.Load(text.Replace("weather\tCrypt\n", "weather\tCrypt\nsomething new\t1\n"));
            Assert.Null(unknown.Find("Crypt2", false));
        }

        [Fact]
        public void APlaceWithAFieldWrittenWronglyIsLeftOut()
        {
            var text = Of(Crypt(), Room()).Save();
            var broken = text.Replace("vegvisir\tBonemass\n", "vegvisir\tBone\\qmass\n");
            Assert.NotEqual(text, broken);
            var back = KeptPlaces.Load(broken);
            Assert.Null(back.Find("Crypt2", false));
            Assert.NotNull(back.Find("crypt2_Room_skull", true));
        }

        [Fact]
        public void ALineWithMoreOrFewerFieldsThanItsKindHasLeavesThePlaceOut()
        {
            var text = Of(Crypt(), Room()).Save();
            var dungeon = text.Split('\n').Single(l => l.StartsWith("dungeon\t", System.StringComparison.Ordinal));
            foreach (var wrong in new[] { dungeon + "\t1", dungeon.Substring(0, dungeon.LastIndexOf('\t')) })
            {
                var back = KeptPlaces.Load(text.Replace(dungeon, wrong));
                Assert.Null(back.Find("Crypt2", false));
                Assert.NotNull(back.Find("crypt2_Room_skull", true));
            }
        }

        [Fact]
        public void APlaceCutShortIsLeftOutEvenWhereTheNextCannotBeRead()
        {
            var text = Of(Crypt(), Room()).Save();
            var broken = text.Replace("end\nplace\tcrypt2_Room_skull\t1\t", "place\tcrypt2_Room_skull\t2\t");
            Assert.NotEqual(text, broken);
            var back = KeptPlaces.Load(broken);
            Assert.Empty(back.Records);
        }

        [Fact]
        public void ALocationAndARoomOfOneNameAreKeptApart()
        {
            var location = Crypt();
            location.Prefab = "same";
            var room = Room();
            room.Prefab = "same";
            var back = KeptPlaces.Load(Of(location, room).Save());
            Assert.Equal(2, back.Records.Count);
            Assert.False(back.Find("same", false).Room);
            Assert.True(back.Find("same", true).Room);
        }

        [Fact]
        public void APlaceReadAgainTakesTheOldOnesPlace()
        {
            var kept = Of(Crypt());
            var again = Crypt();
            again.Stamp = "newer";
            kept.Put(again);
            Assert.Equal("newer", Assert.Single(kept.Records).Stamp);
        }

        [Fact]
        public void WhatIsWrittenIsThePlacesAsTheyWereWhenAskedForThoughMoreAreReadMeanwhile()
        {
            // Written on a worker thread while the places are read again.
            var kept = Of(Crypt());
            var written = kept.Copy();
            var again = Crypt();
            again.Stamp = "newer";
            kept.Put(again);
            kept.Put(Room());

            var back = KeptPlaces.Load(written.Save());

            Assert.Equal("bundle 7f3a91c2 3043 638640000000000000", Assert.Single(back.Records).Stamp);
            Assert.Equal(2, kept.Records.Count);
        }
    }
}
