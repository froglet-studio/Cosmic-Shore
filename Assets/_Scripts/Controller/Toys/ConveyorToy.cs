using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Wanderway toy: fly through it and you LEAVE for a wander. The cell reverts to its bare
    /// environment-free canvas, a belt of little worlds - grand assemblies and gate runs, tunnels,
    /// canyons, orchards, meadows, menageries - starts streaming ahead of your flight path scene
    /// after scene, and your trail becomes a rolling tether: a fixed-length ribbon that follows you
    /// (the tail recycles into the pool the head lays from) with the station that brings you home
    /// riding its far end.
    ///
    /// Three things end the wander and all do the same thing (see <see cref="WanderwayRun"/>): the
    /// return station at the tail of your tether, another pass through this toy, and the overview
    /// button (or gamepad Start), which drops freestyle. The label flips to show which way the next
    /// pass will toggle it; the emblem's orbit speed carries the live state.
    ///
    /// The belt is a closed system: its whole conserved stock is built ONCE, behind a load veil, on
    /// the first wander, and every arrival after that is transport (the same mass, endlessly
    /// re-arranged). No score, no end condition - wander as long as you like.
    /// </summary>
    public class ConveyorToy : Toy, IToyShellSurface
    {
        /// <summary>The recipes the emblem shows: a gate run, a tunnel, an archway, a torus knot.</summary>
        static readonly int[] EmblemRecipes = { 0, 2, 16, 30 };

        // Orbit rates that ARE the belt state. Motion is the one identity channel that survives
        // distance, and unlike the old two-state label/tint it tells the truth about all three:
        // a belt that is running but dormant (you left freestyle) is neither off nor flowing.
        const float OrbitStopped = 0f;
        const float OrbitDormant = 3f;
        const float OrbitFlowing = 18f;

        ConveyorConfig _cfg;
        MicrosceneConveyor _conveyor;
        WanderwayRun _run;
        bool _conveyorPrimed;   // the stock is built ONCE - a later wander resumes, never re-primes
        TMP_Text _label;

        public void Configure(ConveyorConfig cfg) => _cfg = cfg;

        protected override void OnInitialized()
        {
            _label = GetComponentInChildren<TMP_Text>(true);

            // Show the "off" affordance from the start so the first pass reads as a switch.
            if (_label)
                _label.text = $"{DisplayName}\n<size=60%>fly through to wander</size>";

            AttachEmblem(new EmblemSource(this), OrbitStopped);
        }

        /// <summary>
        /// The Wanderway in one glyph: four real microscenes - built by the same
        /// <see cref="MicroscenePatterns"/> planner the belt itself runs, so they are literally
        /// scenes you will fly through - orbiting a fifth. The orbit's SPEED is the belt state.
        ///
        /// Planning is pure trig against a seeded RNG and lays no prism: the belt's conserved stock
        /// is untouched, and the emblem costs a mesh, not mass.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly ConveyorToy _toy;
            public EmblemSource(ConveyorToy toy) => _toy = toy;

            public int SatelliteCount => 3;

            // Microscenes wear the real per-domain prism materials.
            public bool UsesSharedMaterial => false;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                var cfg = _toy._cfg;
                if (cfg == null || slot < 0 || slot >= EmblemRecipes.Length) return false;

                // Seeded off the config, never re-rolled: recipes randomise their parameters on
                // every Plan call, and an icon that changes shape between rebuilds is a bug.
                var rng = new System.Random(cfg.Seed * 31 + slot);
                var plan = MicroscenePatterns.Plan(EmblemRecipes[slot], rng, prismBudget: 60,
                    sceneRadius: cfg.SceneRadius, maxCrystals: 0, palette: cfg.Palette);
                if (plan?.Prisms is not { Count: > 0 }) return false;

                var miniature = CellMiniatureBuilder.BuildFromLays(plan.Prisms, radius, 120, 1f,
                    $"Micro_{EmblemRecipes[slot]}");
                if (!miniature.IsValid) return false;

                var go = ToyFactory.AddMiniatureBody(holder, miniature, _toy.Context, "Microscene");
                if (!go) return false;

                // The emblem built these meshes, so the emblem frees them.
                _toy.Emblem?.Own(miniature.Mesh);
                return true;
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                return false; // state is carried by orbit speed, not by a rebuild
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false; // microscenes wear their own real domain materials
            }
        }

        // The belt has THREE states and the emblem shows all three. Must call base.Update() -
        // Toy.Update owns the exit-gated re-arm, and shadowing it ships a toy that never fires.
        protected override void Update()
        {
            base.Update();
            if (!Emblem) return;

            bool running = _run && _run.IsRunning;
            bool freestyle = Context?.IsFreestyleActive == null || Context.IsFreestyleActive();
            Emblem.SetOrbitRate(!running ? OrbitStopped : freestyle ? OrbitFlowing : OrbitDormant);
        }

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        bool IToyShellSurface.ShellAvailable => _cfg != null;

        /// <summary>
        /// One option, because the toy is one switch: the wander is on or it is off. It needs the
        /// player at the stick (the belt streams ahead of a flight path and the tether is your own
        /// trail), so the shell enters freestyle first and then throws the same switch a pass
        /// through the ring throws.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(System.Collections.Generic.List<ToyShellOption> into)
        {
            bool running = _run && _run.IsRunning;

            into.Add(new ToyShellOption
            {
                Label = running ? "Come home" : "Wander",
                Detail = running
                    ? "end the wander and return to the cell"
                    : "leave for an endless belt of little worlds",
                Accent = Definition ? Definition.AccentColor : Color.white,
                IsCurrent = running,
                RequiresFreestyle = true,
                Apply = ActivateFromShell,
            });
        }

        /// <summary>
        /// The shell's press, routed through the toy's own activation so there is exactly one
        /// implementation of "throw this switch" - the shell cannot drift from the ring.
        /// </summary>
        void ActivateFromShell()
        {
            var vessel = ResolveLocalVessel();
            if (vessel == null)
            {
                CSDebug.LogWarning("[ConveyorToy] No local vessel to send wandering.");
                return;
            }
            OnActivated(vessel);
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_cfg == null)
            {
                CSDebug.LogWarning("[ConveyorToy] No config assigned - nothing to run.");
                return;
            }
            if (localVessel?.Vessel == null) return;

            // Toggle: a pass while the wander is on ends it (and brings the player home). The
            // belt's scenes stay in the world - conserved mass and released citizens are not toy
            // props to vanish.
            if (_run && _run.IsRunning)
            {
                _run.End(returnToCell: true);
                return; // End raises the callback that flips the label
            }

            if (!_cfg.PrismPrefab)
                CSDebug.LogWarning("[ConveyorToy] No prism prefab wired - scenes will carry only " +
                                   "crystals and lifeforms. Author the definition asset (or run " +
                                   "FrogletTools > Scene Setup > Setup Freestyle Toybox) to wire one.");

            EnsureConveyor();
            EnsureRun();

            // Order matters: the run reverts the cell FIRST (that swap raises the load veil and
            // retires the old world), so the belt's stock build joins the same hold instead of
            // stacking a second cover on top of it.
            _run.Begin(localVessel);

            // Begin BUILDS the belt's whole conserved stock; every later wander resumes the stock
            // that already exists. Keyed off an explicit flag, not IsRunning: a stopped belt is not
            // an unbuilt one, and re-priming would mint a second pool.
            if (_conveyorPrimed)
            {
                _conveyor.Resume(localVessel);
            }
            else
            {
                _conveyor.Begin(_cfg, localVessel, Context?.IsFreestyleActive, Context?.GameData);
                _conveyorPrimed = true;
            }

            ShowState(on: true);
        }

        /// <summary>
        /// The belt lives as a SIBLING of the toy under the toybox root, never as a child: the
        /// toy's root scale animates on bloom/rebloom and must never scale the belt's laid mass.
        /// Still torn down with the toybox root on scene exit.
        /// </summary>
        void EnsureConveyor()
        {
            if (_conveyor) return;
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(transform.parent, false);
            _conveyor = go.AddComponent<MicrosceneConveyor>();
        }

        void EnsureRun()
        {
            if (_run) return;
            var go = new GameObject("WanderwayRun");
            go.transform.SetParent(transform.parent, false); // sibling, same reason as the belt
            _run = go.AddComponent<WanderwayRun>();
            _run.Configure(_cfg, Context, _conveyor, () => ShowState(on: false));
        }

        /// <summary>
        /// Flip the toy's look so the player can read the belt state at a glance - and know the
        /// next pass toggles it the other way. The STATE itself is carried by the emblem's orbit
        /// speed (see <see cref="Update"/>); this just retexts the label and reblooms to signal the
        /// in-place change (the established flip-set pattern).
        ///
        /// It used to also write <c>_body.sharedMaterial.color</c> - which was the SHARED, cached
        /// per-accent material from <see cref="ToyFactory.AccentMaterial"/> ("nothing mutates these
        /// after creation"): it repainted every body using that accent and desynchronised the
        /// cache's colour key. Latent only because this toy's accent happened to be unique.
        /// </summary>
        void ShowState(bool on)
        {
            if (_label)
                _label.text = on
                    ? $"{DisplayName}\n<size=60%>wandering - fly through to come home</size>"
                    : $"{DisplayName}\n<size=60%>fly through to wander</size>";

            Rebloom();
        }
    }
}
