using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// Shared vessel-layer test doubles: minimal IVessel / IVesselStatus implementations
// for exercising ElementalFloat binding, VesselAnimation, and action plumbing.
    class StubVesselStatus : IVesselStatus
    {
        public IVessel Vessel { get; set; }
        public bool AlignmentEnabled { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material AOEExplosionMaterial { get; set; }
        public bool IsAttached { get; set; }
        public Quaternion blockRotation { get; set; }
        public bool IsBoosting { get; set; }
        public float BoostMultiplier { get; set; }
        public float Inertia => 0f;
        public float ChargedBoostCharge { get; set; }
        public bool IsChargedBoostDischarging { get; set; }
        public Vector3 Course { get; set; }
        public bool IsDrifting { get; set; }
        public Transform CameraFollowTarget { get; set; }
        public bool GunsActive { get; set; }
        public bool HasLiveProjectiles { get; set; }
        public bool IsOverheating { get; set; }
        public IPlayer Player { get; set; }
        public bool IsPortrait { get; set; }
        public ResourceSystem ResourceSystem { get; set; }
        public VesselAnimation VesselAnimation => null;
        // Overrides the interface default (Player.InputStatus) so animation tests can
        // drive input without a full IPlayer chain.
        public IInputStatus InputStatus { get; set; }
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public VesselTransformer VesselTransformer { get; set; }
        public string Name => "stub";
        public VesselClassType VesselType => VesselClassType.Manta;
        public GameObject OrientationHandle => null;
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public float Speed { get; set; }
        public bool IsSingleStickControls { get; set; }
        public bool IsSlowed { get; set; }
        public bool IsStationary { get; set; }
        public bool IsTranslationRestricted { get; set; }
        public IVesselHUDController VesselHUDController => null;
        public VesselCustomization Customization => null;
        public R_ShipElementStatsHandler ElementalStatsHandler => null;
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public void ResetForPlay() { }
    }

    class StubVessel : IVessel
    {
        public event Action OnInitialized { add { } remove { } }
        public event Action OnBeforeDestroyed { add { } remove { } }
        public IVesselStatus VesselStatus { get; set; }
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => null;

        public readonly List<(string name, Element element)> Bound = new();
        public void BindElementalFloat(string name, Element element) => Bound.Add((name, element));

        public int SlowedAdds, SlowedRemoves;
        public Material LastShipMaterial, LastSkimmerMaterial, LastAOEMaterial, LastConicMaterial;
        public GameObject LastSilhouettePrefab;
        public (Color highlight, Color core)? LastTrailColors;

        public void Initialize(IPlayer player) { }
        public void PerformShipControllerActions(InputEvents @event) { }
        public void StopShipControllerActions(InputEvents @event) { }
        public void Teleport(Transform transform) { }
        public void SetResourceLevels(ResourceCollection resources) { }
        public void SetShipUp(float angle) { }
        public void DisableSkimmer() { }
        public void SetBoostMultiplier(float boostMultiplier) { }
        public void SetShipMaterial(Material material) => LastShipMaterial = material;
        public void SetBlockSilhouettePrefab(GameObject prefab) => LastSilhouettePrefab = prefab;
        public void SetAOEExplosionMaterial(Material material) => LastAOEMaterial = material;
        public void SetAOEConicExplosionMaterial(Material material) => LastConicMaterial = material;
        public void SetSkimmerMaterial(Material material) => LastSkimmerMaterial = material;
        public void SetTrailColors(Color highlightColor, Color coreColor) => LastTrailColors = (highlightColor, coreColor);
        public void ToggleAIPilot(bool toggle) { }
        public void StartVessel() { }
        public bool AllowClearPrismInitialization() => false;
        public void DestroyVessel() { }
        public void ResetForPlay() { }
        public void SetPose(Pose pose) { }
        public void ChangePlayer(IPlayer player) { }
        public void ModifyThrottle(float amount, float duration) { }
        public void AddSlowedShipTransformToGameData() => SlowedAdds++;
        public void RemoveSlowedShipTransformFromGameData() => SlowedRemoves++;
    }

