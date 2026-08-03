using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The microscene conveyor toy: fly through it and a belt of little worlds - prism gate runs,
    /// helix weaves, tunnels, canyons, orchards, meadows, menageries and more - starts blooming in
    /// ahead of your flight path, scene after scene, like an open world crossed with an infinite
    /// runner. The belt follows you anywhere at any speed; once the pool is full it recycles the
    /// scene farthest behind you into a fresh arrangement ahead (a closed system: the same
    /// conserved mass, endlessly re-arranged). Fly through the toy again to switch the flow OFF -
    /// the toy's body and label flip to show which way the next pass will toggle it. No score, no
    /// end condition - fly it forever.
    /// </summary>
    public class ConveyorToy : Toy
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
        TMP_Text _label;

        public void Configure(ConveyorConfig cfg) => _cfg = cfg;

        protected override void OnInitialized()
        {
            _label = GetComponentInChildren<TMP_Text>(true);

            // Show the "off" affordance from the start so the first pass reads as a switch.
            if (_label)
                _label.text = $"{DisplayName}\n<size=60%>fly through to start</size>";

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
                    radius: cfg.SceneRadius, maxCrystals: 0, cfg.Palette);
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

            bool running = _conveyor && _conveyor.IsRunning;
            bool freestyle = Context?.IsFreestyleActive == null || Context.IsFreestyleActive();
            Emblem.SetOrbitRate(!running ? OrbitStopped : freestyle ? OrbitFlowing : OrbitDormant);
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_cfg == null)
            {
                CSDebug.LogWarning("[ConveyorToy] No config assigned - nothing to run.");
                return;
            }
            if (localVessel?.Vessel == null) return;

            // Toggle: a pass while the belt is flowing switches it off (scenes stay in the
            // world - conserved mass and released citizens are not toy props to vanish).
            if (_conveyor && _conveyor.IsRunning)
            {
                _conveyor.StopBelt();
                ShowState(on: false);
                return;
            }

            if (_conveyor)
            {
                _conveyor.Resume(localVessel);
                ShowState(on: true);
                return;
            }

            if (!_cfg.PrismPrefab)
                CSDebug.LogWarning("[ConveyorToy] No prism prefab wired - scenes will carry only " +
                                   "crystals and lifeforms. Author the definition asset (or run " +
                                   "FrogletTools > Scene Setup > Setup Freestyle Toybox) to wire one.");

            // Sibling of the toy under the toybox root (NOT a child of the toy - the toy's root
            // scale animates on bloom/rebloom and must never scale the belt's laid mass). Still
            // torn down with the toybox root on scene exit.
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(transform.parent, false);
            _conveyor = go.AddComponent<MicrosceneConveyor>();
            _conveyor.Begin(_cfg, localVessel, Context?.IsFreestyleActive, Context?.GameData);
            ShowState(on: true);
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
                    ? $"{DisplayName}\n<size=60%>flowing - fly through to stop</size>"
                    : $"{DisplayName}\n<size=60%>fly through to start</size>";

            Rebloom();
        }
    }
}
