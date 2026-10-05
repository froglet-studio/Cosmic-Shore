using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc F 2b-i — the arcade service foundation, headless: FavoriteSystem's
// toggle/notify/persist loop and LoadoutSystem's per-game loadout round-trip
// (both back onto the real DataAccessor JSON store). Tests leave no net state:
// favorites toggle back, and the saved game loadout is overwritten idempotently
// on every run (same key, deterministic value).
// ─────────────────────────────────────────────────────────────────────────────

public class ArcadeServiceFoundationTests
{
    [Fact]
    public void FavoriteSystem_Toggle_FlipsStateAndNotifies()
    {
        var game = GameModes.HexRace;
        bool initial = FavoriteSystem.IsFavorited(game);

        var events = new List<(GameModes game, bool favorited)>();
        void Record(GameModes g, bool f) => events.Add((g, f));
        FavoriteSystem.OnFavoriteChanged += Record;
        try
        {
            FavoriteSystem.ToggleFavorite(game);
            Assert.Equal(!initial, FavoriteSystem.IsFavorited(game));
            Assert.Equal((game, !initial), events[^1]);

            FavoriteSystem.ToggleFavorite(game);                  // toggle back — no net change
            Assert.Equal(initial, FavoriteSystem.IsFavorited(game));
            Assert.Equal((game, initial), events[^1]);
            Assert.Equal(2, events.Count);
        }
        finally
        {
            FavoriteSystem.OnFavoriteChanged -= Record;
        }
    }

    [Fact]
    public void LoadoutSystem_GameLoadout_RoundTrips()
    {
        LoadoutSystem.Init();

        var saved = new Loadout(3, 4, VesselClassType.Squirrel, GameModes.HexRace, isMultiplayer: true);
        LoadoutSystem.SaveGameLoadOut(GameModes.HexRace, saved);

        var loaded = LoadoutSystem.LoadGameLoadout(GameModes.HexRace, isMultiplayer: true);
        Assert.Equal(GameModes.HexRace, loaded.GameMode);
        Assert.Equal(3, loaded.Loadout.Intensity);
        Assert.Equal(4, loaded.Loadout.PlayerCount);
        Assert.Equal(VesselClassType.Squirrel, loaded.Loadout.VesselType);
        Assert.True(loaded.Loadout.IsMultiplayer);
        Assert.True(loaded.Loadout.Initialized);
    }

    [Fact]
    public void LoadoutSystem_UnknownMode_ReturnsUninitializedDefault()
    {
        LoadoutSystem.Init();

        // Elimination has no saved loadout (nothing in this suite writes one).
        var fallback = LoadoutSystem.LoadGameLoadout(GameModes.Elimination, isMultiplayer: false);
        Assert.Equal(GameModes.Elimination, fallback.GameMode);
        Assert.False(fallback.Loadout.Initialized);               // the all-defaults sentinel
        Assert.False(fallback.Loadout.IsMultiplayer);
    }

    [Fact]
    public void LoadoutSystem_ActiveLoadout_FollowsIndexAndWrites()
    {
        LoadoutSystem.Init();
        Assert.True(LoadoutSystem.CheckLoadoutsExist(0));         // Init seeds 4 slots

        var custom = new Loadout(2, 2, VesselClassType.Manta, GameModes.HexRace, isMultiplayer: false);
        LoadoutSystem.SetLoadout(custom, 1);
        LoadoutSystem.SetActiveLoadoutIndex(1);

        Assert.Equal(1, LoadoutSystem.GetActiveLoadoutIndex());
        Assert.Equal(custom.ToString(), LoadoutSystem.GetActiveLoadout().ToString());

        LoadoutSystem.SetActiveLoadoutIndex(0);                   // restore the default slot
    }
}
