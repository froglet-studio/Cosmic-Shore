using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tollway's HUD objective arrow. It teaches the mode's loop in the order a player has to
    /// learn it, by answering a different question at each step:
    ///
    ///   1. You have a ball AND a ring standing → point at YOUR NEAREST RING. That is the whole
    ///      objective: the ball is easy to find (it is big, it is yours, you made it), the ring
    ///      is the thing you have to take it to.
    ///   2. You have a ball but no ring standing → point at the BALL. Planting a ring is a
    ///      button press, not a place to fly, so the only thing left to steer at is the payload.
    ///   3. Neither → point at the nearest OMNI crystal, the raw material of a ball.
    ///
    /// Omni only at step 3: an elemental crystal levels an element instead of forging, and
    /// pointing a new player at a fauna heart teaches them that crystals sometimes do not make
    /// balls, which is the wrong first lesson (the predicate is shared with Scarab Scramble
    /// rather than re-derived).
    ///
    /// Instantiated by <c>MiniGameHUD.CreateObjectiveProviderForGameMode</c> for
    /// <c>GameModes.Tollway</c> (the AstroLeague / DogFight / Scramble pattern; GameDataSO
    /// arrives through the DI injection that creation path runs). Crystals are read off the arena
    /// cell's own runtime data — the same registry the crystal manager writes its slots into.
    /// </summary>
    public class TollwayObjectiveProvider : MonoBehaviour, IObjectiveProvider
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

            // 1) & 2) — do you have a ball to move?
            var ball = FindNearestDomainBall(domain, from);
            if (ball != null)
            {
                // Measure the ring from the BALL, not from the ship: the arrow should name the
                // ring that ball is closest to, which is the one you would actually herd it into.
                var ring = TollwayController.NearestOwnRing(domain, ball.transform.position);
                target = ring ? ring.transform : ball.transform;
                return true;
            }

            // 3) — no ball: the nearest thing you can make one out of.
            var cellData = ResolveCellData(from);
            if (cellData != null && cellData.Crystals != null)
            {
                float bestSqr = float.MaxValue;
                for (int i = 0; i < cellData.Crystals.Count; i++)
                {
                    var crystal = cellData.Crystals[i];
                    if (!TollwayController.IsForgeSource(crystal)
                        || !crystal.gameObject.activeInHierarchy) continue;
                    float sqr = (crystal.transform.position - from).sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    target = crystal.transform;
                }
            }
            return target != null;
        }

        static AstroLeagueBall FindNearestDomainBall(Domains domain, Vector3 from)
        {
            AstroLeagueBall best = null;
            float bestSqr = float.MaxValue;
            var live = AstroLeagueBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (ball == null || ball.IsHidden || ball.IsFrozen) continue;
                if (ball.LastHitDomain != domain) continue;
                float sqr = (ball.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = ball;
            }
            return best;
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
