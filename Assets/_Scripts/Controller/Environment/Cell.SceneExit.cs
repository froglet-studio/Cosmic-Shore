using System.Collections;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The cell's SCENE EXIT: handing its world away because the scene is about to be replaced (the Maelstrom
    /// hub's park before a round, and every scene load out of a match). A cell swap's own retire stays in
    /// Cell.cs; this part reuses its gather, its trail return and its prism-by-prism release.
    /// </summary>
    public partial class Cell
    {
        bool _retiringForSceneExit;

        /// <summary>True while <see cref="RetireWorldIntoPoolForSceneExit"/> is draining this cell's world.</summary>
        public bool IsRetiringForSceneExit => _retiringForSceneExit;

        /// <summary>
        /// True once <see cref="RetireWorldIntoPoolForSceneExit"/> has emptied this cell and nothing has stood a
        /// world in it since. <see cref="Config"/> still names the retired world, so a caller that wants it back
        /// asks <see cref="RequestCellSwap"/> for the same config (the reset) rather than trusting the name.
        /// </summary>
        public bool WorldRetiredForSceneExit { get; private set; }

        /// <summary>Environment prisms the last scene-exit retire handed to <see cref="EnvironmentPrismPool"/>.</summary>
        public int LastSceneExitParkedCount { get; private set; }

        /// <summary>Prisms the last scene-exit retire destroyed in slices (plants, anything the pool does not take).</summary>
        public int LastSceneExitDestroyedCount { get; private set; }

        /// <summary>
        /// Hand this cell's world to the prism pool because its SCENE is about to be replaced, so the next scene's
        /// lay re-issues the same prisms instead of cloning them again.
        ///
        /// <para><b>Why:</b> a scene unload destroys every prism in it, and the next scene clones its arena from
        /// nothing. The Maelstrom hub previews exactly the arena the round then lays, so a round cost two full lays
        /// of 13-15 thousand prisms with "Prisms reused from the environment pool 0" both times, each lay a long
        /// frame with Relay live (Docs/Reports/MAELSTROM_MULTIPLAYER_RUNS.md finding 1; Docs/PartySystem/BUGS.md
        /// B45). <see cref="EnvironmentPrismPool"/> outlives a scene load, so what is parked here is what the round
        /// pulls.</para>
        ///
        /// <para><b>What it does:</b> the first half of <see cref="RequestCellSwap"/> and nothing else: stop the
        /// spawner, detach every vessel's trail, gather the world under a retiring root, return the pooled trail to
        /// its pool, release the environment prisms to <see cref="EnvironmentPrismPool"/> a time slice per frame,
        /// reset the books. No rebuild. Lifeforms retire WITH the world (no death, no crystal, no sound), and a
        /// lifeform that is a spawned NetworkObject is left for the scene's own teardown. Pass
        /// <paramref name="suctionSeconds"/> whenever a camera may still draw the world: it then eases away first,
        /// as a swap's does (continuity of existence); zero is for a world already out of sight, the clause
        /// <see cref="StrikeSatelliteWorld"/> rides. Mass conservation holds: this is the scene load's own ending
        /// of the cell's mass, done a moment early (Docs/ECOSYSTEM.md §19).</para>
        ///
        /// <para>The drain runs on this cell, so it stops when the scene unloads; prisms not yet parked are
        /// destroyed with the scene exactly as before. A caller that wants the whole world parked waits on
        /// <see cref="IsRetiringForSceneExit"/> before it starts the load.</para>
        /// </summary>
        /// <param name="releaseSliceMs">Milliseconds of release work per frame. The world is already gone from
        /// sight when the release runs, and nobody is flying, so it can be longer than the swap's 12 ms.</param>
        /// <param name="suctionSeconds">Seconds the world takes to ease away before it is parked; 0 for none.</param>
        /// <param name="requireEnvironment">True (the Maelstrom hub): only a cell with an authored environment
        /// is retired, because the point there is to park that environment for the round. False (a scene load's
        /// own exit, SceneLoader): a world that is plants and trail only is retired too. Bloomrush ends with
        /// 60,000 plant prisms and no environment, and destroying them inside the load's frame was 3.4-3.7 s of a
        /// 9.5-10.5 s freeze with Relay live (B45, measured 2026-11-20).</param>
        /// <param name="rootDestroyPrismsPerFrame">0 (the Maelstrom hub) drains the world prism by prism, as a swap
        /// does. Above 0 (a scene load's exit): only the environment is drained that way; every plant, creature and
        /// shell is destroyed a whole root at a time, about this many prisms' worth a frame
        /// (<see cref="ReleaseRetiredWorldByRoot"/>).</param>
        /// <param name="returnPooledTrail">False leaves the vessels' pooled trail where it is. The host's scene
        /// loader passes false: its trail pools have already started their own release
        /// (<c>InteractivePrismPoolManager.HandleSceneTransition</c>), which stops tracking every prism at once, so a
        /// return asked for here would be ignored and the prism left loose in the scene to die with it.</param>
        /// <returns>False when nothing was started: not playing, a satellite, a swap or an exit retire already
        /// running, or no world standing.</returns>
        public bool RetireWorldIntoPoolForSceneExit(double releaseSliceMs = 24.0, float suctionSeconds = 0f,
            bool requireEnvironment = true, int rootDestroyPrismsPerFrame = 0, bool returnPooledTrail = true)
        {
            if (!Application.isPlaying) return false;
            if (IsSatellite) return false;          // a satellite's world is struck by its owner
            if (_swapping || _retiringForSceneExit) return false;
            // `environment == gameObject` is SpawnEnvironment's "a build is pending" sentinel.
            if (environment == gameObject) return false;
            if (!environment && (requireEnvironment || (spawnedLifeForms.Count == 0 && trackedBlocks.Count == 0)))
                return false;

            StartCoroutine(RetireForSceneExitRoutine(releaseSliceMs, suctionSeconds, rootDestroyPrismsPerFrame, returnPooledTrail));
            return true;
        }

        IEnumerator RetireForSceneExitRoutine(double releaseSliceMs, float suctionSeconds, int rootDestroyPrismsPerFrame,
            bool returnPooledTrail)
        {
            _retiringForSceneExit = true;
            LastSceneExitParkedCount = 0;
            LastSceneExitDestroyedCount = 0;
            // The flag is what a caller waits on, and what refuses a swap: a step that throws must not latch it.
            try
            {
                if (_deferredEnvironmentBuild != null)
                {
                    StopCoroutine(_deferredEnvironmentBuild);
                    _deferredEnvironmentBuild = null;
                }

                StopSpawner();
                // Left detached and paused: the scene is ending, nobody lays trail into it again.
                SetVesselTrailsDetached(pauseSpawners: true);

                // The world goes as a whole, so nothing in it DIES and nothing grows: stopped BEFORE the gather
                // clears the list. Without this each plant ran its full death path (sound, crystal) when the
                // root was destroyed with the scene still loaded, and growth coroutines went on pulling the
                // prisms this drain had just parked (LifeForm.RetireWithWorld; the Arkway strike's own remedy).
                for (int i = 0; i < spawnedLifeForms.Count; i++)
                {
                    if (!spawnedLifeForms[i]) continue;
                    foreach (var lifeForm in spawnedLifeForms[i].GetComponentsInChildren<LifeForm>(true))
                        if (lifeForm) lifeForm.RetireWithWorld();
                }

                // The gather below forgets the environment; the by-root drain needs to tell it from the plants.
                var environmentRoot = environment && environment != gameObject ? environment : null;
                var retiring = RetireWorldIntoSuctionRoot(clearLooseTrailMass: returnPooledTrail, out var pooledRetiring,
                    leaveSpawnedNetworkLifeForms: true);

                // Continuity of existence: a world a camera may still draw eases away before it is parked.
                if (suctionSeconds > 0.01f)
                    yield return SuctionRetiringWorld(retiring, suctionSeconds);

                yield return ReturnRetiredTrailToPool(pooledRetiring);
                if (rootDestroyPrismsPerFrame > 0)
                    yield return ReleaseRetiredWorldByRoot(retiring, environmentRoot, releaseSliceMs, rootDestroyPrismsPerFrame);
                else
                    yield return ReleaseRetiredWorld(retiring, releaseSliceMs);
                LastSceneExitParkedCount = _lastReleaseParkedCount;
                LastSceneExitDestroyedCount = _lastReleaseDestroyedCount;

                ResetBooksAfterRetire();

                WorldRetiredForSceneExit = true;
                if (CSDebug.IsVerbose(CSLogChannel.Ecology))
                    CSDebug.LogVerbose(CSLogChannel.Ecology,
                        $"[Cell {ID}] World retired for the scene exit: {LastSceneExitParkedCount} prisms parked in the pool.");
            }
            finally
            {
                _retiringForSceneExit = false;
            }
        }

        /// <summary>
        /// The scene exit's drain. Environment prisms go back to the pool one by one, as in
        /// <see cref="ReleaseRetiredWorld"/> (the next scene re-issues them). Everything else is destroyed a WHOLE
        /// ROOT at a time: each plant, each creature, the shells.
        ///
        /// <para>Why by root: destroying a plant prism by prism cost three times the engine's own teardown of the
        /// same plants, and left one 4.1 s frame when the emptied roots went (60,000 plant prisms at the end of a
        /// Bloomrush: 13 s in slices against 3.8 s inside the scene unload, measured 2026-11-20). A root goes down
        /// the engine's bulk path at the cost the unload pays; only the frame it lands in changes.</para>
        /// </summary>
        /// <param name="prismsPerFrame">Prisms' worth of roots destroyed a frame. A root larger than this goes alone.</param>
        IEnumerator ReleaseRetiredWorldByRoot(GameObject retiring, GameObject environmentRoot, double releaseSliceMs,
            int prismsPerFrame)
        {
            _lastReleaseParkedCount = 0;
            _lastReleaseDestroyedCount = 0;
            if (!retiring) yield break;

            int parked = 0, destroyed = 0;
            if (environmentRoot)
            {
                yield return ReleaseRetiredWorld(environmentRoot, releaseSliceMs);
                parked = _lastReleaseParkedCount;
                destroyed = _lastReleaseDestroyedCount;
            }

            // Newest first. Destroy is deferred to the end of the frame, so within a frame the indices below hold;
            // after a yield the destroyed roots (all above i) are gone and the rest keep theirs.
            var rootT = retiring ? retiring.transform : null;
            int budget = 0;
            for (int i = rootT ? rootT.childCount - 1 : -1; i >= 0; i--)
            {
                if (!retiring) yield break;
                if (i >= rootT.childCount) continue;   // something else took a root meanwhile

                var child = rootT.GetChild(i);
                int prisms = child.GetComponentsInChildren<Prism>(true).Length;
                destroyed += prisms;
                budget += Mathf.Max(1, prisms);
                Destroy(child.gameObject);

                if (budget >= prismsPerFrame)
                {
                    yield return null;
                    budget = 0;
                }
            }

            if (retiring) Destroy(retiring);
            _lastReleaseParkedCount = parked;
            _lastReleaseDestroyedCount = destroyed;
            yield return null; // let the deferred destroys land before the bookkeeping reset
        }
    }
}
