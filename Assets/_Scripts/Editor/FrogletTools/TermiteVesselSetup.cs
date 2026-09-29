#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;   // ClientNetworkTransform, NetcodeHooks — NOT Unity.Netcode.Components
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Builds the <b>Termite</b> — the commander queen — prefab, its HUD variant, its vessel
    /// container and its registrations. Design record: <c>R_VesselActions/TERMITE.md</c>.
    ///
    /// <para><b>Why an editor tool.</b> A vessel prefab's root carries a <c>NetworkObject</c>
    /// whose <c>GlobalObjectIdHash</c> Unity computes (a hand-authored hash that collides breaks
    /// scene synchronization for every later joiner — <c>Docs/PartySystem/BUGS.md</c> B16), and a
    /// HUD variant carries modification blocks Unity owns. Everything that does NOT need Unity was
    /// authored headlessly and is only LOADED here: the three card slots and the pheromone clock
    /// (<c>_SO_Assets/VesselActions/Termite/</c>), the camera settings, the autothysis blast
    /// (<c>AOETermiteAutothysis.prefab</c> + its container) and the ability map. A missing one is
    /// reported as UNWIRED rather than re-created, so this tool never becomes their second
    /// author.</para>
    ///
    /// <para><b>It REPLACES the legacy prefab in place.</b> <c>Termite.prefab</c> was the 2024
    /// prototype — a Manta-bodied hull with a <c>CommandVesselTransformer</c>, two missing scripts
    /// and the old drone actions — registered in <c>DefaultNetworkPrefabs</c> and in nothing
    /// else. Saving over the same path keeps its guid, so that registration stays valid; the
    /// contents are built from scratch, exactly as the Butterfly's are, so every authored number
    /// on this vessel is visible in THIS FILE.</para>
    ///
    /// <para>Every write goes through <see cref="Set"/>, which REPORTS a missing property instead
    /// of silently doing nothing. Run it, read the report, then <b>Validate &amp; Push</b>.</para>
    /// </summary>
    public class TermiteVesselSetup : EditorWindow
    {
        const string ToolName = "Termite Vessel Setup";

        const string VesselName = "Termite";
        const string PrefabPath = "Assets/_Prefabs/Spacevessels/Termite.prefab";
        const string ActionDir = "Assets/_SO_Assets/VesselActions/Termite";
        const string EffectDir = "Assets/_SO_Assets/Effects";
        const string CameraPath = "Assets/_SO_Assets/Camera/TermiteCameraSettingsSO.asset";
        const string ClassPath = "Assets/_SO_Assets/Classes/SO_Class_Termite.asset";
        const string VesselContainerPath = "Assets/_SO_Assets/Vessel Prefab Container.asset";
        const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        const string AutothysisPrefabPath = "Assets/_Prefabs/Projectile/AOETermiteAutothysis.prefab";
        const string TeamCrystalPrefabPath = "Assets/_Prefabs/Environment/TeamCrystal.prefab";
        const string PrismSpawnChannelPath = "Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset";
        const string TailPrefabPath = "Assets/_Prefabs/Spacevessels/Components/VesselTail.prefab";
        const string CardArtDir = "Assets/_Graphics/VesselButtons";

        // The fleet's reference two-thumb hull: shared channels, the prism pool, the baseline
        // prism effects and the game-data reference are mirrored from it.
        const string SquirrelPrefabPath = "Assets/_Prefabs/Spacevessels/Squirrel.prefab";
        const string HudBasePrefabPath = "Assets/_Prefabs/UI Elements/VesselHUD/VesselHUDPrefab.prefab";
        const string HudVariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/TermiteHUDVariant.prefab";

        /// <summary>|followOffset.z| of TermiteCameraSettingsSO — sizes the tail (VESSEL_TAIL_AND_JETS.md).</summary>
        const float CameraDistance = 110f;

        readonly List<string> _log = new();
        readonly List<string> _unwired = new();
        Vector2 _scroll;

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            ToolScriptPaths = new[]
                { "Assets/_Scripts/Editor/FrogletTools/TermiteVesselSetup.cs" },
            CommitType = "feat",
            CommitScope = "vessel",
            CommitSubject = _ => "feat(vessel): the TERMITE queen — prefab, HUD variant and registrations",
        };

        [MenuItem("FrogletTools/Vessels/Create Termite Vessel")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Builds the Termite commander-queen prefab (replacing the 2024 prototype in " +
                          "place), its HUD variant, vessel container and registrations. Idempotent.")]
        public static void Open() =>
            GetWindow<TermiteVesselSetup>(true, "Termite Vessel Setup").minSize = new Vector2(520, 460);

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Termite", "The commander queen", FrogletEditorPalette.Violet);

            EditorGUILayout.HelpBox(
                "Builds every Termite asset that needs Unity to author it. Safe to re-run: the " +
                "prefab and HUD variant are rebuilt in place, not duplicated.\n\n" +
                "Anything listed as UNWIRED is a real gap, not a warning.", MessageType.Info);

            if (GUILayout.Button("Build the Termite", GUILayout.Height(34)))
                Build();

            if (_log.Count > 0)
            {
                EditorGUILayout.Space();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));
                foreach (var line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();

                if (_unwired.Count > 0)
                    EditorGUILayout.HelpBox(
                        $"{_unwired.Count} field(s) could not be wired — see the report.", MessageType.Error);
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
                var charge = Load<TermiteCardActionSO>($"{ActionDir}/TermiteChargeCards.asset");
                var mass = Load<TermiteCardActionSO>($"{ActionDir}/TermiteMassCards.asset");
                var space = Load<TermiteCardActionSO>($"{ActionDir}/TermiteSpaceCards.asset");
                var pheromone = Load<TermitePheromoneActionSO>($"{ActionDir}/TermitePheromoneAction.asset");
                var camera = Load<CameraSettingsSO>(CameraPath);

                var container = BuildVesselContainer();
                var hudVariant = BuildHudVariant();
                var prefab = BuildPrefab(charge, mass, space, pheromone, container, camera, hudVariant);
                if (prefab) Register(prefab);

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

        T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) Unwired(Path.GetFileNameWithoutExtension(path), "missing — authored headlessly, restore it from git");
            return asset;
        }

        // ─────────────────────────────────────────────────────── container

        /// <summary>
        /// The vessel container mirrors the Squirrel's baseline prism trio (damage, haptics,
        /// danger-prism debuff) — a prism should read the same whichever hull hits it — and its
        /// crystal haptics. The queen has no crystal ability of her own, so her OMNI list is just
        /// the haptics: collecting a crystal is still the element economy doing its job.
        /// </summary>
        VesselImpactorDataContainerSO BuildVesselContainer() =>
            CreateOrUpdate<VesselImpactorDataContainerSO>(
                $"{EffectDir}/Effect Containers/VesselContainers/TermiteImpactorDataContainer.asset", so =>
                {
                    CopyArrayFromReferenceContainer(so, "SquirrelImpactorDataContainer",
                        new[] { "vesselPrismEffects" });
                    var haptics = FindFirstAssetNamed("VesselHapticsByCrystalEffect");
                    SetArray(so, "vesselCrystalEffects",
                        haptics ? new UnityEngine.Object[] { haptics } : Array.Empty<UnityEngine.Object>());
                });

        // ─────────────────────────────────────────────────────────────── HUD

        GameObject BuildHudVariant()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudBasePrefabPath);
            if (!basePrefab) { Unwired("HUD base prefab", HudBasePrefabPath); return null; }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(HudVariantPath))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = "TermiteHUDVariant";
                StripMissingScripts(instance);
                // SaveAsPrefabAsset on a prefab INSTANCE produces a VARIANT (propagation kept).
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, HudVariantPath);
                DestroyImmediate(instance);
                if (!variant) { Unwired("HUD variant", "SaveAsPrefabAsset refused " + HudVariantPath); return null; }
                FrogletToolChangeLedger.Record(ToolName, HudVariantPath);
                Note("Created HUD variant " + HudVariantPath);
            }

            AuthorHudRow();
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudVariantPath);
        }

        /// <summary>
        /// The deck on the HUD: the Termite view replaces the base view, the fleet-standard four-band
        /// row is placed and bound (<see cref="VesselAbilityRowWirer.WireRow"/>, the same code the
        /// menu item runs), each card slot's icon starts on its A card's FRONT art, the Time slot
        /// gets the pheromone drop and a linear pheromone gauge bound both as the view's field and
        /// as the Time icon's lockup gauge (the view WRITES through one, the lockup ADOPTS through
        /// the other — binding only the field leaves the lockup switching off a driven meter).
        /// </summary>
        void AuthorHudRow()
        {
            var root = PrefabUtility.LoadPrefabContents(HudVariantPath);
            try
            {
                StripMissingScripts(root);
                var view = root.GetComponentInChildren<TermiteHUDView>(true);
                if (!view)
                {
                    var legacy = root.GetComponentInChildren<VesselHUDView>(true);
                    var host = legacy ? legacy.gameObject : root;
                    if (legacy) DestroyImmediate(legacy, true);
                    view = host.AddComponent<TermiteHUDView>();
                    Note("Added TermiteHUDView to the HUD variant.");
                }

                var icons = VesselAbilityRowWirer.WireRow(view);   // charge, mass, space, time

                Sprite Art(string file)
                {
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardArtDir}/{file}.png");
                    if (!s) Unwired(file, "card art sprite missing");
                    return s;
                }

                var fronts = new (TermiteCard card, string file)[]
                {
                    (TermiteCard.Autothysis, "TermiteCard_Front_Autothysis"),
                    (TermiteCard.TeamCrystal, "TermiteCard_Front_TeamCrystal"),
                    (TermiteCard.QueenDrones, "TermiteCard_Front_QueenDrones"),
                    (TermiteCard.MoundDrones, "TermiteCard_Front_MoundDrones"),
                    (TermiteCard.Teleport, "TermiteCard_Front_Teleport"),
                    (TermiteCard.NewMound, "TermiteCard_Front_NewMound"),
                };
                var viewSo = new SerializedObject(view);
                var art = viewSo.FindProperty("cardArt");
                if (art == null) Unwired("TermiteHUDView.cardArt", "not found");
                else
                {
                    art.arraySize = fronts.Length;
                    for (int i = 0; i < fronts.Length; i++)
                    {
                        var e = art.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("card").intValue = (int)fronts[i].card;
                        e.FindPropertyRelative("front").objectReferenceValue = Art(fronts[i].file);
                    }
                }
                viewSo.ApplyModifiedPropertiesWithoutUndo();

                // Starting faces: each slot's A card.
                string[] startFaces = { "TermiteCard_Front_Autothysis", "TermiteCard_Front_QueenDrones",
                                        "TermiteCard_Front_Teleport", "TermiteCard_Pheromone" };
                for (int i = 0; i < icons.Length && i < startFaces.Length; i++)
                {
                    if (!icons[i]) { Unwired($"ability icon {i}", "WireRow left it unbound"); continue; }
                    icons[i].sprite = Art(startFaces[i]);
                    icons[i].preserveAspect = true;
                }

                // The pheromone gauge: a horizontal fill on the Time plate.
                var timeIcon = icons.Length > 3 ? icons[3] : null;
                if (timeIcon)
                {
                    var host = timeIcon.transform.parent;
                    var gaugeT = host.Find("PheromoneGauge");
                    var gauge = gaugeT ? gaugeT.GetComponent<Image>() : null;
                    if (!gauge)
                    {
                        var go = new GameObject("PheromoneGauge", typeof(RectTransform), typeof(Image));
                        go.transform.SetParent(host, false);
                        go.transform.SetSiblingIndex(0);   // behind the icon
                        var rt = (RectTransform)go.transform;
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                        rt.offsetMin = rt.offsetMax = Vector2.zero;
                        gauge = go.GetComponent<Image>();
                    }
                    gauge.type = Image.Type.Filled;
                    gauge.fillMethod = Image.FillMethod.Vertical;
                    gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
                    gauge.raycastTarget = false;
                    gauge.fillAmount = 0.5f;

                    Set(view, "pheromoneGauge", gauge);
                    var rowSo = new SerializedObject(view);
                    var row = rowSo.FindProperty("abilityIcons");
                    for (int i = 0; row != null && i < row.arraySize; i++)
                    {
                        var e = row.GetArrayElementAtIndex(i);
                        if (e.FindPropertyRelative("icon").objectReferenceValue == timeIcon)
                            e.FindPropertyRelative("gauge").objectReferenceValue = gauge;
                    }
                    rowSo.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, HudVariantPath);
                FrogletToolChangeLedger.Record(ToolName, HudVariantPath);
                Note("Authored the HUD row: three card faces, the pheromone drop and its gauge.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ─────────────────────────────────────────────────────────────── prefab

        GameObject BuildPrefab(TermiteCardActionSO charge, TermiteCardActionSO mass, TermiteCardActionSO space,
                               TermitePheromoneActionSO pheromone, VesselImpactorDataContainerSO container,
                               CameraSettingsSO camera, GameObject hudVariant)
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

                // ---- the hull: the procedural queen ----
                var hullGo = new GameObject("Hull");
                hullGo.transform.SetParent(root.transform, false);
                hullGo.AddComponent<MeshFilter>();
                var hullRenderer = hullGo.AddComponent<MeshRenderer>();
                // TWO slots: submesh 0 the chitin, submesh 1 the membrane + wings, which the
                // fleet paints in the DOMAIN colour (ShipHelper.ApplyShipMaterial).
                hullRenderer.sharedMaterials = BorrowVesselMaterials();
                hullGo.AddComponent<TermiteHullBuilder>();

                // A capsule along the body: the head at +z, the abdomen trailing to about -16.
                var hullCollider = hullGo.AddComponent<CapsuleCollider>();
                hullCollider.direction = 2;
                hullCollider.radius = 3.2f;
                hullCollider.height = 20f;
                hullCollider.center = new Vector3(0f, 0.4f, -6f);
                var impactCollider = hullGo.AddComponent<ImpactCollider>();

                // ---- core components: ORDER IS LOAD-BEARING (see ButterflyVesselSetup) ----
                var animation = Require<TermiteAnimation>(root);          // is a VesselAnimation
                var status = Require<VesselStatus>(root);                 // is an IVesselStatus
                var controller = Require<VesselController>(root);         // is an IVessel
                var resources = Require<ResourceSystem>(root);
                var impactor = Require<VesselImpactor>(root);
                var cameraCustomizer = Require<VesselCameraCustomizer>(root);
                var actionHandler = Require<R_VesselActionHandler>(root);
                var customization = Require<VesselCustomization>(root);
                Require<R_ShipElementStatsHandler>(root);

                // The COMMANDER transformer: derives from the base, so she is a TWO-thumb hull and
                // never sets IsSingleStickControls (the one-thumb desktop scheme makes the mouse a
                // stick — the device a commander points with).
                var transformer = Require<TermiteCommandTransformer>(root);
                var commander = Require<TermiteCommander>(root);
                var prisms = Require<VesselPrismController>(root);
                Require<AIPilot>(root);
                Require<ElementalBarsController>(root);
                var hud = Require<TermiteHUDController>(root);
                Require<VesselTailAndJets>(root);

                if (!animation || !status || !controller || !resources || !impactor ||
                    !transformer || !commander || !prisms || !actionHandler || !hud)
                {
                    Note("ABORTED: a core component could not be added — see UNWIRED above. No prefab was written.");
                    return null;
                }

                // ---- the deck ----
                var actionsGo = new GameObject("VesselActions");
                actionsGo.transform.SetParent(root.transform, false);
                var registry = actionsGo.AddComponent<ActionExecutorRegistry>();
                var deckGo = new GameObject("Deck");
                deckGo.transform.SetParent(actionsGo.transform, false);
                var deck = deckGo.AddComponent<TermiteDeckExecutor>();
                Set(deck, "pheromoneConfig", pheromone);
                Set(deck, "prismSpawnChannel", AssetDatabase.LoadAssetAtPath<ScriptableObject>(PrismSpawnChannelPath));
                Set(deck, "teamCrystalPrefab",
                    AssetDatabase.LoadAssetAtPath<GameObject>(TeamCrystalPrefabPath)?.GetComponent<Crystal>());
                var aoe = AssetDatabase.LoadAssetAtPath<GameObject>(AutothysisPrefabPath);
                SetArray(deck, "autothysisPrefabs",
                    aoe && aoe.GetComponent<AOEExplosion>()
                        ? new UnityEngine.Object[] { aoe.GetComponent<AOEExplosion>() }
                        : Array.Empty<UnityEngine.Object>());
                if (!aoe) Unwired("AOETermiteAutothysis", AutothysisPrefabPath);
                CopyReference(deck, "OnMiniGameTurnEnd", FindFirstAssetNamed("EventOnMiniGameTurnEnd"),
                              "turn-end channel");
                SetArray(registry, "_executors", new UnityEngine.Object[] { deck });

                // ---- the tail: one beacon behind the camera, sized to its distance ----
                var tailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TailPrefabPath);
                if (tailPrefab)
                {
                    var tail = (GameObject)PrefabUtility.InstantiatePrefab(tailPrefab, root.transform);
                    tail.transform.localPosition = new Vector3(0f, 0f, -1.05f * CameraDistance);
                    var marker = tail.GetComponentInChildren<VesselTail>(true);
                    if (marker) Set(marker, "widthScale", CameraDistance / 20f);
                    else Unwired("VesselTail marker", "not on " + TailPrefabPath);
                }
                else Unwired("VesselTail prefab", TailPrefabPath);

                // ---- HUD ----
                Transform hudContainer = null;
                if (hudVariant)
                {
                    var hudHost = new GameObject("ShipHUDContainer");
                    hudHost.transform.SetParent(root.transform, false);
                    var canvas = hudHost.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    hudHost.AddComponent<CanvasScaler>();
                    hudHost.AddComponent<GraphicRaycaster>();
                    var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudVariant, hudHost.transform);
                    hudContainer = hudInstance.transform;
                }

                // ---- wiring ----
                AdoptSharedAssetReferences(root, SquirrelPrefabPath);
                Set(root.GetComponent<AIPilot>(), "actionExecutorRegistry", registry);

                WireStatus(status, controller, hud);
                CopyReference(controller, "gameData", FindFirstAssetNamed("Runtime GameData"), "GameDataSO");
                Set(cameraCustomizer, "settings", camera);
                Set(impactor, "vesselImpactorDataContainerSO", container);
                SetArray(customization, "_shipGeometries", new UnityEngine.Object[] { hullGo });
                Set(impactCollider, "impactorObject", impactor);
                Set(commander, "transformer", transformer);
                WireTransformer(transformer);
                WirePrisms(prisms);
                WireResources(resources);
                WireActionHandler(actionHandler, registry, charge, mass, space);
                WireHud(hud, hudContainer, deck);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                FrogletToolChangeLedger.Record(ToolName, PrefabPath);
                Note("Wrote " + PrefabPath + " (the 2024 prototype is replaced in place; guid kept)");
                return saved;
            }
            finally { DestroyImmediate(root); }
        }

        void WireStatus(VesselStatus status, VesselController controller, MonoBehaviour hud)
        {
            Set(status, "vesselType", (int)VesselClassType.Termite);
            Set(status, "_name", VesselName);
            Set(status, "_shipInstance", controller);
            Set(status, "vesselHUDController", hud);
            // NO skimmer, by design: a commander does not skim, and none of her four abilities
            // reads skim state. VesselController.Initialize tolerates both references empty.
            Clear(status, "_nearFieldSkimmer");
            Clear(status, "_farFieldSkimmer");
        }

        void WireTransformer(VesselTransformer t)
        {
            // The STICK model is what an AI (and the menu's autopilot) flies: a stately queen,
            // under the fleet's cruise. The COMMANDED flight's numbers live on
            // TermiteCommandTransformer's own fields (70 u/s, TIME -> 112).
            Set(t, "DefaultThrottleScaler", 70f);
            Set(t, "DefaultMinimumSpeed", 10f);
            Set(t, "PitchScaler", 70f);
            Set(t, "YawScaler", 70f);
            Set(t, "RollScaler", 80f);
        }

        void WirePrisms(VesselPrismController p)
        {
            // The pool: Squirrel's (0 = Dolphin's by default, and nothing says so).
            Set(p, "prismType", (int)PrismType.Squirrel);
            // A narrow pheromone trail — the queen's mass is her MOUNDS, not her wake.
            Set(p, "BaseScale", new Vector3(2.2f, 0.8f, 5f));
            Set(p, "minBlockScale", 1f);
            Set(p, "maxBlockScale", 1f);
            Set(p, "initialWavelength", 10f);
            Set(p, "minWavelength", 6f);
            Set(p, "Gap", 0f);
            // No skimmer to clear, so no clearance delay to wait on.
            Set(p, "waitTillOutsideSkimmer", false);
            // OFF: MASS is the drone count on this hull (TermiteMassCards.power); a second Mass
            // dial on the trail would be the double-dip the convention forbids.
            SetElementalFloat(p, "trailVolume", Element.Mass, min: 1f, max: 2f);
            DisableElementalFloat(p, "trailVolume");
            // Renamed from massUpgradeShieldsTrail (FormerlySerializedAs keeps old prefabs).
            Set(p, "driftShieldsTrail", false);
            Set(p, "turnUpgradeShieldsTrail", false);
            CopyReferenceFromVessel(p, "_onPrismSpawnedEventChannel", SquirrelPrefabPath,
                                    "prism spawn event channel");
        }

        void WireResources(ResourceSystem resources)
        {
            // No meters: the deck's currency is pheromone, owned by the deck executor. The list is
            // left empty rather than inheriting a meter nothing reads.
            if (!resources) { Unwired("ResourceSystem", "component is null"); return; }
            var so = new SerializedObject(resources);
            var list = so.FindProperty("Resources");
            if (list == null) { Unwired("ResourceSystem.Resources", "property not found"); return; }
            list.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void WireActionHandler(R_VesselActionHandler handler, ActionExecutorRegistry registry,
                               TermiteCardActionSO charge, TermiteCardActionSO mass, TermiteCardActionSO space)
        {
            if (!handler) { Unwired("R_VesselActionHandler", "component is null"); return; }
            Set(handler, "_executors", registry);

            var so = new SerializedObject(handler);
            var list = so.FindProperty("_inputEventShipActions");
            if (list == null) { Unwired("R_VesselActionHandler._inputEventShipActions", "not found"); return; }

            // Must agree with Resources/ElementalAbilityMaps/Termite.asset:
            // Charge = RightStickAction (1), Mass = LeftStickAction (2), Space = Button1Action (6).
            list.arraySize = 3;
            BindInput(list.GetArrayElementAtIndex(0), InputEvents.RightStickAction, charge);
            BindInput(list.GetArrayElementAtIndex(1), InputEvents.LeftStickAction, mass);
            BindInput(list.GetArrayElementAtIndex(2), InputEvents.Button1Action, space);
            so.ApplyModifiedPropertiesWithoutUndo();
            Note("Bound RT → Charge cards, LT → Mass cards, Button1 → Space cards");
        }

        void BindInput(SerializedProperty entry, InputEvents input, ScriptableObject action)
        {
            var ev = entry.FindPropertyRelative("InputEvent");
            var actions = entry.FindPropertyRelative("ShipActions");
            if (ev == null || actions == null) { Unwired("InputEventShipActionMapping", "shape changed"); return; }
            if (!action) { Unwired("card slot for " + input, "asset missing"); return; }
            ev.intValue = (int)input;   // by VALUE, never enumValueIndex
            actions.arraySize = 1;
            actions.GetArrayElementAtIndex(0).objectReferenceValue = action;
        }

        void WireHud(TermiteHUDController hud, Transform hudInstance, TermiteDeckExecutor deck)
        {
            Set(hud, "deck", deck);
            if (!hudInstance) return;
            var view = hudInstance.GetComponentInChildren<TermiteHUDView>(true);
            if (view) { Set(hud, "view", view); Set(hud, "baseView", view); }
            else Unwired("TermiteHUDView", "not found in the HUD variant instance");
        }

        // ─────────────────────────────────────────────────────── registration

        void Register(GameObject prefab)
        {
            var container = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VesselContainerPath);
            if (container)
            {
                var so = new SerializedObject(container);
                var list = so.FindProperty("_shipPrefabs");
                // _shipPrefabs is Transform[] — a GameObject would silently store null.
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

            // The legacy prefab was already in DefaultNetworkPrefabs; saving over its path kept
            // the guid, so this normally finds it present. Still checked, never assumed.
            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (!netList) { Unwired("DefaultNetworkPrefabs", NetworkPrefabsPath); return; }
            var netSo = new SerializedObject(netList);
            var prefabs = netSo.FindProperty("List");
            if (prefabs == null) { Unwired("DefaultNetworkPrefabs.List", "not found"); return; }
            for (int i = 0; i < prefabs.arraySize; i++)
            {
                var p = prefabs.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                if (p != null && p.objectReferenceValue == prefab) return;
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
                        "the Termite is NOT in the asset on disk after saving");
            else Note("VERIFIED on disk: Vessel Prefab Container carries the Termite.");

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
                        "the Termite is NOT in the asset on disk after saving — it cannot " +
                        "replicate; add it by hand in the NetworkManager's prefab list");
            else Note("VERIFIED on disk: DefaultNetworkPrefabs carries the Termite.");
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
                // Exact type first, then up the chain: the queen's components are often
                // SUBCLASSES of what the donor carries (TermiteCommandTransformer is a
                // VesselTransformer, TermiteAnimation a VesselAnimation), and the channels live on
                // the base — an exact-type-only match would adopt none of them.
                Component donorComponent = null;
                for (var t = component.GetType();
                     !donorComponent && t != null && t != typeof(MonoBehaviour) && t != typeof(Component);
                     t = t.BaseType)
                    donorComponent = donor.GetComponentInChildren(t, true);
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

        /// <summary>
        /// Deliberately EMPTY an object reference. <see cref="Set"/> reports a null value as
        /// UNWIRED because a null there almost always means a lookup failed; a reference this
        /// vessel is designed NOT to have is a different statement and must not read as a gap.
        /// </summary>
        void Clear(UnityEngine.Object target, string field)
        {
            if (!target) { Unwired(field, "target is null"); return; }
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Unwired($"{target.GetType().Name}.{field}", "property not found"); return; }
            if (p.propertyType != SerializedPropertyType.ObjectReference)
            { Unwired($"{target.GetType().Name}.{field}", "not an object reference"); return; }
            p.objectReferenceValue = null;
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
