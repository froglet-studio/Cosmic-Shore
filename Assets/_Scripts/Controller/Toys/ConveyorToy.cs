using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Wanderway toy: fly through it and you LEAVE for a wander. The cell reverts to its bare
    /// environment-free canvas, a belt of little worlds — grand assemblies and gate runs, tunnels,
    /// orchards, menageries — starts streaming ahead of your flight path, and the trail you lay on
    /// the way out is a finite tether whose far end carries the station that brings you home.
    ///
    /// Two things end the wander and both do the same thing (see <see cref="WanderwayRun"/>): the
    /// return station at the end of your tether, and the overview button (or gamepad Start), which
    /// drops freestyle. Another pass through this toy ends it too. The toy's body and label flip to
    /// show which way the next pass will toggle it.
    ///
    /// The belt itself is a closed system: its whole conserved stock is built once, behind a load
    /// veil, on the first wander, and every arrival after that is transport. No score, no end
    /// condition — wander as long as you like.
    /// </summary>
    public class ConveyorToy : Toy
    {
        ConveyorConfig _cfg;
        MicrosceneConveyor _conveyor;
        WanderwayRun _run;
        bool _conveyorPrimed;   // the stock is built ONCE - a later wander resumes, never re-primes
        TMP_Text _label;
        MeshRenderer _body;
        Color _accent = Color.white;

        public void Configure(ConveyorConfig cfg) => _cfg = cfg;

        protected override void OnInitialized()
        {
            _label = GetComponentInChildren<TMP_Text>(true);
            _body = GetComponentInChildren<MeshRenderer>(true);
            if (Definition) _accent = Definition.AccentColor;

            // Show the "off" affordance from the start so the first pass reads as a switch.
            if (_label)
                _label.text = $"{DisplayName}\n<size=60%>fly through to wander</size>";
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
                                   "Tools > Cosmic Shore > Setup Freestyle Toybox) to wire one.");

            EnsureConveyor();
            EnsureRun();

            // Order matters: the run reverts the cell FIRST (that swap raises the load veil and
            // retires the old world), so the belt's stock build joins the same hold instead of
            // stacking a second cover on top of it.
            _run.Begin(localVessel);

            // Begin BUILDS the belt's whole conserved stock; every later wander resumes the
            // stock that already exists. Keyed off an explicit flag, not IsRunning: a stopped belt
            // is not an unbuilt one, and re-priming would mint a second pool.
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
        /// Flip the toy's look so the player can read the wander state at a glance - and know the
        /// next pass toggles it the other way. ON = bright white-hot body + "wandering" label;
        /// OFF = the definition's accent + the invitation. Rebloom signals the in-place change
        /// (the established flip-set pattern).
        /// </summary>
        void ShowState(bool on)
        {
            if (_label)
                _label.text = on
                    ? $"{DisplayName}\n<size=60%>wandering - fly through to come home</size>"
                    : $"{DisplayName}\n<size=60%>fly through to wander</size>";

            if (_body && _body.sharedMaterial)
                _body.sharedMaterial.color = on ? Color.Lerp(_accent, Color.white, 0.55f) : _accent;

            Rebloom();
        }
    }
}
