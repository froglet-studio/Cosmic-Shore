namespace CosmicShore.App.Systems.UserActions
{
    public enum UserActionType
    {
        None = -1,

        // Arcade Related -- 100s
        ViewArcadeMenu = 100,
        ViewArcadeLoadoutMenu = 101,
        ViewArcadeExploreMenu = 102,
        ViewArcadeGameDarts = 110,
        ViewArcadeGameRampage = 111,

        // Store Related - 200s
        ViewStoreMenu = 200,
        ClickDailyReward = 201,

        // Hangar Related - 300s
        ViewHangarMenu = 300,
        ViewShipManta = 310,
        ViewShipRhino = 311,
        ViewShipSquirrel = 312,

        // Game Related - 400s
        PlayGame = 400,


        /*********** ADDED BY WILL *************/
    }
}