using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scarab Scramble's HUD objective arrow: point at the nearest LIVE ball owned by the
    /// local player's domain (the thing to escort), else the nearest crystal (the thing to
    /// forge one from). That one arrow teaches the mode's whole loop — fetch, roll, score —
    /// without a word of tutorial, which is the point for a beachhead mode.
    ///
    /// Instantiated by <c>MiniGameHUD.CreateObjectiveProviderForGameMode</c> for
    /// <c>GameModes.ScarabScramble</c> (the AstroLeague/DogFight pattern; GameDataSO arrives
    /// through the DI injection that creation path runs). Crystals are read off the arena
    /// cell's own runtime data (<c>Cell.RuntimeData</c>), resolved once — the same registry
    /// the crystal manager writes its slots into.
    /// </summary>
    public class ScarabScrambleObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        CellRuntimeDataSO _cellData;
        bool _cellResolved;

        public bool TryGetObjective(out Transform target)
        {
            target = null;

            if (gameData == null) return false;
            var localPlayer = gameData.LocalPlayer;
            var vesselTf = localPlayer?.Vessel?.Transform;
            if (vesselTf == null) return false;

            Vector3 from = vesselTf.position;
            Domains domain = localPlayer.Domain;
            float bestSqr = float.MaxValue;

            // 1) Your team's nearest live ball — the thing to push.
            var live = AstroLeagueBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (ball == null || ball.IsHidden || ball.IsFrozen) continue;
                if (ball.LastHitDomain != domain) continue;
                float sqr = (ball.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                target = ball.transform;
            }
            if (target != null) return true;

            // 2) No ball yet — the nearest FORGE-SOURCE crystal, the raw material of one.
            // Omni only: pointing a new player at an elemental heart teaches them that
            // crystals sometimes don't make balls, which is the wrong first lesson.
            var cellData = ResolveCellData(from);
            if (cellData != null && cellData.Crystals != null)
            {
                for (int i = 0; i < cellData.Crystals.Count; i++)
                {
                    var crystal = cellData.Crystals[i];
                    if (!ScarabScrambleController.IsForgeSource(crystal)
                        || !crystal.gameObject.activeInHierarchy) continue;
                    float sqr = (crystal.transform.position - from).sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    target = crystal.transform;
                }
            }
            return target != null;
        }

        CellRuntimeDataSO ResolveCellData(Vector3 near)
        {
            if (_cellResolved) return _cellData;
            var cell = Cell.FindNearestActiveCell(near);
            if (cell == null) return null; // not resolved yet — retry next query
            _cellData = cell.RuntimeData;
            _cellResolved = true;
            return _cellData;
        }
    }
}
