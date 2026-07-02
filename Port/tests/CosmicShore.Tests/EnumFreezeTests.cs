using System;
using CosmicShore.Data;

namespace CosmicShore.Tests;

/// <summary>
/// Freeze tests for every ported enum's numeric values. These values are wire format,
/// save-data format, and asset-reference format all at once — any drift between the
/// Unity original and the port (or between port versions) is a data-corruption bug.
/// </summary>
public class EnumFreezeTests
{
    [Theory]
    [InlineData(Domains.Jade, 1)]
    [InlineData(Domains.Ruby, 2)]
    [InlineData(Domains.Blue, 3)]
    [InlineData(Domains.Gold, 4)]
    public void Domains_Values_Frozen(Domains domain, int expected)
        => Assert.Equal(expected, (int)domain);

    [Theory]
    [InlineData(VesselClassType.Any, -1)]
    [InlineData(VesselClassType.Random, 0)]
    [InlineData(VesselClassType.Manta, 1)]
    [InlineData(VesselClassType.Dolphin, 2)]
    [InlineData(VesselClassType.Rhino, 3)]
    [InlineData(VesselClassType.Urchin, 4)]
    [InlineData(VesselClassType.Grizzly, 5)]
    [InlineData(VesselClassType.Squirrel, 6)]
    [InlineData(VesselClassType.Serpent, 7)]
    [InlineData(VesselClassType.Termite, 8)]
    [InlineData(VesselClassType.Falcon, 9)]
    [InlineData(VesselClassType.Shrike, 10)]
    [InlineData(VesselClassType.Sparrow, 11)]
    public void VesselClassType_Values_Frozen(VesselClassType vessel, int expected)
        => Assert.Equal(expected, (int)vessel);

    [Theory]
    [InlineData(GameModes.Random, 0)]
    [InlineData(GameModes.Elimination, 1)]
    [InlineData(GameModes.Rampage, 2)]
    [InlineData(GameModes.Darts, 3)]
    [InlineData(GameModes.ShootingGallery, 4)]
    [InlineData(GameModes.BlockBandit, 5)]
    [InlineData(GameModes.RiskyDriftness, 6)]
    // GameModes.Freestyle (7) retired upstream (bleeding-edge merge c833c580): the
    // standalone arcade Freestyle game was removed — freestyle now lives in Menu_Main
    // as the lava lamp. ID 7 stays reserved (see GameModes_Id7_IsIntentionallySkipped).
    [InlineData(GameModes.CellularDuel, 8)]
    [InlineData(GameModes.DashNGrab, 9)]
    [InlineData(GameModes.CellularBrawl, 10)]
    [InlineData(GameModes.Denial, 11)]
    [InlineData(GameModes.CatNMouse, 12)]
    [InlineData(GameModes.SlipNStride, 13)]
    [InlineData(GameModes.PumpNDump, 14)]
    [InlineData(GameModes.MasterExploder, 15)]
    [InlineData(GameModes.Soar, 16)]
    [InlineData(GameModes.ObstacleCourse, 17)]
    [InlineData(GameModes.Distraction, 18)]
    [InlineData(GameModes.RhinoRun, 19)]
    [InlineData(GameModes.KickinMass, 20)]
    [InlineData(GameModes.Sidewinder, 21)]
    [InlineData(GameModes.Multipass, 22)]
    [InlineData(GameModes.BotDuel, 23)]
    [InlineData(GameModes.Curvatious, 24)]
    [InlineData(GameModes.MazeRunner, 25)]
    [InlineData(GameModes.WildlifeBlitz, 26)]
    [InlineData(GameModes.ProtectMission, 27)]
    [InlineData(GameModes.MultiplayerFreestyle, 28)]
    [InlineData(GameModes.MultiplayerCellularDuel, 29)]
    [InlineData(GameModes.Multiplayer2v2CoOpVsAI, 30)]
    [InlineData(GameModes.MultiplayerWildlifeBlitzGame, 32)]
    [InlineData(GameModes.HexRace, 33)]
    [InlineData(GameModes.MultiplayerJoust, 34)]
    [InlineData(GameModes.MultiplayerCrystalCapture, 35)]
    // Tournament (36) + AstroLeague (37) added upstream (bleeding-edge merge c833c580).
    [InlineData(GameModes.Tournament, 36)]
    [InlineData(GameModes.AstroLeague, 37)]
    public void GameModes_Values_Frozen(GameModes mode, int expected)
        => Assert.Equal(expected, (int)mode);

    [Fact]
    public void GameModes_Id31_IsIntentionallySkipped()
        => Assert.False(Enum.IsDefined(typeof(GameModes), 31));

    [Fact]
    public void GameModes_Id7_IsIntentionallySkipped()
        // 7 was the retired standalone arcade Freestyle game (removed upstream,
        // bleeding-edge merge c833c580). Do not reuse the ID.
        => Assert.False(Enum.IsDefined(typeof(GameModes), 7));

    [Theory]
    [InlineData(Element.None, 0)]
    [InlineData(Element.Charge, 1)]
    [InlineData(Element.Mass, 2)]
    [InlineData(Element.Space, 3)]
    [InlineData(Element.Time, 4)]
    [InlineData(Element.Omni, 5)]
    public void Element_Values_Frozen(Element element, int expected)
        => Assert.Equal(expected, (int)element);

    [Theory]
    [InlineData(ApplicationState.None, 0)]
    [InlineData(ApplicationState.Bootstrapping, 1)]
    [InlineData(ApplicationState.Authenticating, 2)]
    [InlineData(ApplicationState.MainMenu, 3)]
    [InlineData(ApplicationState.LoadingGame, 4)]
    [InlineData(ApplicationState.InGame, 5)]
    [InlineData(ApplicationState.GameOver, 6)]
    [InlineData(ApplicationState.Paused, 7)]
    [InlineData(ApplicationState.Disconnected, 8)]
    [InlineData(ApplicationState.ShuttingDown, 9)]
    public void ApplicationState_Values_Frozen(ApplicationState state, int expected)
        => Assert.Equal(expected, (int)state);

    [Theory]
    [InlineData(MainMenuState.None, 0)]
    [InlineData(MainMenuState.Initializing, 1)]
    [InlineData(MainMenuState.Ready, 2)]
    [InlineData(MainMenuState.LaunchingGame, 3)]
    [InlineData(MainMenuState.Freestyle, 4)]
    public void MainMenuState_Values_Frozen(MainMenuState state, int expected)
        => Assert.Equal(expected, (int)state);

    [Theory]
    [InlineData(CellPhase.None, 0)]
    [InlineData(CellPhase.Calm, 1)]
    [InlineData(CellPhase.Restless, 2)]
    [InlineData(CellPhase.Frenzy, 3)]
    public void CellPhase_Values_Frozen(CellPhase phase, int expected)
        => Assert.Equal(expected, (int)phase);

    [Theory]
    [InlineData(CellAggressionLevel.Level0, 0)]
    [InlineData(CellAggressionLevel.Level1, 1)]
    [InlineData(CellAggressionLevel.Level2, 2)]
    public void CellAggressionLevel_Values_Frozen(CellAggressionLevel level, int expected)
        => Assert.Equal(expected, (int)level);

    [Theory]
    [InlineData(FaunaDiet.Herbivore, 0)]
    [InlineData(FaunaDiet.Predator, 1)]
    public void FaunaDiet_Values_Frozen(FaunaDiet diet, int expected)
        => Assert.Equal(expected, (int)diet);

    [Theory]
    [InlineData(InputEvents.FullSpeedStraightAction, 0)]
    [InlineData(InputEvents.RightStickAction, 1)]
    [InlineData(InputEvents.LeftStickAction, 2)]
    [InlineData(InputEvents.FlipAction, 3)]
    [InlineData(InputEvents.IdleAction, 4)]
    [InlineData(InputEvents.MinimumSpeedStraightAction, 5)]
    [InlineData(InputEvents.Button1Action, 6)]
    [InlineData(InputEvents.Button2Action, 7)]
    [InlineData(InputEvents.Button3Action, 8)]
    [InlineData(InputEvents.NodeTapAction, 9)]
    [InlineData(InputEvents.SelfTapAction, 10)]
    [InlineData(InputEvents.OnlyRightStickAction, 11)]
    [InlineData(InputEvents.OnlyLeftStickAction, 12)]
    [InlineData(InputEvents.BothSticksAction, 13)]
    public void InputEvents_Values_Frozen(InputEvents evt, int expected)
        => Assert.Equal(expected, (int)evt);

    [Theory]
    [InlineData(ShipActions.Boost, 1)]
    [InlineData(ShipActions.Invulnerability, 2)]
    [InlineData(ShipActions.ToggleCamera, 3)]
    [InlineData(ShipActions.ToggleMode, 4)]
    [InlineData(ShipActions.ToggleGyro, 5)]
    [InlineData(ShipActions.ZoomOut, 6)]
    [InlineData(ShipActions.GrowSkimmer, 7)]
    [InlineData(ShipActions.ChargeBoost, 8)]
    [InlineData(ShipActions.GrowTrail, 9)]
    [InlineData(ShipActions.Detach, 10)]
    [InlineData(ShipActions.PauseGuns, 11)]
    [InlineData(ShipActions.FireBigGun, 12)]
    [InlineData(ShipActions.LayBulletTrail, 13)]
    [InlineData(ShipActions.DropFakeCrystal, 14)]
    [InlineData(ShipActions.StartGuns, 15)]
    [InlineData(ShipActions.Drift, 16)]
    [InlineData(ShipActions.SpeedTubes, 17)]
    [InlineData(ShipActions.Bouncy, 18)]
    [InlineData(ShipActions.MachCone, 19)]
    [InlineData(ShipActions.ExplosiveAcorn, 20)]
    public void ShipActions_Values_Frozen(ShipActions action, int expected)
        => Assert.Equal(expected, (int)action);

    [Theory]
    [InlineData(TrailBlockImpactEffects.PlayHaptics, 0)]
    [InlineData(TrailBlockImpactEffects.DrainHalfAmmo, 1)]
    [InlineData(TrailBlockImpactEffects.DebuffSpeed, 2)]
    [InlineData(TrailBlockImpactEffects.DeactivateTrailBlock, 3)]
    [InlineData(TrailBlockImpactEffects.ActivateTrailBlock, 4)]
    [InlineData(TrailBlockImpactEffects.OnlyBuffSpeed, 5)]
    [InlineData(TrailBlockImpactEffects.GainResourceByVolume, 6)]
    [InlineData(TrailBlockImpactEffects.Steal, 7)]
    [InlineData(TrailBlockImpactEffects.DecrementLevel, 8)]
    [InlineData(TrailBlockImpactEffects.Attach, 9)]
    [InlineData(TrailBlockImpactEffects.GainResource, 10)]
    [InlineData(TrailBlockImpactEffects.Shield, 11)]
    [InlineData(TrailBlockImpactEffects.Stop, 12)]
    [InlineData(TrailBlockImpactEffects.Fire, 13)]
    [InlineData(TrailBlockImpactEffects.Bounce, 14)]
    [InlineData(TrailBlockImpactEffects.Explode, 15)]
    [InlineData(TrailBlockImpactEffects.FX, 16)]
    [InlineData(TrailBlockImpactEffects.FeelDanger, 17)]
    [InlineData(TrailBlockImpactEffects.Redirect, 18)]
    public void TrailBlockImpactEffects_Values_Frozen(TrailBlockImpactEffects effect, int expected)
        => Assert.Equal(expected, (int)effect);

    [Theory]
    [InlineData(CrystalImpactEffects.FillCharge, 1)]
    [InlineData(CrystalImpactEffects.DrainAmmo, 2)]
    [InlineData(CrystalImpactEffects.GainOneThirdMaxAmmo, 3)]
    [InlineData(CrystalImpactEffects.Boost, 5)]
    [InlineData(CrystalImpactEffects.AreaOfEffectExplosion, 6)]
    [InlineData(CrystalImpactEffects.IncrementLevel, 8)]
    [InlineData(CrystalImpactEffects.PlayFakeCrystalHaptics, 9)]
    [InlineData(CrystalImpactEffects.ReduceSpeed, 10)]
    [InlineData(CrystalImpactEffects.PlayHaptics, 11)]
    [InlineData(CrystalImpactEffects.StealCrystal, 12)]
    [InlineData(CrystalImpactEffects.GainFullAmmo, 13)]
    [InlineData(CrystalImpactEffects.AdjustLevel, 14)]
    public void CrystalImpactEffects_Values_Frozen(CrystalImpactEffects effect, int expected)
        => Assert.Equal(expected, (int)effect);

    [Theory]
    [InlineData(ShipImpactEffects.TrailSpawnerCooldown, 0)]
    [InlineData(ShipImpactEffects.PlayHaptics, 1)]
    [InlineData(ShipImpactEffects.SpinAround, 2)]
    [InlineData(ShipImpactEffects.Knockback, 3)]
    [InlineData(ShipImpactEffects.Stun, 4)]
    [InlineData(ShipImpactEffects.Charm, 5)]
    [InlineData(ShipImpactEffects.AreaOfEffectExplosion, 6)]
    public void ShipImpactEffects_Values_Frozen(ShipImpactEffects effect, int expected)
        => Assert.Equal(expected, (int)effect);

    [Theory]
    [InlineData(SkimmerStayEffects.ChangeResource, 1)]
    [InlineData(SkimmerStayEffects.FX, 3)]
    [InlineData(SkimmerStayEffects.Boost, 4)]
    [InlineData(SkimmerStayEffects.ScaleTrailAndCamera, 5)]
    [InlineData(SkimmerStayEffects.Align, 6)]
    [InlineData(SkimmerStayEffects.VizualizeDistance, 7)]
    [InlineData(SkimmerStayEffects.ScalePitchAndYaw, 8)]
    [InlineData(SkimmerStayEffects.ScaleHapticWithDistance, 9)]
    [InlineData(SkimmerStayEffects.ScaleGap, 10)]
    public void SkimmerStayEffects_Values_Frozen(SkimmerStayEffects effect, int expected)
        => Assert.Equal(expected, (int)effect);

    [Theory]
    [InlineData(ResourceType.Gauge, 0)]
    [InlineData(ResourceType.Item, 1)]
    public void ResourceType_Values_Frozen(ResourceType type, int expected)
        => Assert.Equal(expected, (int)type);

    [Theory]
    [InlineData(InputDeviceType.Touch, 0)]
    [InlineData(InputDeviceType.Gamepad, 1)]
    [InlineData(InputDeviceType.Keyboard, 2)]
    [InlineData(InputDeviceType.DualMouse, 3)]
    public void InputDeviceType_Values_Frozen(InputDeviceType type, int expected)
        => Assert.Equal(expected, (int)type);

    [Theory]
    [InlineData(BootStatusMode.Hide, 0)]
    [InlineData(BootStatusMode.Status, 1)]
    [InlineData(BootStatusMode.Retry, 2)]
    public void BootStatusMode_Values_Frozen(BootStatusMode mode, int expected)
        => Assert.Equal(expected, (int)mode);

    [Theory]
    [InlineData(ShipCameraOverrides.CloseCam, 3)]
    [InlineData(ShipCameraOverrides.FarCam, 4)]
    [InlineData(ShipCameraOverrides.ChangeFollowTarget, 5)]
    [InlineData(ShipCameraOverrides.SetFollowTarget, 6)]
    [InlineData(ShipCameraOverrides.Orthgraphic, 7)]
    [InlineData(ShipCameraOverrides.SetFixedFollowOffset, 8)]
    public void ShipCameraOverrides_Values_Frozen(ShipCameraOverrides cam, int expected)
        => Assert.Equal(expected, (int)cam);

    [Theory]
    [InlineData(CaptainLevel.Upgrade0, 0)]
    [InlineData(CaptainLevel.Upgrade5, 5)]
    public void CaptainLevel_Values_Frozen(CaptainLevel level, int expected)
        => Assert.Equal(expected, (int)level);

    [Theory]
    [InlineData(CallToActionTargetType.None, -1)]
    [InlineData(CallToActionTargetType.ArcadeMenu, 100)]
    [InlineData(CallToActionTargetType.StoreMenu, 200)]
    [InlineData(CallToActionTargetType.HangarMenu, 300)]
    [InlineData(CallToActionTargetType.PlayGameSport, 401)]
    [InlineData(CallToActionTargetType.PlayGameCurvatious, 436)]
    public void CallToActionTargetType_Values_Frozen(CallToActionTargetType target, int expected)
        => Assert.Equal(expected, (int)target);

    [Theory]
    [InlineData(UserActionType.None, -1)]
    [InlineData(UserActionType.ViewArcadeMenu, 100)]
    [InlineData(UserActionType.ViewStoreMenu, 200)]
    [InlineData(UserActionType.ViewHangarMenu, 300)]
    [InlineData(UserActionType.PlayGame, 400)]
    public void UserActionType_Values_Frozen(UserActionType action, int expected)
        => Assert.Equal(expected, (int)action);

    [Theory]
    [InlineData(ResourceEvents.AboveThreeQuartersAmmo, 0)]
    [InlineData(ResourceEvents.AboveHalfAmmo, 1)]
    public void ResourceEvents_Values_Frozen(ResourceEvents evt, int expected)
        => Assert.Equal(expected, (int)evt);

    [Theory]
    [InlineData(ScoringMetric.Crystals, 0)]
    [InlineData(ScoringMetric.OmniCrystals, 1)]
    [InlineData(ScoringMetric.ElementalCrystals, 2)]
    [InlineData(ScoringMetric.Jousts, 3)]
    public void ScoringMetric_Values_Frozen(ScoringMetric metric, int expected)
        => Assert.Equal(expected, (int)metric);
}
