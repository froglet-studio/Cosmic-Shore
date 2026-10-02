using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Wander</b> toy: leave the cell and go wandering, <b>with an Ark</b> or <b>without
    /// one</b>. Fly it and two stations bloom out ahead; the Toy Box window shows the same two as
    /// cards. They are the two runs that used to be two separate toys:
    ///
    /// <list type="bullet">
    /// <item><b>Without Ark</b> (<see cref="WanderwayRun"/>) - the cell reverts to its bare
    /// canvas, a belt of little worlds streams ahead of your flight path, and your trail becomes a
    /// rolling tether with the way home riding its far end.</item>
    /// <item><b>With Ark</b> (<see cref="ArkwayRun"/>) - a corridor of whole cells opens ahead and
    /// an <see cref="Ark"/> in your domain sails it at its own pace, with you sworn to its side.</item>
    /// </list>
    ///
    /// <para><b>A pass through the toy ENDS a live wander</b>, exactly as it did on either old toy:
    /// with a run going, the toy is a way home rather than a menu, so the pass is routed to the run
    /// instead of opening the matrix. The run's own return station and the overview button end it
    /// too. A pass while the Ark's voyage is still building behind the load veil is ignored - the
    /// player cannot see, and a toggle there ended a voyage they never saw.</para>
    ///
    /// <para><b>One wander at a time.</b> Both runs hand the host cell its bare canvas, raise the
    /// load veil and own the local trail, so two at once would fight over all three. While one is
    /// live its own card becomes the way home and the OTHER card is offered but not startable -
    /// still listed (a surface may decline to act, never to OFFER), and honest about why.</para>
    ///
    /// <para>The emblem is both halves at once: a miniature Ark as the core (the ship you would
    /// escort, in your live domain) ringed by three real microscenes (the worlds you would find).
    /// Its orbit speed carries the run state - stopped / running but dormant / under way.</para>
    /// </summary>
    public sealed class WanderToy : MatrixToy
    {
        /// <summary>The microscene recipes the emblem's satellites show: a gate run, a tunnel, a torus knot.</summary>
        static readonly int[] EmblemRecipes = { 0, 2, 30 };

        /// <summary>The recipe the WITHOUT ARK station and card model: an archway you fly under.</summary>
        const int StationRecipe = 16;

        /// <summary>Budget for a station/card miniature (a few hundred points read fine at station size).</summary>
        const int StationPointBudget = 160;

        // Orbit rates that ARE the run state (motion is the identity channel that survives distance).
        const float OrbitStopped = 0f;
        const float OrbitDormant = 3f;
        const float OrbitFlowing = 18f;

        /// <summary>The label the WITH ARK card and station wear.</summary>
        public const string WithArkLabel = "With Ark";

        /// <summary>The label the WITHOUT ARK card and station wear.</summary>
        public const string WithoutArkLabel = "Without Ark";

        /// <summary>The two choices, in the order both surfaces list them.</summary>
        enum Choice { WithArk = 0, WithoutArk = 1 }

        WanderToyDefinitionSO _def;
        ConveyorConfig _wanderCfg;
        ArkwayConfig _arkCfg;

        // Without Ark: the belt and the run. The belt's stock is built ONCE - a later wander
        // resumes it, never re-primes, which would mint a second pool.
        MicrosceneConveyor _belt;
        WanderwayRun _wanderRun;
        bool _beltPrimed;

        // With Ark: the corridor of cells and the voyage.
        CellConveyor _corridor;
        ArkwayRun _arkRun;

        // Station/card models, built once and shared (the card preview borrows the mesh, never owns it).
        CellMiniatureBuilder.Miniature _sceneModel;
        CellMiniatureBuilder.Miniature _arkModel;
        Domains _arkModelDomain;
        readonly List<Mesh> _retiredMeshes = new();

        public void Configure(WanderToyDefinitionSO definition)
        {
            _def = definition;
            _wanderCfg = definition ? definition.BuildWithoutArkConfig() : new ConveyorConfig();
            _arkCfg = definition ? definition.BuildWithArkConfig() : new ArkwayConfig();
        }

        /// <summary>
        /// Whether this build offers the WITH ARK choice. The Android strip does not: a voyage
        /// stands a corridor of three satellite cells, which is the heaviest thing the toybox can
        /// build (<see cref="PerfStrip.LightToysOnly"/>). Gated at the one declaration, so the
        /// station and the Toy Box card drop it together and the two surfaces still agree.
        /// </summary>
        static bool ArkShips => !PerfStrip.LightToysOnly;

        bool WanderRunning => _wanderRun && _wanderRun.IsRunning;
        bool VoyageRunning => _arkRun && _arkRun.IsRunning;
        bool VoyageBuilding => _arkRun && _arkRun.IsBuilding;
        bool AnyRunning => WanderRunning || VoyageRunning;

        /// <summary>The local player's live domain; the sentinel reads as Jade, the menu's default.</summary>
        Domains LiveDomain
        {
            get
            {
                var domain = Context?.GameData?.LocalPlayer?.Domain ?? Domains.Jade;
                return domain == Domains.Blue ? Domains.Jade : domain;
            }
        }

        // ── Emblem ───────────────────────────────────────────────────────────

        protected override void OnInitialized() => AttachEmblem(new EmblemSource(this), OrbitStopped);

        /// <summary>
        /// Both choices in one glyph: a miniature Ark as the core, built from the Ark's own hull plan
        /// in the player's live domain, ringed by three real microscenes built by the same planner
        /// the belt runs. Planning lays no prism: the emblem costs meshes, not mass.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly WanderToy _toy;
            public EmblemSource(WanderToy toy) => _toy = toy;

            public int SatelliteCount => EmblemRecipes.Length;

            // Microscenes and the mini Ark wear the real per-domain prism materials.
            public bool UsesSharedMaterial => false;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                if (slot == 0)
                {
                    heavy = true; // mesh assembly - give the streamer a clear frame after it
                    // A build that does not offer the Ark must not advertise one: its core is the
                    // Without Ark station's own microscene instead.
                    if (!ArkShips)
                    {
                        var core = BuildSceneMiniature(_toy._wanderCfg, StationRecipe, 7, radius);
                        return _toy.AttachEmblemModel(holder, core, "Microscene", spin: true);
                    }
                    var ark = BuildArkMiniature(_toy._arkCfg, _toy.LiveDomain, radius);
                    return _toy.AttachEmblemModel(holder, ark, "Ark", spin: true);
                }

                int recipe = slot - 1;
                if (recipe < 0 || recipe >= EmblemRecipes.Length) return false;
                var scene = BuildSceneMiniature(_toy._wanderCfg, EmblemRecipes[recipe], slot, radius);
                return _toy.AttachEmblemModel(holder, scene, "Microscene", spin: false);
            }

            public bool TryGetLiveKey(out object key)
            {
                // Rebuild when the player's domain changes - the escort flies YOUR flag.
                key = _toy.LiveDomain;
                return true;
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false; // every item wears its own real domain materials
            }
        }

        bool AttachEmblemModel(Transform holder, CellMiniatureBuilder.Miniature miniature, string name, bool spin)
        {
            if (!miniature.IsValid) return false;
            var go = ToyFactory.AddMiniatureBody(holder, miniature, Context, name);
            if (!go) return false;
            if (spin) go.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 10f);
            // The emblem built this mesh, so the emblem frees it.
            Emblem?.Own(miniature.Mesh);
            return true;
        }

        static CellMiniatureBuilder.Miniature BuildArkMiniature(ArkwayConfig cfg, Domains domain, float radius)
        {
            var lays = Ark.BuildHullLays(Mathf.Max(30f, cfg?.ArkHullLength ?? 110f), domain);
            return CellMiniatureBuilder.BuildFromLays(lays, radius, StationPointBudget, 1f, "MiniArk");
        }

        static CellMiniatureBuilder.Miniature BuildSceneMiniature(ConveyorConfig cfg, int recipe, int salt, float radius)
        {
            if (cfg == null) return default;
            // Seeded off the config, never re-rolled: recipes randomise their parameters on every
            // Plan call, and an icon that changes shape between rebuilds is a bug.
            var rng = new System.Random(cfg.Seed * 31 + salt);
            var plan = MicroscenePatterns.Plan(recipe, rng, prismBudget: 60,
                sceneRadius: cfg.SceneRadius, maxCrystals: 0, palette: cfg.Palette);
            if (plan?.Prisms is not { Count: > 0 }) return default;
            return CellMiniatureBuilder.BuildFromLays(plan.Prisms, radius, 120, 1f, $"Micro_{recipe}");
        }

        // Must call base.Update() - Toy.Update owns the exit-gated re-arm.
        protected override void Update()
        {
            base.Update();
            if (!Emblem) return;

            bool freestyle = Context?.IsFreestyleActive == null || Context.IsFreestyleActive();
            Emblem.SetOrbitRate(!AnyRunning ? OrbitStopped : freestyle ? OrbitFlowing : OrbitDormant);
        }

        // ── A pass: a way home while a run is live, else the matrix ──────────

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (VoyageBuilding)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    "[WanderToy] Pass ignored - the voyage is still building behind the veil.");
                return;
            }

            if (AnyRunning)
            {
                EndLiveRun("the player flew the Wander toy again");
                return; // the run's end callback reblooms the toy
            }

            // With one choice there is nothing to choose: a pass starts the wander, exactly as the
            // single-purpose Wanderway toy did before the two runs were merged into this one, so
            // the toy stays a one-ring toggle (pass to leave, pass again to come home).
            if (!ArkShips)
            {
                StartChoice(Choice.WithoutArk);
                return;
            }

            base.OnActivated(localVessel);
        }

        // ── The one declaration ──────────────────────────────────────────────

        protected override float StationSpacing => _def ? _def.StationSpacing : 90f;
        protected override float StationRadius => Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
        protected override float MatrixDistanceFactor => _def ? _def.MatrixDistanceFactor : 3f;

        /// <summary>Two choices, side by side.</summary>
        protected override int MatrixColumns(int count) => count;

        /// <summary>
        /// The same two rows for the fly-through stations and the Toy Box cards. With no run live
        /// both start their run. With one live, that one's row is the way home and the other is
        /// listed but not startable (Apply is null, so the station does nothing and the window has
        /// nothing to commit) until you come home.
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
        {
            if (ArkShips) into.Add(BuildOption(Choice.WithArk));
            into.Add(BuildOption(Choice.WithoutArk));
        }

        ToyShellOption BuildOption(Choice choice)
        {
            bool withArk = choice == Choice.WithArk;
            bool live = withArk ? VoyageRunning : WanderRunning;
            bool otherLive = withArk ? WanderRunning : VoyageRunning;
            var capturedChoice = choice;

            string detail =
                live ? (withArk ? "under way - bring the Ark home and return to the cell"
                                : "wandering - end the wander and return to the cell")
                : otherLive ? "come home from the current wander first"
                : withArk ? "escort an Ark on a voyage through a corridor of cells"
                          : "fly alone through an endless belt of little worlds";

            // Live: the way home. Another run live: listed, not startable. Neither: start it.
            System.Action apply = null;
            if (live) apply = () => EndLiveRun("the player chose to come home");
            else if (!otherLive) apply = () => StartChoice(capturedChoice);

            return new ToyShellOption
            {
                Label = withArk ? WithArkLabel : WithoutArkLabel,
                Detail = detail,
                Accent = Definition ? Definition.AccentColor : Color.white,
                IsCurrent = live,
                // A wander means nothing without the player at the stick: the shell enters
                // freestyle first, then applies.
                RequiresFreestyle = true,
                // A run is STARTED, not switched to: the window closes and the player is flying.
                CommitVerb = live ? "Come home" : "Start",
                Payload = capturedChoice,
                Apply = apply,
                BuildPreview = parent => BuildShellPreview(capturedChoice, parent),
            };
        }

        protected override bool ShellAvailable => _wanderCfg != null && _arkCfg != null;

        // ── Stations ─────────────────────────────────────────────────────────

        protected override void BuildStation(ToyShellOption option, Transform parent, Vector3 position, float radius)
        {
            if (option.Payload is not Choice choice) return;
            var station = CreateStation(option, parent, position, option.Label, radius * 1.6f);

            var model = ResolveModel(choice, radius);
            if (!model.IsValid)
            {
                ToyFactory.AddSphereBody(station.transform, radius * 0.6f,
                    Definition ? Definition.AccentColor : Color.white);
                return;
            }

            var go = ToyFactory.AddMiniatureBody(station.transform, model, Context, option.Label);
            if (!go) return;
            // The model turns in place, and grows in rather than appearing (continuity law).
            go.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 12f);
            ToyFactory.ScaleInFromZero(go.transform, 0.6f).Forget();
        }

        /// <summary>
        /// The card's picture: the same model the station shows, so a choice looks the same
        /// whichever surface it was picked from. No spin (the preview camera orbits) and no bloom
        /// (the camera measures the model the frame it is handed it).
        /// </summary>
        GameObject BuildShellPreview(Choice choice, Transform parent)
        {
            if (!parent) return null;
            var model = ResolveModel(choice, StationRadius);
            return model.IsValid
                ? ToyFactory.AddMiniatureBody(parent, model, Context, $"{choice} Preview")
                : null;
        }

        /// <summary>
        /// The cached station model for <paramref name="choice"/>. The Ark model is rebuilt when the
        /// player's domain changes (it wears their flag); the microscene never changes.
        /// </summary>
        CellMiniatureBuilder.Miniature ResolveModel(Choice choice, float radius)
        {
            if (choice == Choice.WithArk)
            {
                var domain = LiveDomain;
                if (_arkModel.IsValid && _arkModelDomain == domain) return _arkModel;
                // Retired, not destroyed: an open station or a card preview may still be drawing
                // it. Freed with the toy.
                if (_arkModel.Mesh) _retiredMeshes.Add(_arkModel.Mesh);
                _arkModel = BuildArkMiniature(_arkCfg, domain, radius);
                _arkModelDomain = domain;
                return _arkModel;
            }

            if (!_sceneModel.IsValid)
                _sceneModel = BuildSceneMiniature(_wanderCfg, StationRecipe, 7, radius);
            return _sceneModel;
        }

        // ── Starting and ending ──────────────────────────────────────────────

        /// <summary>
        /// The option's Apply for BOTH surfaces. The matrix folds first: the cell it stands in is
        /// about to be handed its bare canvas.
        /// </summary>
        void StartChoice(Choice choice)
        {
            if (AnyRunning) return; // belt and braces: the option is not offered while one is live

            var vessel = ResolveLocalVessel();
            if (vessel?.Vessel == null)
            {
                CSDebug.LogWarning("[WanderToy] No local vessel to send wandering.");
                return;
            }

            CloseMatrix();

            if (choice == Choice.WithArk) StartVoyage(vessel);
            else StartWander(vessel);
        }

        void StartWander(IVesselStatus localVessel)
        {
            if (!_wanderCfg.PrismPrefab)
                CSDebug.LogWarning("[WanderToy] No prism prefab wired for Without Ark - scenes will " +
                                   "carry only crystals and lifeforms. Author the settings asset (or run " +
                                   "FrogletTools > Scene Setup > Setup Freestyle Toybox) to wire one.");

            EnsureBelt();
            EnsureWanderRun();

            // Order matters: the run reverts the cell FIRST (that swap raises the load veil and
            // retires the old world), so the belt's stock build joins the same hold instead of
            // stacking a second cover on top of it.
            _wanderRun.Begin(localVessel);

            // Keyed off an explicit flag, not IsRunning: a stopped belt is not an unbuilt one.
            if (_beltPrimed)
            {
                _belt.Resume(localVessel);
            }
            else
            {
                _belt.Begin(_wanderCfg, localVessel, Context?.IsFreestyleActive, Context?.GameData);
                _beltPrimed = true;
            }

            Rebloom();
        }

        void StartVoyage(IVesselStatus localVessel)
        {
            if (!_arkCfg.PrismPrefab)
            {
                CSDebug.LogWarning("[WanderToy] No prism prefab wired for With Ark - an Ark cannot exist " +
                                   "without a hull. Author the settings asset (or run FrogletTools > " +
                                   "Scene Setup > Setup Freestyle Toybox) to wire one.");
                return;
            }

            EnsureCorridor();
            EnsureVoyageRun();
            _arkRun.Begin(localVessel);
            Rebloom();
        }

        /// <summary>End whichever run is live and bring the player home. Its end callback reblooms the toy.</summary>
        void EndLiveRun(string reason)
        {
            if (VoyageBuilding) return;
            if (WanderRunning) _wanderRun.End(returnToCell: true);
            else if (VoyageRunning) _arkRun.End(returnToCell: true, reason);
        }

        // The belt, the corridor and both runs live as SIBLINGS of the toy under the toybox root,
        // never as children: the toy's root scale animates on bloom/rebloom and must never scale
        // laid mass, standing cells or the Ark. Still torn down with the toybox root on scene exit.

        void EnsureBelt()
        {
            if (_belt) return;
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(ToyboxRoot, false);
            _belt = go.AddComponent<MicrosceneConveyor>();
        }

        void EnsureWanderRun()
        {
            if (_wanderRun) return;
            var go = new GameObject("WanderwayRun");
            go.transform.SetParent(ToyboxRoot, false);
            _wanderRun = go.AddComponent<WanderwayRun>();
            _wanderRun.Configure(_wanderCfg, Context, _belt, Rebloom);
        }

        void EnsureCorridor()
        {
            if (_corridor) return;
            var go = new GameObject("ArkwayCellConveyor");
            go.transform.SetParent(ToyboxRoot, false);
            _corridor = go.AddComponent<CellConveyor>();
        }

        void EnsureVoyageRun()
        {
            if (_arkRun) return;
            var go = new GameObject("ArkwayRun");
            go.transform.SetParent(ToyboxRoot, false);
            _arkRun = go.AddComponent<ArkwayRun>();
            _arkRun.Configure(_arkCfg, Context, Context?.Container, _corridor, Rebloom);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_sceneModel.Mesh) Destroy(_sceneModel.Mesh);
            if (_arkModel.Mesh) Destroy(_arkModel.Mesh);
            foreach (var mesh in _retiredMeshes)
                if (mesh) Destroy(mesh);
            _retiredMeshes.Clear();
        }
    }
}
