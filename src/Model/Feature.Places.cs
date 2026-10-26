namespace Scry
{
    /// <summary>Locations, dungeons, raids and biomes.</summary>
    internal sealed partial class Feature
    {
        public static readonly Feature WhichGearTakesPlaceOfWhich = Listed("which gear takes the place of which");
        public static readonly Feature LocationMusic = Listed("location music");
        public static readonly Feature LocationsAsBuilt = Listed("locations and rooms as the game builds them");
        public static readonly Feature ChancePartsOfLocationsAndRooms = Listed("the chance parts of locations and rooms");
        public static readonly Feature ExampleDungeonAndCampLayouts = Listed("example dungeons and camps");
        public static readonly Feature DungeonMaps = Listed("maps of the dungeons a player is in or has been in");
        public static readonly Feature DungeonMarksOnMap = Listed("marks on Valheim's map at the dungeons been in");
        public static readonly Feature GroupDungeonMaps = Listed("the walks of the dungeons kept on the server");
        public static readonly Feature BossFightsAmongRaids = Listed("boss fights among the raids");
        public static readonly Feature PlacesCreaturesOnStage = Listed("places' creatures on the stage");
        public static readonly Feature CreatureStars = Listed("which creatures can come with stars");
        public static readonly Feature PlacesPathsOnStageGround = Listed("places' paths on the stage's ground");
        public static readonly Feature PlacesGroundOnStage = Listed("places' dug ground, liquids and settling parts on the stage");
        public static readonly Feature RunestoneTexts = Listed("runestone texts");
        public static readonly Feature RaidsStarsOnStage = Listed("raids' stars on the stage");
        public static readonly Feature RaidsRolledOnStage = Listed("raids rolled on the stage");
        public static readonly Feature Locations = Listed("the Locations tab");
        public static readonly Feature PlacesKept = Listed("keeping what locations hold from one session to the next");
        public static readonly Feature FloorsKept = Listed("keeping where places' floors are from one session to the next");
        public static readonly Feature LocationsOnStage = Listed("locations on the stage");
        public static readonly Feature Biomes = Listed("the Biomes tab");
        public static readonly Feature Raids = Listed("the Raids tab");
        public static readonly Feature RaidOrigins = Listed("telling the game's raids from those mods add");
        public static readonly Feature LocationOrigins = Listed("telling the game's locations from those mods add");
        public static readonly Feature RoomOrigins = Listed("telling the game's dungeon rooms from those mods add");
    }
}
