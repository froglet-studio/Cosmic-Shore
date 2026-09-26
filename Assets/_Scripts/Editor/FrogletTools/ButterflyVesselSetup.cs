#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;   // ClientNetworkTransform, NetcodeHooks — NOT Unity.Netcode.Components
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Builds the <b>Butterfly</b> vessel — the prefab, its effect containers, its action and
    /// camera assets, its class asset, and its two registrations — from nothing.
    ///
    /// <para><b>Why this is an editor tool and not a Python generator.</b> Every other asset in
    /// this branch was authored headlessly, and a vessel prefab cannot be: its root carries a
    /// <c>NetworkObject</c>, whose <c>GlobalObjectIdHash</c> Unity computes and which every other
    /// prefab in <c>DefaultNetworkPrefabs</c> must be distinct from. A hand-authored hash that
    /// collides does not fail loudly — it makes the prefab indistinguishable from another
    /// in-scene object and breaks scene synchronization for every later joiner
    /// (<c>Docs/PartySystem/BUGS.md</c> B16). The same is true of nested prefab INSTANCES (the
    /// skimmers, the tail, the jets, the HUD), which carry modification blocks Unity owns.</para>
    ///
    /// <para><b>The prefab is built from SCRATCH rather than cloned.</b> Cloning the Scarab would
    /// inherit its hidden Sparrow FBX instance, its containers and its Scarab-only components —
    /// every one of which would then have to be remembered and removed. Building explicitly means
    /// every authored number on this vessel is visible in THIS FILE, which is also the only place
    /// they are written down until someone opens the prefab.</para>
    ///
    /// <para><b>Every write goes through <see cref="Set"/>.</b> A <c>SerializedObject</c> write to a
    /// field that does not exist is a silent no-op, which is indistinguishable from success — so a
    /// missing property is COLLECTED and reported, and the tool refuses to call itself finished
    /// with unwired fields outstanding.</para>
    ///
    /// <para>Run it, check the report, then use <b>Validate &amp; Push</b> at the bottom — a tool's
    /// real deliverable is the assets it wrote, and those land in the working tree rather than on
    /// the branch (<c>Docs/TOOLING.md</c>).</para>
    /// </summary>
    public class ButterflyVesselSetup : EditorWindow
    {
        const string ToolName = "Butterfly Vessel Setup";

        const string VesselName = "Butterfly";
        const string PrefabPath = "Assets/_Prefabs/Spacevessels/Butterfly.prefab";
        const string ActionDir = "Assets/_SO_Assets/VesselActions/Butterfly";
        const string EffectDir = "Assets/_SO_Assets/Effects";
        const string CameraPath = "Assets/_SO_Assets/Camera/ButterflyCameraSettingsSO.asset";
        const string ClassPath = "Assets/_SO_Assets/Classes/SO_Class_Butterfly.asset";
        const string VesselContainerPath = "Assets/_SO_Assets/Vessel Prefab Container.asset";
        const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        // Reference vessels we borrow shared wiring from. The Squirrel is the fleet's reference
        // two-thumb hull and the Butterfly is a two-thumb hull, so its prism-spawn channel, its
        // baseline prism effects and its game-data reference are the right ones to mirror.
        const string SquirrelPrefabPath = "Assets/_Prefabs/Spacevessels/Squirrel.prefab";
        // The ONE skimmer: a trigger capsule hanging below the hull, live only in Dust mode. The
        // prefab (and the bloom AOE below) are authored by Tools/Build/author_butterfly_dust.py,
        // which also owns every dust effect asset's NUMBERS — this tool only wires them.
        const string DustSkimmerPrefabPath =
            "Assets/_Prefabs/Spacevessels/Components/ButterflyDustSkimmer.prefab";
        const string BloomPrefabPath = "Assets/_Prefabs/Projectile/AOEButterflyBloom.prefab";
        const string HudBasePrefabPath = "Assets/_Prefabs/UI Elements/VesselHUD/VesselHUDPrefab.prefab";
        const string HudVariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/ButterflyHUDVariant.prefab";

        readonly List<string> _log = new();
        readonly List<string> _unwired = new();
        Vector2 _scroll;

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            ToolScriptPaths = new[]
                { "Assets/_Scripts/Editor/FrogletTools/ButterflyVesselSetup.cs" },
            CommitType = "feat",
            CommitScope = "vessel",
            CommitSubject = _ => "feat(vessel): the BUTTERFLY — assets, prefab and registrations",
        };

        [MenuItem("FrogletTools/Vessels/Create Butterfly Vessel")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Builds the Butterfly vessel prefab, its effect containers, action and " +
                          "camera assets, class asset and both registrations. Idempotent.")]
        public static void Open() =>
            GetWindow<ButterflyVesselSetup>(true, "Butterfly Vessel Setup").minSize =
                new Vector2(520, 460);

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Butterfly", "The meditative surface painter",
                                        FrogletEditorPalette.Violet);

            EditorGUILayout.HelpBox(
                "Builds every Butterfly asset that needs Unity to author it. Safe to re-run: " +
                "existing assets are updated in place, not duplicated.\n\n" +
                "After running, read the report below — anything listed as UNWIRED is a field " +
                "this tool could not find, and is a real gap rather than a warning.",
                MessageType.Info);

            if (GUILayout.Button("Build the Butterfly", GUILayout.Height(34)))
                Build();

            if (_log.Count > 0)
            {
                EditorGUILayout.Space();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));
                foreach (var line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();

                if (_unwired.Count > 0)
                    EditorGUILayout.HelpBox(
                        $"{_unwired.Count} field(s) could not be wired — see the report. The " +
                        "vessel will spawn but will not be complete.", MessageType.Error);
            }

            FrogletToolShipPanel.Draw(Ship, this);
        }

        // ─────────────────────────────────────────────────────────────── build

        void Build()
        {
            _log.Clear();
            _unwired.Clear();

            try
            {
                EnsureFolder(ActionDir);

                var fold = CreateOrUpdate<FoldActionSO>($"{ActionDir}/ButterflyFoldAction.asset");
                var spread = CreateOrUpdate<SpreadWingsActionSO>($"{ActionDir}/ButterflySpreadWingsAction.asset");

                var effects = BuildEffects();
                var containers = BuildContainers(effects);
                var camera = BuildCameraSettings();
                var hudVariant = BuildHudVariant();
                var prefab = BuildPrefab(fold, spread, containers, camera, hudVariant);
                if (prefab) { BuildClassAsset(prefab); Register(prefab); }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (prefab) VerifyRegistrations(prefab);

                Note(_unwired.Count == 0
                    ? "DONE — every field wired. Use Validate & Push below."
                    : $"DONE with {_unwired.Count} UNWIRED field(s) — fix before pushing.");
            }
            catch (Exception e)
            {
                Note("FAILED: " + e.Message);
                Debug.LogException(e);
            }
        }

        // ─────────────────────────────────────────────────────── effects & containers

        class Effects
        {
            public ScriptableObject DustPrism, DustDebuff, DustWither, DustNourish, CombatHit, Bloom;
        }

        Effects BuildEffects()
        {
            var e = new Effects();

            // The dust on MASS: own-domain prisms grow / turn dangerous / shield (Space 5 adds a
            // rare super-shield); opposing prisms are destroyed / shrunk / stolen. Numbers are
            // the generator's (author_butterfly_dust.py); this only makes sure the asset exists.
            e.DustPrism = CreateOrUpdate<SkimmerScaleDustPrismEffectSO>(
                $"{EffectDir}/Skimmer Prism Effects/ButterflyScaleDustPrismEffect.asset");

            // CHARGE — the dust's bite on a pilot, scaled by the attacker's Charge.
            e.DustDebuff = CreateOrUpdate<VesselElementalDebuffBySkimmerEffectSO>(
                $"{EffectDir}/Vessel Skimmer Effects/ButterflyScaleDustDebuffBySkimmerEffect.asset", so =>
                {
                    // DERIVED, not chosen: a Strike-class hit is 8 points and a hit's bite tracks
                    // its price — 8 × (2.0/12) = 1.3333 total, over four elements.
                    Set(so, "debuffMagnitude", -0.3333333f);
                    Set(so, "debuffDuration", 4f);
                    Set(so, "upgradeElement", (int)Element.Charge);
                    Set(so, "upgradeBiteMultiplier", 2f);
                    Set(so, "cooldown", 1f);
                    // x0.5 at Charge 0, x2 at Charge 10 — read at the pilot's REPLICATED level.
                    SetElementalFloat(so, "biteScale", Element.Charge, 0.5f, 2f);
                });

            // Lifeforms, partitioned by colour: opposing hearts wither (flora AND fauna now),
            // ally hearts are refreshed.
            e.DustWither = CreateOrUpdate<SkimmerWitherLifeformByCrystalEffectSO>(
                $"{EffectDir}/Skimmer Crystal Effects/ButterflyScaleDustWitherLifeformEffect.asset", so =>
                {
                    Set(so, "faunaOnly", false);
                    Set(so, "sparesOwnDomain", true);
                });
            e.DustNourish = CreateOrUpdate<SkimmerNourishLifeformByCrystalEffectSO>(
                $"{EffectDir}/Skimmer Crystal Effects/ButterflyDustNourishLifeformEffect.asset");

            e.CombatHit = CreateOrUpdate<VesselCombatHitBySkimmerEffectSO>(
                $"{EffectDir}/Vessel Skimmer Effects/ButterflyCombatHitBySkimmerEffect.asset", so =>
                {
                    Set(so, "hitClass", (int)CombatHitClass.Strike);
                    // OFF: this is the slowest hull in the fleet. An overtake requirement would
                    // mean it could never score, which is the same trap the wither effect records.
                    Set(so, "requireFasterThanVictim", false);
                    Set(so, "requireOwningMachine", true);
                    Set(so, "sameVictimCooldownSeconds", 1f);
                    CopyReference(so, "onCombatHitLanded",
                        FindFirstAssetNamed("Event_CombatHitStats"), "combat-hit stats channel");
                });

            // The omni-crystal bloom: kills opposing lifeform hearts in a 450 u radius. The
            // generator authors the prefab, its container and this crystal effect's numbers.
            e.Bloom = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                $"{EffectDir}/Vessel Crystal Effects/ButterflyVesselExplosionByCrystalEffect.asset");
            if (!e.Bloom) Unwired("ButterflyVesselExplosionByCrystalEffect",
                "run Tools/Build/author_butterfly_dust.py first");

            return e;
        }

        class Containers
        {
            public VesselImpactorDataContainerSO Vessel;
            public SkimmerImpactorDataContainerSO Dust;
        }

        Containers BuildContainers(Effects e)
        {
            var c = new Containers();

            // The vessel container mirrors the Squirrel's baseline prism trio — damage, haptics,
            // danger-prism debuff — because a prism should read the same whichever hull hits it.
            // Its OMNI crystal list is the Butterfly's own: the bloom, then the haptics.
            c.Vessel = CreateOrUpdate<VesselImpactorDataContainerSO>(
                $"{EffectDir}/Effect Containers/VesselContainers/ButterflyImpactorDataContainer.asset",
                so =>
                {
                    CopyArrayFromReferenceContainer(so, "SquirrelImpactorDataContainer",
                        new[] { "vesselPrismEffects" });
                    var haptics = FindFirstAssetNamed("VesselHapticsByCrystalEffect");
                    var crystal = new List<UnityEngine.Object>();
                    if (e.Bloom) crystal.Add(e.Bloom);
                    if (haptics) crystal.Add(haptics);
                    SetArray(so, "vesselCrystalEffects", crystal);
                });

            c.Dust = CreateOrUpdate<SkimmerImpactorDataContainerSO>(
                $"{EffectDir}/Effect Containers/SkimmerContainers/ButterflyDustSkimmerImpactorDataContainer.asset",
                so =>
                {
                    SetArray(so, "skimmerPrismEffectsSO", new[] { e.DustPrism });
                    SetArray(so, "vesselSkimmerEffectsSO", new[] { e.DustDebuff, e.CombatHit });
                    SetArray(so, "skimmerLifeformCrystalEffectsSO", new[] { e.DustWither, e.DustNourish });
                });

            return c;
        }

        CameraSettingsSO BuildCameraSettings() =>
            CreateOrUpdate<CameraSettingsSO>(CameraPath, so =>
            {
                // "flies slow from far away" — the brief's framing, and it is load-bearing for
                // more than the look: the prism occlusion corridor and the vessel-tail width are
                // both derived from |followOffset.z|, so this one number sizes the whole vessel's
                // relationship with the camera. 70% further than it first shipped (22/-120).
                Set(so, "followOffset", new Vector3(0f, 37.4f, -204f));

                // STATED, not inherited. This tool set followOffset and nothing else, so every
                // other field came out at the C# initializer - and farClipPlane's was 1000 against
                // the fleet's 12000, which does not even cross a standard 1200-radius cell.
                Set(so, "farClipPlane", 12000f);
            });

        // ─────────────────────────────────────────────────────────────── HUD

        GameObject BuildHudVariant()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudBasePrefabPath);
            if (!basePrefab) { Unwired("HUD base prefab", HudBasePrefabPath); return null; }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudVariantPath);
            if (existing)
            {
                EnsureHudView(existing);
                Note("HUD variant already present — left in place.");
                // RE-LOAD: EnsureHudView may have re-saved the asset at this path, which
                // invalidates the reference we are holding.
                return AssetDatabase.LoadAssetAtPath<GameObject>(HudVariantPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = "ButterflyHUDVariant";
            StripMissingScripts(instance);
            // SaveAsPrefabAsset on a prefab INSTANCE produces a VARIANT, which is what the fleet's
            // Squirrel / Manta / Serpent HUDs are. A hard copy would sever propagation — the exact
            // failure Docs/GAMECANVAS.md records for the forked GameCanvas.
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, HudVariantPath);
            if (!variant)
            {
                Unwired("HUD variant", "SaveAsPrefabAsset refused " + HudVariantPath);
                DestroyImmediate(instance);
                return null;
            }
            DestroyImmediate(instance);
            EnsureHudView(variant);
            FrogletToolChangeLedger.Record(ToolName, HudVariantPath);
            Note("Created HUD variant " + HudVariantPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudVariantPath);
        }

        void EnsureHudView(GameObject variant)
        {
            if (!variant) return;
            if (variant.GetComponentInChildren<ButterflyHUDView>(true)) return;

            var root = PrefabUtility.LoadPrefabContents(HudVariantPath);
            try
            {
                StripMissingScripts(root);
                var legacy = root.GetComponentInChildren<VesselHUDView>(true);
                var host = legacy ? legacy.gameObject : root;
                // The base view IS the contract (IVesselHUDView is an empty marker nothing
                // implements), so the Butterfly's view has to REPLACE it on the same object
                // rather than sit beside it — two VesselHUDViews on one HUD is two answers to
                // every ability-row query.
                if (legacy) DestroyImmediate(legacy, true);
                host.AddComponent<ButterflyHUDView>();
                PrefabUtility.SaveAsPrefabAsset(root, HudVariantPath);
                Note("Added ButterflyHUDView to the HUD variant.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ─────────────────────────────────────────────────────────────── prefab

        GameObject BuildPrefab(FoldActionSO fold, SpreadWingsActionSO spread,
                               Containers containers, CameraSettingsSO camera, GameObject hudVariant)
        {
            var root = new GameObject(VesselName);
            try
            {
                // ---- networking ----
                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();
                root.AddComponent<NetcodeHooks>();

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                // ---- the hull ----
                var hullGo = new GameObject("Hull");
                hullGo.transform.SetParent(root.transform, false);
                hullGo.AddComponent<MeshFilter>();
                var hullRenderer = hullGo.AddComponent<MeshRenderer>();
                // TWO slots: submesh 0 is the body, submesh 1 is the wings, and the fleet paints
                // the DOMAIN colour onto slot 1 (ShipHelper.ApplyShipMaterial). A one-slot hull
                // throws IndexOutOfRange at theming.
                hullRenderer.sharedMaterials = BorrowVesselMaterials();
                hullGo.AddComponent<ButterflyHullBuilder>();

                var hullCollider = hullGo.AddComponent<SphereCollider>();
                hullCollider.radius = 6f;
                var impactCollider = hullGo.AddComponent<ImpactCollider>();

                // ---- core vessel components ----
                // ORDER IS LOAD-BEARING, and it is the whole reason this block is not a flat list
                // of AddComponent calls. Four fleet components declare a [RequireComponent] naming
                // an INTERFACE or an ABSTRACT class — VesselController and ResourceSystem name
                // IVesselStatus, VesselImpactor names IVessel, VesselStatus names VesselAnimation.
                // Unity cannot ADD any of those, so if the dependency is not ALREADY on the object
                // it logs "Can't add script behaviour 'X'. The script class can't be abstract!" and
                // AddComponent returns NULL — and every Set() against that null then reports
                // "target is null", which reads as a wiring bug in this tool rather than as an
                // ordering one. Satisfy each with a CONCRETE implementor first and Unity's
                // GetComponent(requiredType) finds it, so nothing is ever added.
                var animation = Require<ButterflyAnimation>(root);        // is a VesselAnimation
                var status = Require<VesselStatus>(root);                 // is an IVesselStatus
                var controller = Require<VesselController>(root);         // is an IVessel
                var resources = Require<ResourceSystem>(root);
                var impactor = Require<VesselImpactor>(root);

                // VesselStatus's own [RequireComponent]s are concrete, so Unity has ALREADY added
                // VesselCameraCustomizer, R_VesselActionHandler, VesselCustomization and
                // R_ShipElementStatsHandler by this point. Require<T> takes the existing one —
                // a bare AddComponent here would mint a SECOND copy of each (none of them carries
                // [DisallowMultipleComponent]), and two R_VesselActionHandlers is two answers to
                // every ability press.
                var cameraCustomizer = Require<VesselCameraCustomizer>(root);
                var actionHandler = Require<R_VesselActionHandler>(root);
                var customization = Require<VesselCustomization>(root);
                Require<R_ShipElementStatsHandler>(root);

                // The BASE transformer: the Butterfly is a TWO-THUMB hull, so it must NOT get
                // SingleStickVesselTransformer (which sets IsSingleStickControls and would send it
                // to the mouse-as-one-stick desktop scheme). Nothing else about it is special —
                // slow is an authored number, not a new flight model.
                var transformer = Require<VesselTransformer>(root);
                var prisms = Require<VesselPrismController>(root);
                Require<AIPilot>(root);
                Require<ElementalBarsController>(root);
                var hud = Require<ButterflyHUDController>(root);
                Require<VesselTailAndJets>(root);

                // A missing core component means every later Set() would report "target is null"
                // and bury the one line that says why. Stop here instead.
                if (!animation || !status || !controller || !resources || !impactor ||
                    !transformer || !prisms || !actionHandler || !hud)
                {
                    Note("ABORTED: a core component could not be added — see UNWIRED above. " +
                         "No prefab was written.");
                    return null;
                }

                // ---- ability executors ----
                var actionsGo = new GameObject("VesselActions");
                actionsGo.transform.SetParent(root.transform, false);
                var registry = actionsGo.AddComponent<ActionExecutorRegistry>();

                var foldGo = new GameObject("Fold");
                foldGo.transform.SetParent(actionsGo.transform, false);
                var foldExec = foldGo.AddComponent<FoldActionExecutor>();
                Set(foldExec, "config", fold);

                var spreadGo = new GameObject("SpreadWings");
                spreadGo.transform.SetParent(actionsGo.transform, false);
                var spreadExec = spreadGo.AddComponent<SpreadWingsActionExecutor>();
                Set(spreadExec, "config", spread);

                SetArray(registry, "_executors", new UnityEngine.Object[] { foldExec, spreadExec });

                // ---- the ONE skimmer: the dust capsule ----
                // SPACE's continuous dial is its LENGTH: Skimmer.Scale is an ElementalFloat
                // evaluated live and, with elongateYOnly, it drives only local Y — so Space
                // lengthens the column hanging below the hull and changes nothing else.
                var dust = InstantiateDustSkimmer(root.transform, containers.Dust);
                Set(spreadExec, "dustField", dust ? dust.GetComponentInChildren<ButterflyDustField>(true) : null);

                // ---- HUD ----
                Transform hudContainer = null;
                if (hudVariant)
                {
                    var hudHost = new GameObject("ShipHUDContainer");
                    hudHost.transform.SetParent(root.transform, false);
                    var canvas = hudHost.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    hudHost.AddComponent<UnityEngine.UI.CanvasScaler>();
                    hudHost.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudVariant, hudHost.transform);
                    hudContainer = hudInstance.transform;
                }

                // ---- wiring ----
                // FIRST: adopt every SHARED CHANNEL the reference hull already subscribes to.
                // These are SOAP events and config SOs — one asset per channel, fleet-wide — and
                // the fleet FAILS LOUD on a missing one by policy: R_VesselActionHandler does
                // `_onButtonPressed.OnRaised += ...` with no guard, so an unwired channel is a
                // NullReferenceException the first time the vessel subscribes, which happens
                // INSIDE the swap. The swap's catch then reports a NullReferenceException in
                // SubscribeToInputEvents and the pilot gets a hull that turns and has no
                // throttle and no abilities — every symptom pointing at flight code, none at the
                // six empty fields that caused it. Enumerating them by hand is what let that
                // happen; a sweep cannot forget one.
                AdoptSharedAssetReferences(root, SquirrelPrefabPath);
                Set(root.GetComponent<AIPilot>(), "actionExecutorRegistry", registry);

                WireStatus(status, controller, hud, dust);
                WireController(controller);
                Set(cameraCustomizer, "settings", camera);
                Set(impactor, "vesselImpactorDataContainerSO", containers.Vessel);
                SetArray(customization, "_shipGeometries", new UnityEngine.Object[] { hullGo });
                Set(impactCollider, "impactorObject", impactor);
                WireTransformer(transformer);
                WirePrisms(prisms, dust ? dust.GetComponentInChildren<Skimmer>(true) : null);
                WireResources(resources);
                WireActionHandler(actionHandler, registry, fold, spread);
                WireHud(hud, hudContainer, foldExec, spreadExec);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                FrogletToolChangeLedger.Record(ToolName, PrefabPath);
                Note("Wrote " + PrefabPath);
                return saved;
            }
            finally { DestroyImmediate(root); }
        }

        GameObject InstantiateDustSkimmer(Transform parent, SkimmerImpactorDataContainerSO container)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DustSkimmerPrefabPath);
            if (!prefab)
            {
                Unwired("ButterflyDustSkimmer", DustSkimmerPrefabPath +
                        " — run Tools/Build/author_butterfly_dust.py first");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "ButterflyDustSkimmer";

            var skimmerImpactor = go.GetComponentInChildren<SkimmerImpactor>(true);
            if (skimmerImpactor) Set(skimmerImpactor, "skimmerImpactorDataContainer", container);
            else Unwired("ButterflyDustSkimmer.SkimmerImpactor", "not found on the dust prefab");

            if (!go.GetComponentInChildren<ButterflyDustField>(true))
                Unwired("ButterflyDustSkimmer.ButterflyDustField", "not found on the dust prefab");
            return go;
        }

        void WireStatus(VesselStatus status, VesselController controller,
                        MonoBehaviour hud, GameObject dust)
        {
            Set(status, "vesselType", (int)VesselClassType.Butterfly);
            Set(status, "_name", VesselName);
            Set(status, "_shipInstance", controller);
            Set(status, "vesselHUDController", hud);
            // VesselController.Initialize initializes ONLY these two references — a skimmer the
            // status does not point at is permanently inert dead weight, silently (the Dolphin
            // shipped that way for its whole life). ONE skimmer: the far field is empty.
            if (dust) Set(status, "_nearFieldSkimmer", dust.GetComponentInChildren<Skimmer>(true));
            Set(status, "_farFieldSkimmer", (UnityEngine.Object)null);
        }

        void WireController(VesselController controller) =>
            CopyReference(controller, "gameData", FindFirstAssetNamed("Runtime GameData"), "GameDataSO");

        void WireTransformer(VesselTransformer t)
        {
            // SLOW, and slow to turn. This is the whole vessel: a cruise a third of the Squirrel's
            // and a turn rate well under the fleet's, so a corner is something you commit to.
            Set(t, "DefaultThrottleScaler", 55f);
            Set(t, "DefaultMinimumSpeed", 12f);
            Set(t, "PitchScaler", 45f);
            Set(t, "YawScaler", 45f);
            Set(t, "RollScaler", 70f);
            // ZERO, and load-bearing: the Fold stops the vessel with IsTranslationRestricted and
            // then wants all four stick axes for placing the ghost. TurnScalar returns this while
            // restricted, so 0 kills pitch and yaw for the hold. Roll is deliberately NOT scaled
            // by it, which is how YDiff goes on rolling the frame (FoldActionExecutor's note).
            Set(t, "restrictedTurnMultiplier", 0f);
        }

        void WirePrisms(VesselPrismController p, Skimmer dustSkimmer)
        {
            // THE POOL. `prismType` selects which PrismFactory pool a laid prism comes from, and
            // it defaults to 0 — PrismType.Dolphin — so a vessel that never authors it lays
            // another ship's prisms and nothing says so. The Butterfly takes the Squirrel's, the
            // same reference two-thumb hull its prism-spawn channel and baseline prism effects
            // come from, and the one whose trail the Urchin already rides.
            Set(p, "prismType", (int)PrismType.Squirrel);

            // THE CLEARANCE SKIMMER. Not optional while `waitTillOutsideSkimmer` is on: this is
            // read per prism, so an unwired one used to throw from inside the spawn loop — which
            // swallows the exception and ENDS it, leaving the hull flying with no trail for the
            // rest of its life. VesselPrismController degrades now, but an authored reference is
            // still the point: the dust capsule hangs below the hull, so a prism laid inside it
            // needs the delay more than most of the fleet does.
            Set(p, "skimmer", dustSkimmer);

            // THE PIANO KEYS: wide across (x), thin (y), SHORT along the flight path (z), laid at
            // a wavelength that leaves air between them — so the wake reads as a row of keys
            // rather than a ribbon, and a curve through them reads as a surface.
            Set(p, "BaseScale", new Vector3(26f, 1.2f, 3.4f));
            // The narrow line (Dust mode) is XScaler = minBlockScale; Mass mode widens it through
            // VesselPrismController.WidthMultiplier (SpreadWingsActionExecutor), 5x..20x.
            Set(p, "minBlockScale", 0.35f);
            Set(p, "maxBlockScale", 1f);
            Set(p, "initialWavelength", 9f);
            Set(p, "minWavelength", 5f);
            Set(p, "Gap", 0f);                // ONE wide key, not two rails
            // OFF: Mass's one parameter is the Mass-mode WIDTH (SpreadWingsActionSO), and a second
            // Mass dial growing the same prism would be the double-dip the convention forbids.
            SetElementalFloat(p, "trailVolume", Element.Mass, min: 1f, max: 2.5f);
            DisableElementalFloat(p, "trailVolume");
            // Mass 5 armours the wake through ForceShielded (Mass mode only), not these flags.
            Set(p, "massUpgradeShieldsTrail", false);
            Set(p, "turnUpgradeShieldsTrail", false);

            CopyReferenceFromVessel(p, "_onPrismSpawnedEventChannel", SquirrelPrefabPath,
                                    "prism spawn event channel");
        }

        void WireResources(ResourceSystem resources)
        {
            // ONE meter, index 0. Its reader (the Spread Wings energy cost) was retired when the
            // right trigger became a mode switch; it stays because meters are addressed by index
            // fleet-wide and the omni bloom's crystal effect reads index 0 (at a fixed scale, so
            // its value changes nothing).
            if (!resources) { Unwired("ResourceSystem", "component is null"); return; }
            var so = new SerializedObject(resources);
            var list = so.FindProperty("Resources");
            if (list == null) { Unwired("ResourceSystem.Resources", "property not found"); return; }

            list.arraySize = 1;
            var r = list.GetArrayElementAtIndex(0);
            SetChild(r, "Name", "Wing Energy");
            SetChild(r, "maxAmount", 1f);
            SetChild(r, "initialAmount", 1f);
            SetChild(r, "resourceGainRate", 0.00125f);
            so.ApplyModifiedPropertiesWithoutUndo();
            Note("Authored 1 resource meter: [0] Wing Energy");
        }

        void WireActionHandler(R_VesselActionHandler handler, ActionExecutorRegistry registry,
                               FoldActionSO fold, SpreadWingsActionSO spread)
        {
            if (!handler) { Unwired("R_VesselActionHandler", "component is null"); return; }
            Set(handler, "_executors", registry);

            var so = new SerializedObject(handler);
            var list = so.FindProperty("_inputEventShipActions");
            if (list == null) { Unwired("R_VesselActionHandler._inputEventShipActions", "not found"); return; }

            list.arraySize = 2;
            // The map asset is the design record and these two must agree with it:
            // Mass  = Spread Wings on RightStickAction (1)
            // Time  = Fold        on LeftStickAction  (2)
            BindInput(list.GetArrayElementAtIndex(0), InputEvents.RightStickAction, spread);
            BindInput(list.GetArrayElementAtIndex(1), InputEvents.LeftStickAction, fold);
            so.ApplyModifiedPropertiesWithoutUndo();
            Note("Bound RightStickAction → Mass/Dust mode, LeftStickAction → Fold");
        }

        void BindInput(SerializedProperty entry, InputEvents input, ScriptableObject action)
        {
            var ev = entry.FindPropertyRelative("InputEvent");
            var actions = entry.FindPropertyRelative("ShipActions");
            if (ev == null || actions == null) { Unwired("InputEventShipActionMapping", "shape changed"); return; }
            ev.intValue = (int)input;   // by VALUE — see the note in Set()
            actions.arraySize = 1;
            actions.GetArrayElementAtIndex(0).objectReferenceValue = action;
        }

        void WireHud(ButterflyHUDController hud, Transform hudInstance,
                     FoldActionExecutor fold, SpreadWingsActionExecutor spread)
        {
            Set(hud, "foldExecutor", fold);
            Set(hud, "spreadExecutor", spread);
            if (!hudInstance) return;
            var view = hudInstance.GetComponentInChildren<ButterflyHUDView>(true);
            if (view) { Set(hud, "view", view); Set(hud, "baseView", view); }
            else Unwired("ButterflyHUDView", "not found in the HUD variant instance");
        }

        // ─────────────────────────────────────────────────────── class asset & registration

        void BuildClassAsset(GameObject prefab)
        {
            var asset = CreateOrUpdate<SO_Vessel>(ClassPath, so =>
            {
                Set(so, "Class", (int)VesselClassType.Butterfly);
                Set(so, "Name", VesselName);
                Set(so, "Description",
                    "Slow, wide and quiet. The Butterfly paints broad surfaces through the " +
                    "hypersea while everyone else is fighting — and folds space when it wants to " +
                    "be somewhere else.");
            });
            if (asset) AddToClassLists(asset);
        }

        void AddToClassLists(SO_Vessel vessel)
        {
            foreach (var path in new[]
                     {
                         "Assets/_SO_Assets/Classes/SO_Classlist_All.asset",
                         "Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset",
                     })
            {
                var list = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!list) { Note("SKIP class list (absent): " + path); continue; }
                if (AppendToFirstObjectArray(list, vessel))
                {
                    EditorUtility.SetDirty(list);
                    FrogletToolChangeLedger.Record(ToolName, path);
                    Note("Added to " + Path.GetFileName(path));
                }
            }
        }

        void Register(GameObject prefab)
        {
            var container = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VesselContainerPath);
            if (container)
            {
                var so = new SerializedObject(container);
                var list = so.FindProperty("_shipPrefabs");
                // _shipPrefabs is Transform[], NOT GameObject[] — assigning the GameObject here
                // would silently store null and the vessel would never resolve at spawn
                // ("No Vessel Prefab found"). The container reads VesselStatus off the Transform.
                var entryValue = prefab ? prefab.transform : null;
                if (list != null && !ArrayContains(list, entryValue))
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = entryValue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(container);
                    FrogletToolChangeLedger.Record(ToolName, VesselContainerPath);
                    Note("Registered in Vessel Prefab Container.");
                }
                else if (list == null) Unwired("Vessel Prefab Container._shipPrefabs", "not found");
            }
            else Unwired("Vessel Prefab Container", VesselContainerPath);

            // Netcode: a client cannot replicate an unregistered NetworkObject. Nothing audits
            // container ↔ network-list sync, so this is the half that is usually forgotten.
            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (!netList) { Unwired("DefaultNetworkPrefabs", NetworkPrefabsPath); return; }
            var netSo = new SerializedObject(netList);
            var prefabs = netSo.FindProperty("List");
            if (prefabs == null) { Unwired("DefaultNetworkPrefabs.List", "not found"); return; }
            for (int i = 0; i < prefabs.arraySize; i++)
            {
                var p = prefabs.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                if (p != null && p.objectReferenceValue == prefab) return;   // already registered
            }
            prefabs.arraySize++;
            var entry = prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1);
            var prefabProp = entry.FindPropertyRelative("Prefab");
            if (prefabProp != null) prefabProp.objectReferenceValue = prefab;
            netSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(netList);
            FrogletToolChangeLedger.Record(ToolName, NetworkPrefabsPath);
            Note("Registered in DefaultNetworkPrefabs.");
        }

        /// <summary>
        /// Re-read both registrations FROM DISK, after the save, and report a miss.
        ///
        /// <para>This exists because a registration that did not take is invisible from inside the
        /// code that made it: <c>ApplyModifiedPropertiesWithoutUndo</c> returns nothing useful, the
        /// in-memory SerializedObject reports the value it was told, and the ledger records the
        /// path whether or not anything changed. The Netcode half went missing exactly that way —
        /// <c>Register</c> ran to completion, recorded <c>DefaultNetworkPrefabs.asset</c>, and the
        /// file on disk came out with the same 20 entries it went in with, so the vessel could
        /// never have replicated and nothing said so.</para>
        ///
        /// <para>Reading the asset back is the only claim worth making here, because it is the one
        /// the rest of the game will read.</para>
        /// </summary>
        void VerifyRegistrations(GameObject prefab)
        {
            var container = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VesselContainerPath);
            var list = container ? new SerializedObject(container).FindProperty("_shipPrefabs") : null;
            if (list == null || !ArrayContains(list, prefab.transform))
                Unwired("Vessel Prefab Container._shipPrefabs",
                        "the Butterfly is NOT in the asset on disk after saving");
            else Note("VERIFIED on disk: Vessel Prefab Container carries the Butterfly.");

            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            var prefabs = netList ? new SerializedObject(netList).FindProperty("List") : null;
            bool found = false;
            for (int i = 0; prefabs != null && i < prefabs.arraySize && !found; i++)
            {
                var p = prefabs.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                found = p != null && p.objectReferenceValue == prefab;
            }
            if (!found)
                Unwired("DefaultNetworkPrefabs.List",
                        "the Butterfly is NOT in the asset on disk after saving — it cannot " +
                        "replicate; add it by hand in the NetworkManager's prefab list");
            else Note("VERIFIED on disk: DefaultNetworkPrefabs carries the Butterfly.");
        }

        /// <summary>
        /// For every component this vessel shares with <paramref name="donorPrefabPath"/>, copy
        /// each serialized reference that is EMPTY here and points at a PROJECT ASSET there.
        ///
        /// <para>The fleet's cross-system wiring is almost entirely SOAP channels and config SOs —
        /// one asset per channel, referenced identically by every hull — and the platform's policy
        /// is to FAIL LOUD on a missing one rather than guard it (<c>CLAUDE.md</c>: "Do not add
        /// if-null guards on ScriptableEvent serialized fields"). That makes an unwired channel an
        /// immediate NullReferenceException rather than a quiet degradation, which is right; it
        /// also means a hand-written list of "the references a new vessel needs" is a list that
        /// costs a playtest every time somebody forgets a line. Six were missing here
        /// (<c>_onButtonPressed</c>, <c>_onButtonReleased</c>, <c>onAbilityExecuted</c>,
        /// <c>boostChanged</c>, <c>cellData</c>, <c>OnCellItemsUpdated</c>,
        /// <c>OnInitializePlayerCamera</c>) and each was invisible until something subscribed.</para>
        ///
        /// <para><b>ASSETS ONLY — never a Component or a GameObject.</b> A donor's reference to one
        /// of its OWN children is a pointer into the donor prefab; copying it would either dangle
        /// or, worse, make this vessel drive a part of the Squirrel. Those stay explicit
        /// (<c>AIPilot.actionExecutorRegistry</c>, the skimmers, the HUD view). A field the donor
        /// also leaves empty is left empty here, so this can only ever copy a decision somebody
        /// already made.</para>
        /// </summary>
        void AdoptSharedAssetReferences(GameObject root, string donorPrefabPath)
        {
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(donorPrefabPath);
            if (!donor) { Unwired("shared channels", "donor prefab missing: " + donorPrefabPath); return; }

            int adopted = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!component || component is Transform) continue;
                var donorComponent = donor.GetComponentInChildren(component.GetType(), true);
                if (!donorComponent) continue;

                var targetSo = new SerializedObject(component);
                var donorSo = new SerializedObject(donorComponent);
                bool dirty = false;

                var it = targetSo.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue) continue;
                    var from = donorSo.FindProperty(it.propertyPath);
                    var value = from?.objectReferenceValue;
                    // A ScriptableObject IS the shared-channel case; a Component or GameObject is
                    // the donor's own hierarchy and must never travel.
                    if (value is not ScriptableObject || !EditorUtility.IsPersistent(value)) continue;
                    it.objectReferenceValue = value;
                    dirty = true;
                    adopted++;
                    Note($"Adopted {component.GetType().Name}.{it.propertyPath} = {value.name}");
                }
                if (dirty) targetSo.ApplyModifiedPropertiesWithoutUndo();
            }
            Note($"Adopted {adopted} shared channel(s) from {Path.GetFileNameWithoutExtension(donorPrefabPath)}.");
        }

        // ─────────────────────────────────────────────────────────────── helpers

        /// <summary>
        /// Add <typeparamref name="T"/>, or take the one Unity already added as somebody else's
        /// <c>[RequireComponent]</c>. Two failures this closes, both of which otherwise present as
        /// a field that "could not be wired":
        /// <list type="bullet">
        /// <item>A bare <c>AddComponent</c> returns <b>null</b> — with only a console log — when
        /// T declares a <c>[RequireComponent]</c> naming an interface or an abstract class that is
        /// not already satisfied. Reporting the TYPE here is what makes that one line of console
        /// noise attributable.</item>
        /// <item>A bare <c>AddComponent</c> of something Unity already auto-added mints a SECOND
        /// copy, which nothing complains about and which duplicates every subscription that
        /// component makes.</item>
        /// </list>
        /// </summary>
        T Require<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var existing)) return existing;
            var added = go.AddComponent<T>();
            if (!added)
                Unwired(typeof(T).Name,
                        "AddComponent returned null — it declares a [RequireComponent] naming an " +
                        "interface or abstract type that nothing on this object satisfies yet");
            return added;
        }

        /// <summary>
        /// Remove every MonoBehaviour whose script no longer resolves, on this object and all of
        /// its descendants, and say how many there were.
        ///
        /// <para><b>Unity refuses to SAVE a prefab that contains one</b>, and the base HUD prefab
        /// contains one: <c>VesselHUDPrefab.prefab</c>'s root carries a MonoBehaviour pointing at
        /// guid <c>57dc27a3f7264d548b51007c0615f701</c>, which no asset in the project owns — a
        /// deleted <c>ShipHUDView</c>-era component whose serialized data (<c>hudType</c>,
        /// <c>resourceDisplays</c>, <c>psIconRoot</c>…) Unity has kept ever since, because it never
        /// prunes a modification it cannot resolve. Every hand-authored variant in the fleet
        /// (Squirrel, Serpent, Manta…) carries an <c>m_RemovedComponents</c> entry for it, which is
        /// what the editor writes when you use Remove Missing Script; this does the same thing from
        /// code so a generated variant is authored the way a hand-authored one is.</para>
        ///
        /// <para>It is deliberately a strip rather than a fix to the base prefab: repointing or
        /// deleting that component in <c>VesselHUDPrefab.prefab</c> would dangle the
        /// <c>m_RemovedComponents</c> and modification entries that seven shipped variants hold
        /// against its fileID, and that is an editor operation, not a YAML edit.</para>
        /// </summary>
        int StripMissingScripts(GameObject go)
        {
            int removed = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (removed > 0)
                Note($"Removed {removed} missing-script component(s) inherited from the base HUD prefab.");
            return removed;
        }

        T CreateOrUpdate<T>(string path, Action<T> configure = null) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = false;
            if (!asset)
            {
                asset = CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }
            configure?.Invoke(asset);
            EditorUtility.SetDirty(asset);
            FrogletToolChangeLedger.Record(ToolName, path);
            Note((created ? "Created " : "Updated ") + path);
            return asset;
        }

        /// <summary>
        /// Write a serialized field BY NAME, reporting when it is not there. A SerializedObject
        /// write to a missing property is a silent no-op — which is indistinguishable from
        /// success, and is exactly how a vessel ships half-wired.
        /// </summary>
        void Set(UnityEngine.Object target, string field, object value)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Unwired($"{target.GetType().Name}.{field}", "property not found"); return; }

            switch (value)
            {
                case bool b: p.boolValue = b; break;
                // An enum is written by VALUE, never by enumValueIndex — which is the position in
                // the enum's NAME LIST, not the member's number. The two agree only while an enum
                // is zero-based and contiguous, which is true of Element, InputEvents and
                // CombatHitClass and FALSE of VesselClassType, whose `Any = -1` shifts every index
                // down by one. Writing VesselClassType.Butterfly (13) as an index therefore stored
                // the 14th member, Scarab (12) — so the Butterfly prefab identified as a SCARAB,
                // the container could not resolve Butterfly at all, and the vessel was silently
                // absent from the Vessel Changer and the Spawn Matrix. Nothing reports it: the
                // write succeeds, the field holds a valid member of the right type, and the asset
                // looks correct in the inspector because the inspector shows what is stored.
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case UnityEngine.Object o: p.objectReferenceValue = o; break;
                // A null UnityEngine.Object does NOT match `case UnityEngine.Object` — a type
                // pattern never matches null — so without this branch a reference the caller could
                // not resolve is reported as "unsupported value type", which names the wrong
                // problem and sends the reader to this switch instead of to the null source.
                case null when p.propertyType == SerializedPropertyType.ObjectReference:
                    Unwired($"{target.GetType().Name}.{field}", "the value to assign was null");
                    return;
                default: Unwired($"{target.GetType().Name}.{field}", "unsupported value type"); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void SetChild(SerializedProperty parent, string child, object value)
        {
            var p = parent.FindPropertyRelative(child);
            if (p == null) { Unwired(parent.propertyPath + "." + child, "not found"); return; }
            switch (value)
            {
                case string s: p.stringValue = s; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
            }
        }

        void SetArray(UnityEngine.Object target, string field, IReadOnlyList<UnityEngine.Object> values)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) { Unwired($"{target.GetType().Name}.{field}", "not an array"); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Author an ElementalFloat: the element it reads and its min/max band.</summary>
        void SetElementalFloat(UnityEngine.Object target, string field, Element element,
                               float min, float max)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Unwired($"{target.GetType().Name}.{field}", "not found"); return; }
            SetChild(p, "element", (int)element);
            SetChild(p, "Min", min);
            SetChild(p, "Max", max);
            // Value is what EvaluateLive returns when Enabled is false or the vessel has no
            // ResourceSystem yet. Seeding it to Min means the disabled path is the RESTING value
            // rather than whatever the C# initializer happened to be — the difference between a
            // degraded read and a wrong one.
            SetChild(p, "Value", min);
            var enabled = p.FindPropertyRelative("Enabled");
            if (enabled != null) enabled.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void DisableElementalFloat(UnityEngine.Object target, string field)
        {
            if (!target) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            var enabled = p?.FindPropertyRelative("Enabled");
            if (enabled == null) { Unwired($"{target.GetType().Name}.{field}.Enabled", "not found"); return; }
            enabled.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void CopyReference(UnityEngine.Object target, string field,
                           UnityEngine.Object value, string label)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            if (!value) { Unwired($"{target.GetType().Name}.{field}", label + " not found"); return; }
            Set(target, field, value);
        }

        /// <summary>Borrow a reference from another vessel's prefab, so shared plumbing (the prism
        /// spawn channel, the game data asset) is the SAME object rather than a lookalike.</summary>
        void CopyReferenceFromVessel(UnityEngine.Object target, string field,
                                     string donorPrefabPath, string label)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(donorPrefabPath);
            var donorComponent = donor ? donor.GetComponent(target.GetType()) : null;
            if (!donorComponent) { Unwired($"{target.GetType().Name}.{field}", label + " donor missing"); return; }
            var donorSo = new SerializedObject(donorComponent);
            var p = donorSo.FindProperty(field);
            if (p == null || p.objectReferenceValue == null)
            { Unwired($"{target.GetType().Name}.{field}", label + " absent on donor"); return; }
            Set(target, field, p.objectReferenceValue);
        }

        void CopyArrayFromReferenceContainer(UnityEngine.Object target, string donorName,
                                             IEnumerable<string> fields)
        {
            if (!target) { Unwired(donorName, "target container is null"); return; }
            var donor = FindFirstAssetNamed(donorName);
            if (!donor) { Unwired(donorName, "donor container not found"); return; }
            var donorSo = new SerializedObject(donor);
            var targetSo = new SerializedObject(target);
            foreach (var field in fields)
            {
                var from = donorSo.FindProperty(field);
                var to = targetSo.FindProperty(field);
                if (from == null || to == null || !from.isArray) { Unwired(field, "absent on donor or target"); continue; }
                to.arraySize = from.arraySize;
                for (int i = 0; i < from.arraySize; i++)
                    to.GetArrayElementAtIndex(i).objectReferenceValue =
                        from.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            targetSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Material[] BorrowVesselMaterials()
        {
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(SquirrelPrefabPath);
            var renderer = donor ? donor.GetComponentInChildren<MeshRenderer>(true) : null;
            if (renderer && renderer.sharedMaterials.Length >= 2) return renderer.sharedMaterials;
            var skinned = donor ? donor.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (skinned && skinned.sharedMaterials.Length >= 1)
                return new[] { skinned.sharedMaterials[0], skinned.sharedMaterials[0] };
            Unwired("hull materials", "no donor renderer with two slots — assign them by hand");
            return new Material[2];
        }

        static bool ArrayContains(SerializedProperty array, UnityEngine.Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return true;
            return false;
        }

        bool AppendToFirstObjectArray(ScriptableObject list, UnityEngine.Object value)
        {
            var so = new SerializedObject(list);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (!it.isArray || it.propertyType == SerializedPropertyType.String) continue;
                if (it.arraySize > 0 &&
                    it.GetArrayElementAtIndex(0).propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (ArrayContains(it, value)) return false;
                it.arraySize++;
                it.GetArrayElementAtIndex(it.arraySize - 1).objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
            Unwired(list.name, "no object array to append to");
            return false;
        }

        static UnityEngine.Object FindFirstAssetNamed(string name)
        {
            var guids = AssetDatabase.FindAssets($"\"{name}\"");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset) return asset;
            }
            return null;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        void Note(string message) => _log.Add(message);

        void Unwired(string what, string why)
        {
            _unwired.Add(what);
            _log.Add($"UNWIRED  {what} — {why}");
        }
    }
}
#endif
