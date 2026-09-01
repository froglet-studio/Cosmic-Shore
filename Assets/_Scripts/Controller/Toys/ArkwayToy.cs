using CosmicShore.Data;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Arkway toy: fly through it and a VOYAGE begins — a corridor of whole cells opens
    /// ahead (previous / current / next, recycled forever) and an <see cref="Ark"/> in your
    /// domain sails it at its own unhurried pace, with you sworn to its side. Take over each
    /// cell's volume and its fauna waves spawn in your colour and cannot touch the Ark; lose a
    /// cell and its waves hunt the Ark's hull. If the food web eats the last hull prism the
    /// voyage resets; otherwise it goes on forever, cell after cell.
    ///
    /// Three things end a voyage and all route through <see cref="ArkwayRun.End"/>: the
    /// DISEMBARK station standing at the entrance you sailed from, another pass through this toy, and the overview
    /// button (or gamepad Start), which drops freestyle. The Ark falling is the fourth — the
    /// reset. The label flips to show which way the next pass toggles; the emblem's orbit
    /// speed carries the live state (the Wanderway's own idiom).
    /// </summary>
    public class ArkwayToy : Toy
    {
        // Orbit rates that ARE the voyage state (motion is the identity channel that survives
        // distance): stopped / running-but-dormant / under way.
        const float OrbitStopped = 0f;
        const float OrbitDormant = 3f;
        const float OrbitFlowing = 18f;

        ArkwayConfig _cfg;
        CellConveyor _conveyor;
        ArkwayRun _run;
        TMP_Text _label;

        public void Configure(ArkwayConfig cfg) => _cfg = cfg;

        protected override void OnInitialized()
        {
            _label = GetComponentInChildren<TMP_Text>(true);
            if (_label)
                _label.text = $"{DisplayName}\n<size=60%>fly through to set sail</size>";

            AttachEmblem(new EmblemSource(this), OrbitStopped);
        }

        /// <summary>
        /// The Arkway in one glyph: a miniature ARK as the core — the thing the toy is about —
        /// with three plain rings orbiting it, one per standing traversal cell. The mini Ark is
        /// built from the Ark's own hull plan (pure math, no prism laid) in the local player's
        /// LIVE domain, so the emblem shows the ship you would actually escort — and re-shows
        /// it when the domain changer repaints you.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly ArkwayToy _toy;
            public EmblemSource(ArkwayToy toy) => _toy = toy;

            public int SatelliteCount => CellConveyor.TargetStanding;

            // The mini Ark wears the real per-domain prism materials.
            public bool UsesSharedMaterial => false;

            Domains LiveDomain
            {
                get
                {
                    var player = _toy.Context?.GameData?.LocalPlayer;
                    var domain = player?.Domain ?? Domains.Jade;
                    return domain == Domains.Blue ? Domains.Jade : domain;
                }
            }

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                var cfg = _toy._cfg;
                if (cfg == null) return false;

                // Slot 0 is the CORE (ToyEmblem's contract); slots 1..N are the satellites.
                if (slot == 0)
                {
                    heavy = true; // mesh assembly — give the streamer a clear frame after it
                    var lays = Ark.BuildHullLays(Mathf.Max(30f, cfg.ArkHullLength), LiveDomain);
                    var miniature = CellMiniatureBuilder.BuildFromLays(lays, radius, 160, 1f, "MiniArk");
                    if (!miniature.IsValid) return false;

                    var go = ToyFactory.AddMiniatureBody(holder, miniature, _toy.Context, "Ark");
                    if (!go) return false;
                    go.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 10f);
                    _toy.Emblem?.Own(miniature.Mesh);
                    return true;
                }

                // Satellites: one plain ring per standing traversal cell.
                var ring = ToyFactory.AddRingBody(holder, radius * 0.85f,
                    _toy.Definition ? _toy.Definition.AccentColor : Color.white);
                return ring;
            }

            public bool TryGetLiveKey(out object key)
            {
                // Rebuild the mini Ark when the player's domain changes — the escort flies
                // YOUR flag.
                key = LiveDomain;
                return true;
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false; // the mini Ark wears its own real domain materials
            }
        }

        // Must call base.Update() — Toy.Update owns the exit-gated re-arm.
        protected override void Update()
        {
            base.Update();
            if (!Emblem) return;

            bool running = _run && _run.IsRunning;
            bool freestyle = Context?.IsFreestyleActive == null || Context.IsFreestyleActive();
            Emblem.SetOrbitRate(!running ? OrbitStopped : freestyle ? OrbitFlowing : OrbitDormant);
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_cfg == null)
            {
                CSDebug.LogWarning("[ArkwayToy] No config assigned - nothing to sail.");
                return;
            }
            if (localVessel?.Vessel == null) return;

            // Toggle: a pass while a voyage is live ends it (and brings the player home).
            if (_run && _run.IsRunning)
            {
                _run.End(returnToCell: true);
                return; // End raises the callback that flips the label
            }

            if (!_cfg.PrismPrefab)
            {
                CSDebug.LogWarning("[ArkwayToy] No prism prefab wired - an Ark cannot exist " +
                                   "without a hull. Author the definition asset (or run " +
                                   "FrogletTools > Scene Setup > Setup Freestyle Toybox) to wire one.");
                return;
            }

            EnsureConveyor();
            EnsureRun();

            _run.Begin(localVessel);
            ShowState(on: true);
        }

        /// <summary>
        /// The corridor and the run live as SIBLINGS of the toy under the toybox root, never as
        /// children: the toy's root scale animates on bloom/rebloom and must never scale the
        /// corridor's standing cells or the Ark. Still torn down with the toybox root on scene
        /// exit.
        /// </summary>
        void EnsureConveyor()
        {
            if (_conveyor) return;
            var go = new GameObject("ArkwayCellConveyor");
            go.transform.SetParent(transform.parent, false);
            _conveyor = go.AddComponent<CellConveyor>();
        }

        void EnsureRun()
        {
            if (_run) return;
            var go = new GameObject("ArkwayRun");
            go.transform.SetParent(transform.parent, false); // sibling, same reason as the corridor
            _run = go.AddComponent<ArkwayRun>();
            _run.Configure(_cfg, Context, ContextContainer, _conveyor, () => ShowState(on: false));
        }

        Reflex.Core.Container ContextContainer => Context?.Container;

        void ShowState(bool on)
        {
            if (_label)
                _label.text = on
                    ? $"{DisplayName}\n<size=60%>under way - fly through to end the voyage</size>"
                    : $"{DisplayName}\n<size=60%>fly through to set sail</size>";

            Rebloom();
        }
    }
}
