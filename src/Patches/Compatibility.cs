using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Checks the parts of the game Scry relies on, once, when it first reads the catalog in a
    /// world, and says in the log what it found: the members it reaches by name, the methods it
    /// patches, the game code whose rules its previews follow (by the shape of that code as it
    /// was when this version was made, see <see cref="IlShape"/>), the animation events the game
    /// answers, the layers falling copies land on, the effect scripts copies keep, and the status
    /// effects and prefab it looks up by name. A game update that moves any of these is told as
    /// the feature it affects, rather than found out as a preview gone quietly wrong.
    /// </summary>
    internal static class Compatibility
    {
        /// <summary>What each patch is for, by the class that makes it, as the plugin, which sees every layer's patches, gives it.</summary>
        public static IReadOnlyDictionary<Type, Feature> PatchFeatures { get; set; } = new Dictionary<Type, Feature>();

        /// <summary>What a patch is for, by the class that makes it; Scry as a whole for one not listed.</summary>
        public static Feature FeatureOfPatch(Type patch) => PatchFeatures.TryGetValue(patch, out var feature) ? feature : Feature.World;

        /// <summary>
        /// The game methods whose rules Scry follows, with the shape of their code in
        /// Valheim 1.0.16, read from the game's own assembly.
        /// </summary>
        private static readonly (string Type, string Method, int Params, uint Shape, Feature Feature)[] Watched =
        {
            ("VisEquipment", "AttachItem", 6, 0xD7DAC228, Feature.GearOnCreaturesAndPerson),
            ("Humanoid", "GiveDefaultItems", 0, 0x308C20BE, Feature.WhatCreaturesCarry),
            ("Humanoid", "EquipItem", 2, 0xB6B9953E, Feature.WhichGearTakesPlaceOfWhich),
            ("Humanoid", "SetupVisEquipment", 2, 0x17BDBBAE, Feature.ArmourOnRagdolls),
            ("LevelEffects", "SetupLevelVisualization", 1, 0xD5E59AD9, Feature.StarLevelLooks),
            ("WearNTear", "UpdateVisual", 1, 0x390E7B55, Feature.WornAndBrokenLooks),
            ("Fireplace", "UpdateState", 0, 0xA932F988, Feature.FireLooks),
            ("TeleportWorld", "UpdatePortal", 0, 0xB1368747, Feature.PortalLooks),
            ("EffectFade", "SetActive", 1, 0xDC4AA873, Feature.EffectsFadingInAndOut),
            ("ItemStyle", "Setup", 1, 0xB6778751, Feature.ItemStyles),
            ("Plant", "Grow", 0, 0x387B2C65, Feature.PlantsGrown),
            ("Pickable", "SetPicked", 1, 0xD43BD169, Feature.PickedLooks),
            ("Character", "OnDeath", 0, 0x63009BD2, Feature.RagdollDeaths),
            ("Ragdoll", "Setup", 7, 0xD56D1722, Feature.RagdollDeaths),
            ("Destructible", "CreateFragments", 2, 0x2A65B522, Feature.PiecesBreakingApart),
            ("WearNTear", "RPC_CreateFragments", 1, 0x2724FEDC, Feature.PiecesBreakingApart),
            ("TreeBase", "SpawnLog", 1, 0xBF8E65F4, Feature.TreesFalling),
            ("TreeLog", "Destroy", 2, 0xD6731DC4, Feature.LogsSplittingIntoHalves),
            ("Destructible", "Destroy", 1, 0xD9E62863, Feature.WhatThingsLeaveWhenBroken),
            ("TreeBase", "Shake", 0, 0x60417B85, Feature.TreesShakingWhenStruck),
            ("TreeBase", "ShakeAnimation", 0, 0xBA9F74EE, Feature.TreesShakingWhenStruck),
            ("EffectList", "Create", 6, 0xA8985D0C, Feature.WhereEffectsAppearOnModel),
            ("AnimationEffect", "Effect", 1, 0xDF43C7EF, Feature.SoundsAndEffectsAnimationsName),
            ("AnimationEffect", "Attach", 1, 0x1974D1DE, Feature.PropsAnimationsHold),
            ("FootStep", "UpdateFootstepCurveTrigger", 1, 0xBB6FCBEA, Feature.Footsteps),
            ("ZSFX", "Play", 0, 0xF60FF796, Feature.PickingSoundVariants),
            ("MusicLocation", "Awake", 0, 0x80F33570, Feature.LocationMusic),
            ("MusicLocation", "Update", 0, 0x68092A70, Feature.LocationMusic),
            ("MusicVolume", "OnEnter", 0, 0xD548F046, Feature.LocationMusic),
            ("EnvZone", "OnTriggerStay", 1, 0xB815D51E, Feature.LocationMusic),
            ("Character", "AddFireDamage", 2, 0xA441CEBC, Feature.StatusEffectsDamageCauses),
            ("Character", "AddFrostDamage", 2, 0x77AE9523, Feature.StatusEffectsDamageCauses),
            ("Character", "AddLightningDamage", 2, 0x068B93DE, Feature.StatusEffectsDamageCauses),
            ("Character", "AddPoisonDamage", 2, 0x77AE9523, Feature.StatusEffectsDamageCauses),
            ("Character", "AddSpiritDamage", 2, 0xA441CEBC, Feature.StatusEffectsDamageCauses),
            ("Door", "SetState", 1, 0xFD8700BC, Feature.DoorLooks),
            ("Container", "UpdateUseVisual", 0, 0x53F25066, Feature.ChestLooks),
            ("Smelter", "UpdateState", 0, 0x74799120, Feature.SmelterLooks),
            ("Smelter", "SetAnimation", 1, 0x8728BDAE, Feature.SmelterLooks),
            ("Fermenter", "SlowUpdate", 0, 0xBF99D857, Feature.FermenterLooks),
            ("SapCollector", "UpdateEffects", 0, 0xA276633B, Feature.SapCollectorLooks),
            ("CraftingStation", "CustomUpdate", 2, 0x0F1DE708, Feature.CraftingStationsInUse),
            ("CraftingStation", "CheckFire", 0, 0xAE55015F, Feature.CraftingStationsFire),
            ("Windmill", "Update", 0, 0x5B823CE2, Feature.WindmillsTurning),
            ("Tameable", "SetSaddle", 1, 0x7BC32B79, Feature.Saddles),
            ("AnimationObjectToggle", "SetGameObject", 2, 0xFB641210, Feature.PartsAnimationsShowAndHide),
            ("Character", "ForceJump", 2, 0xBF80A81B, Feature.JumpAnimations),
            ("Character", "TakeOff", 0, 0xA4E55A1C, Feature.FlyersTakingOff),
            ("Character", "OnDeathAnim", 0, 0xECCFE468, Feature.DeathAnimations),
            ("Humanoid", "SetupAnimationState", 0, 0xA69F123F, Feature.WeaponStances),
            ("Humanoid", "UpdateBlock", 1, 0xE93DD6F9, Feature.BlockAnimations),
            ("Humanoid", "UseItem", 3, 0x0FE529C5, Feature.EatingAnimations),
            ("BaseAI", "SetAlerted", 1, 0xA6DC542C, Feature.AlertAnimations),
            ("MonsterAI", "Wakeup", 0, 0xE05A55C9, Feature.WakingAnimations),
            ("MonsterAI", "UpdateConsumeItem", 2, 0x7D31D2DF, Feature.EatingAnimations),
            ("Attack", "Start", 9, 0xF77DB3EE, Feature.AttackSwings),
            ("Attack", "GetProjectileSpawnPoint", 2, 0xA167E181, Feature.ThrownAndShotProjectiles),
            ("Attack", "FireProjectileBurst", 0, 0x9C942866, Feature.ThrownAndShotProjectiles),
            ("Attack", "OnAttackTrigger", 0, 0x8575D354, Feature.WhatAttackPlaysAsItStrikes),
            ("Attack", "DoMeleeAttack", 0, 0x02E4B054, Feature.WhereSwingsHit),
            ("Attack", "GetMeleeAttackDir", 2, 0x2F48BDB7, Feature.WhereSwingsHit),
            ("Attack", "DoAreaAttack", 0, 0x8E76EA3A, Feature.WhereAreaAttacksHit),
            ("Attack", "ProjectileAttackTriggered", 0, 0x6F38B2FF, Feature.WhatThrowsPlayAsTheyLetGo),
            ("Attack", "GetAttackOrigin", 0, 0x1F38E4AA, Feature.WhereAttacksComeFrom),
            ("Projectile", "Setup", 6, 0xAC11A4B1, Feature.ThrowsOwnHits),
            ("Character", "Awake", 0, 0x26223A54, Feature.BodyGameAnimates),
            ("Humanoid", "InAttack", 0, 0x9FA3519B, Feature.WhichClipsAreAttack),
            ("Character", "RPC_Stagger", 2, 0xF2A24690, Feature.StaggerAnimations),
            ("Character", "AddStaggerDamage", 3, 0xCEA5F8B3, Feature.HitsHeardWithStaggers),
            ("Character", "UpdateContinousEffects", 0, 0xABFA49E2, Feature.LastingEffects),
            ("Character", "UpdateSwimming", 1, 0x0FCF0647, Feature.SwimmingAnimations),
            ("FootStep", "FindBestStepEffect", 2, 0x2A458304, Feature.WhichStepCreatureMakes),
            ("MonsterAI", "Sleep", 0, 0x76D7B742, Feature.SleepingAnimations),
            ("MonsterAI", "UpdateSleep", 1, 0x0849E9C0, Feature.WhatIsHeardAfterWaking),
            ("BaseAI", "DoIdleSound", 0, 0xEAD56C82, Feature.IdleSounds),
            ("RandomIdle", "OnStateEnter", 3, 0x387CD94B, Feature.IdleClipsPickedAtRandom),
            ("RandomIdle", "OnStateUpdate", 3, 0x1E89789A, Feature.IdleClipsPickedAtRandom),
            ("RandomIdle", "GetRandomIdle", 1, 0x7A49F403, Feature.IdleClipsPickedAtRandom),
            ("CreatureSpawner", "Spawn", 0, 0xF64EBBF9, Feature.SpawnRoarsAndLevels),
            ("SpawnAbility", "Spawn", 0, 0xD3A37693, Feature.SpawnRoars),
            ("Player", "SetupAwake", 0, 0x38272BB3, Feature.PersonStandingFromStart),
            ("ZoneSystem", "SpawnLocation", 7, 0xDC30DC06, Feature.LocationsAsBuilt),
            ("RandomSpawn", "Randomize", 3, 0x36081587, Feature.ChancePartsOfLocationsAndRooms),
            ("RandomSpawn", "SetSpawned", 1, 0xEEAF5E83, Feature.ChancePartsOfLocationsAndRooms),
            ("RandomObject", "Randomize", 3, 0x7EF8F11A, Feature.ChancePartsOfLocationsAndRooms),
            ("RandomObject", "GetWeightedObject", 0, 0xDB9205DF, Feature.ChancePartsOfLocationsAndRooms),
            ("RandomObject", "SetSpawned", 1, 0x8FFF8015, Feature.ChancePartsOfLocationsAndRooms),
            ("DungeonGenerator", "Generate", 2, 0xCB559F96, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "SetupAvailableRooms", 0, 0x4399BF25, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GenerateDungeon", 1, 0x394AD1FF, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceStartRoom", 1, 0x856A5ABD, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "FindStartRoom", 0, 0x50AB78E6, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceRooms", 1, 0x70972F3C, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "CheckRequiredRooms", 0, 0xFD8A8347, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceOneRoom", 1, 0xFDC7F575, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GetOpenConnection", 0, 0xACEBB30B, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GetRandomRoom", 1, 0x86FD6D09, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GetRandomWeightedRoom", 1, 0x6B47C5E0, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GetWeightedRoom", 1, 0x67B6F506, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "CalculateRoomPosRot", 5, 0x6BE032B1, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceRoom", 3, 0x748F34EF, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceRoom", 5, 0x40467FF7, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "AddOpenConnections", 2, 0xBDC09E0C, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "TestCollision", 3, 0x6381F9DE, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "IsInsideDungeon", 3, 0xD6C8364F, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceEndCaps", 1, 0x24E2B1F5, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "FindDividers", 1, 0xAADF07E0, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "FindEndCaps", 2, 0x4010457D, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceDoors", 1, 0x3E78CE67, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "FindDoorType", 1, 0x70C27DFB, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GenerateCampGrid", 1, 0xFBB116CD, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GenerateCampRadial", 1, 0x097BF9B1, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "GetCampRoomRotation", 2, 0x586BF423, Feature.ExampleDungeonAndCampLayouts),
            ("DungeonGenerator", "PlaceWall", 3, 0x6403B4E0, Feature.ExampleDungeonAndCampLayouts),
            ("Room", "GetConnection", 1, 0x1059F108, Feature.ExampleDungeonAndCampLayouts),
            ("Room", "GetEntrance", 0, 0x328B4909, Feature.ExampleDungeonAndCampLayouts),
            ("Room", "HaveConnection", 1, 0xB3A7BAC1, Feature.ExampleDungeonAndCampLayouts),
            ("RoomConnection", "TestContact", 1, 0xFC843271, Feature.ExampleDungeonAndCampLayouts),
            ("ShuffleClass", "Shuffle", 2, 0x9FD9815A, Feature.ExampleDungeonAndCampLayouts),
            ("ZoneSystem", "GetRandomPointInZone", 2, 0x9A9990B2, Feature.ExampleDungeonAndCampLayouts),
            ("ZoneSystem", "PlaceLocations", 7, 0x6BD1B2B1, Feature.ExampleDungeonAndCampLayouts),

            // What the details tell the game's rules from: if one changes, its words may be off.
            ("CharacterDrop", "GenerateDropList", 0, 0x1D58F486, Feature.CreatureDrops),
            ("CharacterDrop", "OnDeath", 0, 0x15143844, Feature.DropsSeenInPlay),
            ("Ragdoll", "Setup", 7, 0xD56D1722, Feature.DropsSeenInPlay),
            ("Ragdoll", "SpawnLoot", 1, 0x1CB6DE81, Feature.DropsSeenInPlay),
            ("DropTable", "AddItemToList", 2, 0xAECEE75C, Feature.DropTableAmounts),
            ("DropTable", "GetDropList", 1, 0xBF6963CC, Feature.DropTableRolls),
            ("BaseAI", "CanSeeTarget", 7, 0xDE5EBD88, Feature.WhatCreaturesSee),
            ("Character", "SetupMaxHealth", 0, 0x6F57CA0E, Feature.HealthWithStars),
            ("Character", "GetDamageModifiers", 1, 0x14A24DBA, Feature.WeakSpots),
            ("MonsterAI", "UpdateAI", 1, 0xF625E201, Feature.WhenCreaturesTurnOnYou),
            ("MonsterAI", "UpdateTarget", 4, 0x71A722F8, Feature.HowFarCreaturesChase),
            ("WearNTear", "GetMaterialProperties", 4, 0x12182085, Feature.Support),
            ("WearNTear", "UpdateSupport", 0, 0x291BD05E, Feature.SupportLost),
            ("WearNTear", "UpdateWear", 1, 0xE472FFDA, Feature.RainAshAndSnowWear),
            ("SpawnArea", "UpdateSpawn", 0, 0x97E19B30, Feature.SpawnersPace),
            ("SpawnArea", "SpawnOne", 0, 0x0897D075, Feature.SpawnersCapsAndStars),
            ("SpawnArea", "SelectWeightedPrefab", 0, 0xDA338F1E, Feature.SpawnersShares),
            ("SpawnArea", "GetInstances", 2, 0xE34A2F7A, Feature.SpawnersCaps),
            ("SpawnArea", "FindSpawnPoint", 2, 0xCEBBCAD3, Feature.WhereSpawnersPutWhatTheySpawn),
            ("RandEventSystem", "UpdateRandomEvent", 1, 0xFBDE21DE, Feature.HowRaidsAreRolled),
            ("RandEventSystem", "StartRandomEvent", 0, 0xCBFC4B16, Feature.HowRaidsAreRolled),
            ("RandEventSystem", "GetValidEventPoints", 2, 0xF6E06F29, Feature.WhomRaidsComeFor),
            ("RandEventSystem", "CheckBase", 2, 0x4E771406, Feature.WhomRaidsComeFor),
            ("RandEventSystem", "HaveGlobalKeys", 2, 0xBAF80048, Feature.WhenRaidsAreOnTable),
            ("RandomEvent", "Update", 4, 0xBC0458E1, Feature.HowLongRaidsLast),
            ("SpawnSystem", "UpdateSpawnList", 4, 0x85C54700, Feature.WhatRaidsBring),
            ("EffectArea", "GetBaseValue", 2, 0x1259361B, Feature.WhomRaidsComeFor),
            ("Attack", "GetLevelDamageFactor", 0, 0x56C23F2E, Feature.DamageWithStars),
            ("Player", "HaveRequirementItems", 4, 0xB9AF033A, Feature.UpgradeKitsApart),
            ("InventoryGui", "SetupRequirementList", 4, 0xB8443C09, Feature.UpgradeKitsApart),
            ("InventoryGui", "UpdateRecipeList", 1, 0xC89F3E86, Feature.WhatUpgradeStationDoes),
            ("InventoryGui", "CanRepair", 1, 0xD57710C7, Feature.WhereItemsAreRepaired),
            ("Recipe", "GetRequiredStationLevel", 1, 0x7AD112DA, Feature.StationLevelsForUpgrades),
            ("ItemDrop+ItemData", "GetTooltip", 6, 0xE717E1A4, Feature.ItemStats),
            ("ItemDrop+ItemData", "AddBlockTooltip", 3, 0xDF722134, Feature.BlockAndParry),
            ("Player", "GetBodyArmor", 0, 0x03C09085, Feature.WhichArmourCounts),
            ("HitData", "ApplyResistance", 2, 0x8BB9A7D5, Feature.TrueDamage),
            ("SE_Rested", "CalculateComfortLevel", 2, 0x71FEE0E8, Feature.ComfortGroups),
            ("SE_Stats", "GetTooltipString", 0, 0x8126FDA1, Feature.StatusEffectStats),
            ("Player", "CanConsumeItem", 2, 0x27D77A45, Feature.ExclusiveEffects),
            ("Incinerator+IncineratorConversion", "AttemptCraft", 2, 0x7D4E45C5, Feature.WhatObliteratorMakes),
            ("RandEventSystem", "HaveGlobalKeys", 2, 0xBAF80048, Feature.WhatRaidsWaitFor),
            ("Trader", "GetAvailableItems", 0, 0x19BA382C, Feature.WhatTradersSellAndWhen),
            ("Fermenter", "UpdateCover", 2, 0x861BA786, Feature.WhatFermenterNeeds),
            ("Beehive", "UpdateBees", 0, 0x14D30E4F, Feature.BeehiveNeeds),
            ("Beehive", "HaveFreeSpace", 0, 0x67355E85, Feature.BeehiveNeeds),
            ("Beehive", "CheckBiome", 0, 0x0719B463, Feature.BeehiveNeeds),
            ("SapCollector", "UpdateTick", 0, 0x82F639F5, Feature.SapCollectorNeeds),
            ("Player", "ApplyArmorDamageMods", 1, 0x9032CDDF, Feature.WhatArmourResists),
            ("Humanoid", "BlockAttack", 2, 0xAA978FC7, Feature.ShieldResists),
            ("Player", "AppendEquipmentModifierTooltips", 2, 0xD3F4F5B4, Feature.WhatGearChangesWhileWorn),
            ("Procreation", "Procreate", 0, 0x9DBA014B, Feature.HowTameCreaturesBreed),
            ("Growup", "GrowUpdate", 0, 0x3CA56DD5, Feature.WhatYoungCreaturesGrowInto),
            ("Growup", "GetPrefab", 0, 0x7CD1A961, Feature.WhatYoungCreaturesGrowInto),
            ("EggGrow", "CanGrow", 0, 0x28D2DC79, Feature.WhereEggsHatch),
            ("Fish", "TestBate", 1, 0x92BFD221, Feature.WhatFishBiteOn),
            ("Door", "Interact", 3, 0x90968825, Feature.WhatOpensDoor),
            ("Player", "UpdatePlacementGhost", 1, 0xE17BEED9, Feature.WherePiecesMayBePlaced),
            ("Bed", "Interact", 3, 0x0527CD88, Feature.WhatBedNeeds),
            ("Bed", "CheckExposure", 1, 0xC947A6D8, Feature.WhatBedNeeds),
            ("Player", "UpdateEnvStatusEffects", 1, 0x771FB54F, Feature.WhatWarmAndCozyAreasDo),
            ("BaseAI", "AvoidFire", 3, 0x3A65B21C, Feature.WhatFireAreasDo),
            ("SpawnSystem", "IsSpawnPointGood", 2, 0x24B045D5, Feature.WhatBaseKeepsOut),
            ("CreatureSpawner", "UpdateSpawner", 0, 0x88F6BBD3, Feature.WhatBaseKeepsOut),
            ("EnvMan", "SelectWeightedEnvironment", 1, 0x297CE296, Feature.HowLikelyBiomeWeathersAre),
            ("Turret", "UpdateTarget", 1, 0x5DFD3E27, Feature.WhatBallistaShoots),
            ("Catapult", "CanItemBeLoaded", 1, 0xF252ADEF, Feature.WhatCatapultLoads),
            ("Vagon", "UpdateMass", 0, 0xF4168EF2, Feature.WhatCartWeighs),
            ("Ship", "TakeAshlandsDamage", 1, 0xE4A305CB, Feature.ShipsInAshlands),
            ("Container", "AddDefaultItems", 0, 0xD14D7482, Feature.WhatChestsHold),
            ("MineRock5", "DamageArea", 2, 0x80C15677, Feature.RockHealthPerPiece),
            ("Plant", "GetGrowTime", 0, 0x424F2EF8, Feature.HowPlantsGrow),
            ("Plant", "UpdateHealth", 1, 0xC5AAD837, Feature.HowPlantsGrow),
            ("Pickable", "UpdateRespawn", 0, 0x31AD3B33, Feature.WhenWhatIsPickedGrowsBack),
            ("Tameable", "TamingUpdate", 0, 0xB81F3F62, Feature.TamingAndFeeding),
            ("Tameable", "IsHungry", 0, 0x38421197, Feature.TamingAndFeeding),
            ("Character", "GetMaxHealthBase", 0, 0x1BFF8AE9, Feature.WorldLevel),
            ("SEMan", "HaveStatusEffectCategory", 1, 0xE7A9814B, Feature.ExclusiveEffects),
            ("ZoneSystem", "GenerateLocationsTimeSliced", 0, 0x3D0E575E, Feature.WhichLocationsArePlacedFirst),
            ("ZoneSystem", "GenerateLocationsTimeSliced", 3, 0xB5CD6171, Feature.WhereLocationsArePlaced),
            ("Location", "GetMaxRadius", 0, 0xB73300D6, Feature.NoBuildingNearLocations),
            ("Location", "IsInside", 3, 0xCC89D439, Feature.NoBuildingNearLocations),
            ("WorldGenerator", "GetForestFactor", 1, 0x33E92B46, Feature.WoodsLocationsKeepTo),
            ("WorldGenerator", "InForest", 1, 0x26492B75, Feature.WoodsLocationsKeepTo),
            ("WorldGenerator", "GetBiomeHeight", 6, 0x529952E1, Feature.LavaAndGrowthLocationsKeepTo),
            ("WorldGenerator", "GetAshlandsHeight", 4, 0x203BF12F, Feature.LavaAndGrowthLocationsKeepTo),
            ("WorldGenerator", "GetMistlandsHeight", 3, 0xA0B9E42A, Feature.LavaAndGrowthLocationsKeepTo),
            ("ZoneSystem", "IsLavaPreHeightmap", 2, 0xA4791600, Feature.LavaAndGrowthLocationsKeepTo),
            ("AnimalAI", "UpdateAI", 1, 0x271C0C2A, Feature.AnimalsFleeing),
            ("AudioMan", "RequestPlaySound", 1, 0xFF2FA2EA, Feature.PreviewsHeardAsIfBesideYou),
            ("BaseAI", "CanSenseTarget", 10, 0x0BBA2297, Feature.PassiveEnemies),
            ("BaseAI", "FindClosestCreature", 12, 0x067AD711, Feature.WhatBallistaShoots),
            ("Bed", "CheckFire", 1, 0xA250F4AB, Feature.WhatBedNeeds),
            ("Character", "IsFlying", 0, 0x962D60EC, Feature.FlyerEffectsOnStage),
            ("Character", "OnNearFire", 1, 0x2776BA6F, Feature.FiresWarmth),
            ("ClutterSystem", "GenerateVegPatch", 2, 0xFE9EE3D6, Feature.GrassOnStageGround),
            ("EnemyHud", "TestShow", 2, 0x8219CA93, Feature.BossFightsAmongRaids),
            ("EnvMan", "CalculateCold", 0, 0xFD3B55B3, Feature.WeathersInWords),
            ("EnvMan", "IsWet", 0, 0x9D105759, Feature.WeathersInWords),
            ("Game", "ScaleDrops", 2, 0x736A0A5A, Feature.NotesOnWorldSettings),
            ("CreatureSpawner+Group", "SpawnWeighted", 0, 0x596BE757, Feature.PlacesCreaturesOnStage),
            ("Heightmap", "ApplySettings", 0, 0x75EF4F81, Feature.StageGround),
            ("Heightmap", "PaintCleared", 6, 0x0A9A7BBE, Feature.PlacesPathsOnStageGround),
            ("HitData", "ApplyModifier", 6, 0xDC32A000, Feature.Resistances),
            ("ImpactEffect", "OnCollisionEnter", 1, 0x742C27A8, Feature.FelledLogsStrikingGround),
            ("Incinerator", "Incinerate", 1, 0x76DE92A7, Feature.WhatObliteratorMakes),
            ("InstanceRenderer", "AddInstance", 3, 0x960E6F20, Feature.GrassOnStageGround),
            ("ItemDrop+ItemData", "GetTooltip", 6, 0xE717E1A4, Feature.WhatGearChangesWhileWorn),
            ("ItemDrop+ItemData", "HaveSecondaryAttack", 0, 0x924F9983, Feature.SecondAttacks),
            ("Menu", "Update", 0, 0x0A30DC16, Feature.KeysKeptFromGame),
            ("OfferingBowl", "Interact", 3, 0xE0D1BB7B, Feature.AltarsItemStands),
            ("Player", "HaveRequirements", 4, 0x23C01547, Feature.UpgradeKits),
            ("Player", "TakeInput", 0, 0x3DECB72C, Feature.KeysKeptFromGame),
            ("RandEventSystem", "GetEvent", 1, 0x22158546, Feature.BossFightsAmongRaids),
            ("RandEventSystem", "GetForcedEvent", 0, 0x336C037F, Feature.BossFightsAmongRaids),
            ("RandEventSystem", "InValidBiome", 2, 0xFA79CFCA, Feature.WhereRaidsCome),
            ("Room", "GetConnections", 0, 0xB3701829, Feature.ExampleDungeonAndCampLayouts),
            ("RuneStone", "GetRandomText", 0, 0x309CE075, Feature.RunestoneTexts),
            ("RuneStone", "Interact", 3, 0xB26864BA, Feature.RunestoneTexts),
            ("SpawnArea", "Awake", 0, 0xC832E900, Feature.Spawners),
            ("SpawnSystem", "GetLevelUpChance", 2, 0x48618D51, Feature.RaidsStarsOnStage),
            ("SpawnSystem", "Spawn", 3, 0x9994FB4E, Feature.RaidsRolledOnStage),
            ("Thunder", "DoFlash", 0, 0x7FA8BAAB, Feature.WorldLightsKeptOffStage),
            ("Turret", "UseItem", 2, 0x5FC22918, Feature.WhatBallistaShoots),
            ("WorldGenerator", "GetTerrainDelta", 4, 0x55D6E82F, Feature.GroundLocationsKeepTo),
            ("ZoneSystem", "FindFloor", 2, 0x171A3343, Feature.PlacesCreaturesOnStage),
        };

        /// <summary>Public methods of the animation event receivers that are not events.</summary>
        private static readonly HashSet<string> NotEvents = new HashSet<string>
        {
            "CustomFixedUpdate", "CustomLateUpdate", "FindJoints", "CanChain",
        };

        private static bool _checked;

        /// <summary>The features the check found turned off by a missing part.</summary>
        private static List<Feature> _off = new List<Feature>();

        /// <summary>How many features had been found off when <see cref="OffCount"/> last counted, and what it counted.</summary>
        private static int _offFound = -1;

        private static int _offCounted;

        /// <summary>Whether the check has run in this session.</summary>
        public static bool Checked => _checked;

        /// <summary>How many features are off, cheap enough to ask every frame: counted again only when another has been found off.</summary>
        public static int OffCount
        {
            get
            {
                var found = _off.Count + Faults.ChangedFeatures.Count;
                if (found == _offFound) return _offCounted;
                _offFound = found;
                _offCounted = FeaturesOff().Count;
                return _offCounted;
            }
        }

        /// <summary>
        /// Every feature this version cannot offer on this version of the game, each once: those
        /// the check found missing a part, and those that failed since because the game changed.
        /// </summary>
        public static List<Feature> FeaturesOff() => Feature.Off(_off, Faults.ChangedFeatures);

        /// <summary>Checks once, in a world, and tells the log.</summary>
        public static void Check()
        {
            if (_checked) return;
            _checked = true;

            // Every game type is found by name and every part checked on its own, so a type or
            // member an update removed is told as missing rather than stopping the check.
            var list = new Checklist();
            void Each(string part, Action check) => Guard.Run(Feature.StartupCheck, "the startup check of " + part, check);
            Each("SpawnSystem.m_instances", () => Member(list, "SpawnSystem", "m_instances", Feature.WhereCreaturesSpawn));
            Each("ZSFX.m_fadeOutTimer", () => Member(list, "ZSFX", "m_fadeOutTimer", Feature.SoundsAfterSeek));
            Each("CharacterDrop.m_dropsEnabled", () => Member(list, "CharacterDrop", "m_dropsEnabled", Feature.DropsSeenInPlay));
            Each("the patches", () => Patches(list));
            foreach (var watched in Watched)
            {
                Each($"{watched.Type}.{watched.Method}", () => Code(list, watched.Type, watched.Method, watched.Params, watched.Shape, watched.Feature));
            }
            Each("CharacterAnimEvent", () => Events(list, "CharacterAnimEvent"));
            Each("AnimationEffect", () => Events(list, "AnimationEffect"));
            foreach (var layer in new[] { "Default", "terrain", "static_solid", "piece" })
            {
                Each($"layer {layer}", () => list.Add($"layer {layer}", Feature.FallingCopies(layer), LayerMask.NameToLayer(layer) >= 0 ? Found.Present : Found.Missing));
            }
            foreach (var script in StripPolicy.KeptScriptNames)
            {
                Each($"script {script}", () => list.Add($"script {script}", Feature.KeptScript(script), GameType(script) != null ? Found.Present : Found.Missing));
            }
            foreach (var (_, effect) in CombatWords.DamageEffects)
            {
                Each($"status effect {effect}", () =>
                {
                    var found = ObjectDB.instance != null && ObjectDB.instance.GetStatusEffect(effect.GetStableHashCode()) != null;
                    list.Add($"status effect {effect}", Feature.DamageLink(effect), found ? Found.Present : Found.Missing);
                });
            }
            Each("prefab " + GamePrefabs.PersonName, () =>
            {
                var player = GamePrefabs.Person != null;
                list.Add("prefab " + GamePrefabs.PersonName, Feature.PersonBeside, player ? Found.Present : Found.Missing);
            });

            _off = list.FeaturesOff;
            foreach (var line in list.Report())
            {
                if (list.AnyTrouble) Log.Warn(line);
                else Log.Report(line);
            }
        }

        /// <summary>
        /// One of the game's own types by name, from the game's assembly, where every script the
        /// copies keep is. Looking through all assemblies instead loads every mod's types, and
        /// logs the errors of those that cannot be.
        /// </summary>
        private static Type GameType(string name) => typeof(ZNetScene).Assembly.GetType(name, false);

        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Member(Checklist list, string typeName, string name, Feature feature)
        {
            var type = GameType(typeName);
            list.Add($"{typeName}.{name}", feature, type?.GetField(name, All) != null ? Found.Present : Found.Missing);
        }

        /// <summary>Each patch, by whether its method is there and carries Scry's patch now.</summary>
        private static void Patches(Checklist list)
        {
            foreach (var type in About.OwnTypes())
            {
                // A patch whose attribute names what the game no longer has is told missing by the list.
                if (Steps.Run(() => type.GetCustomAttributes(typeof(HarmonyPatch), false).FirstOrDefault() as HarmonyPatch, out var attribute, null) != null)
                {
                    list.Add(type.Name, FeatureOfPatch(type), Found.Missing);
                    continue;
                }
                if (attribute?.info?.declaringType == null) continue;

                var target = attribute.info.declaringType;
                var name = attribute.info.methodName;
                var feature = FeatureOfPatch(type);
                var method = target.GetMethods(All).FirstOrDefault(m => m.Name == name);
                var patched = method != null && Harmony.GetPatchInfo(method)?.Owners.Contains(About.Guid) == true;
                list.Add($"{target.Name}.{name}", feature, patched ? Found.Present : Found.Missing);
            }
        }

        private static void Code(Checklist list, string typeName, string name, int parameters, uint shape, Feature feature)
        {
            var type = GameType(typeName);
            var method = type?.GetMethods(All).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            if (method == null)
            {
                list.Add($"{typeName}.{name}", feature, Found.Missing);
                return;
            }
            // A coroutine's steps are in the state machine made for it, not in its own method: the
            // one its attribute names, which tells two coroutines of one name apart, else the
            // first named after it.
            var machine = (method.GetCustomAttributes(typeof(IteratorStateMachineAttribute), false).FirstOrDefault() as IteratorStateMachineAttribute)?.StateMachineType
                ?? type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(t => IlShape.IsStateMachineOf(t.Name, name));
            var steps = machine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ?? method;
            var now = IlShape.Of(steps.GetMethodBody()?.GetILAsByteArray());
            list.Add($"{typeName}.{name}", feature, now == shape ? Found.Present : Found.Changed);
        }

        /// <summary>
        /// An animation event the game answers and Scry does not: a creature whose clips send it
        /// has its animations left silent, so the check tells of it.
        /// </summary>
        private static void Events(Checklist list, string typeName)
        {
            var type = GameType(typeName);
            if (type == null)
            {
                list.Add(typeName, Feature.SoundsOfAnimations, Found.Missing);
                return;
            }
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.ReturnType != typeof(void) || method.GetParameters().Length > 1) continue;
                if (NotEvents.Contains(method.Name) || AnimationEvents.Answers(method.Name)) continue;
                list.Add($"animation event {type.Name}.{method.Name}", Feature.SoundsOfAnimationEvents, Found.Missing);
            }
        }
    }
}
