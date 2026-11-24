using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Wander</b> toy: leave the cell and go wandering, <b>with an Ark</b> or <b>without
    /// one</b>. The Toy Box window shows the two as cards; they are the two runs that used to be
    /// two separate toys:
    ///
    /// <list type="bullet">
    /// <item><b>Without Ark</b> (<see cref="WanderwayRun"/>) - the cell reverts to its bare
    /// canvas, a belt of little worlds streams ahead of your flight path, and your trail becomes a
    /// rolling tether with the way home riding its far end.</item>
    /// <item><b>With Ark</b> (<see cref="ArkwayRun"/>) - a corridor of whole cells opens ahead and
    /// an <see cref="Ark"/> in your domain sails it at its own pace, with you sworn to its side.</item>
    /// </list>
    ///
    /// <para>While a run is live its card is the way home ("Come home"); the run's own return
    /// station and the overview button end it too.</para>
    ///
    /// <para><b>One wander at a time.</b> Both runs hand the host cell its bare canvas, raise the
    /// load veil and own the local trail, so two at once would fight over all three. While one is
    /// live its own card becomes the way home and the OTHER card is offered but not startable -
    /// still listed (a surface may decline to act, never to OFFER), and honest about why.</para>
    ///
    /// </summary>
    public sealed class WanderToy : MatrixToy
    {
        /// <summary>The recipe the WITHOUT ARK card models: an archway you fly under.</summary>
        const int PreviewRecipe = 16;

        /// <summary>Budget for a card miniature.</summary>
        const int PreviewPointBudget = 160;

        /// <summary>Radius a card miniature is built at.</summary>
        const float PreviewRadius = 20f;

        /// <summary>The label the WITH ARK card wears.</summary>
        public const string WithArkLabel = "With Ark";

        /// <summary>The label the WITHOUT ARK card wears.</summary>
        public const string WithoutArkLabel = "Without Ark";

        /// <summary>The two choices, in the order the window lists them.</summary>
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

        // Card models, built once and shared (the card preview borrows the mesh, never owns it).
        CellMiniatureBuilder.Miniature _sceneModel;
        CellMiniatureBuilder.Miniature _arkModel;
        Domains _arkModelDomain;
        readonly List<Mesh> _retiredMeshes = new();

        public void Configure(WanderToyDefinitionSO definition)
        {
            _def = definition;
            _wanderCfg = definition ? definition.BuildWithoutArkConfig() : new ConveyorConfig();
            _arkCfg = definition ? definition.BuildWithArkConfig() : new ArkwayConfig();

            // The device tier's belt budget lands on the BUILT config, never on the settings asset,
            // so every other tier still flies the authored belt (Docs/PLATFORM_UNIFICATION.md §3.6).
            var profile = PlatformProfile.Current;
            if (profile) profile.Wanderway.ApplyTo(_wanderCfg);
        }

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

        static CellMiniatureBuilder.Miniature BuildArkMiniature(ArkwayConfig cfg, Domains domain, float radius)
        {
            // The voyage's Ark in miniature: its model's sockets when it is drawn as one, the spindle otherwise.
            var lays = ArkModel.SocketLays(cfg?.ArkModel, domain)
                       ?? Ark.BuildHullLays(Mathf.Max(30f, cfg?.ArkHullLength ?? 110f), domain);
            return CellMiniatureBuilder.BuildFromLays(lays, radius, PreviewPointBudget, 1f, "MiniArk");
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

        // ── The one declaration ──────────────────────────────────────────────

        /// <summary>
        /// The two Toy Box cards. With no run live both start their run. With one live, that one's
        /// row is the way home and the other is listed but not startable (Apply is null) until you
        /// come home.
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
        {
            into.Add(BuildOption(Choice.WithArk));
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

        /// <summary>
        /// The card's picture. No spin (the preview camera orbits) and no bloom (the camera measures
        /// the model the frame it is handed it).
        /// </summary>
        GameObject BuildShellPreview(Choice choice, Transform parent)
        {
            if (!parent) return null;
            var model = ResolveModel(choice, PreviewRadius);
            return model.IsValid
                ? ToyFactory.AddMiniatureBody(parent, model, Context, $"{choice} Preview")
                : null;
        }

        /// <summary>
        /// The cached card model for <paramref name="choice"/>. The Ark model is rebuilt when the
        /// player's domain changes (it wears their flag); the microscene never changes.
        /// </summary>
        CellMiniatureBuilder.Miniature ResolveModel(Choice choice, float radius)
        {
            if (choice == Choice.WithArk)
            {
                var domain = LiveDomain;
                if (_arkModel.IsValid && _arkModelDomain == domain) return _arkModel;
                // Retired, not destroyed: a card preview may still be drawing it. Freed with the toy.
                if (_arkModel.Mesh) _retiredMeshes.Add(_arkModel.Mesh);
                _arkModel = BuildArkMiniature(_arkCfg, domain, radius);
                _arkModelDomain = domain;
                return _arkModel;
            }

            if (!_sceneModel.IsValid)
                _sceneModel = BuildSceneMiniature(_wanderCfg, PreviewRecipe, 7, radius);
            return _sceneModel;
        }

        // ── Starting and ending ──────────────────────────────────────────────

        /// <summary>
        /// The option's Apply: start the chosen run.
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

            if (choice == Choice.WithArk) StartVoyage(vessel);
            else StartWander(vessel);
        }

        void StartWander(IVesselStatus localVessel)
        {
            if (!_wanderCfg.PrismPrefab)
                CSDebug.LogWarning("[WanderToy] No prism prefab wired for Without Ark - scenes will " +
                                   "carry only crystals and lifeforms. Author the settings asset (or run " +
                                   "FrogletTools > Scenes > Setup Freestyle Toybox) to wire one.");

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
        }

        /// <summary>End whichever run is live and bring the player home.</summary>
        void EndLiveRun(string reason)
        {
            if (VoyageBuilding) return;
            if (WanderRunning) _wanderRun.End(returnToCell: true);
            else if (VoyageRunning) _arkRun.End(returnToCell: true, reason);
        }

        // The belt, the corridor and both runs live as SIBLINGS of the toy under the toybox root.
        // Torn down with the toybox root on scene exit.

        void EnsureBelt()
        {
            if (_belt) return;
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(transform.parent, false);
            _belt = go.AddComponent<MicrosceneConveyor>();
        }

        void EnsureWanderRun()
        {
            if (_wanderRun) return;
            var go = new GameObject("WanderwayRun");
            go.transform.SetParent(transform.parent, false);
            _wanderRun = go.AddComponent<WanderwayRun>();
            _wanderRun.Configure(_wanderCfg, Context, _belt, OnRunEnded);
        }

        void EnsureCorridor()
        {
            if (_corridor) return;
            var go = new GameObject("ArkwayCellConveyor");
            go.transform.SetParent(transform.parent, false);
            _corridor = go.AddComponent<CellConveyor>();
        }

        void EnsureVoyageRun()
        {
            if (_arkRun) return;
            var go = new GameObject("ArkwayRun");
            go.transform.SetParent(transform.parent, false);
            _arkRun = go.AddComponent<ArkwayRun>();
            _arkRun.Configure(_arkCfg, Context, Context?.Container, _corridor, OnRunEnded);
        }

        // Nothing to redraw: the Toy Box re-reads this toy's options every time it opens.
        static void OnRunEnded() { }

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
