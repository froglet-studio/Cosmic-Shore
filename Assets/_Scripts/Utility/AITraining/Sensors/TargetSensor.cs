using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Picks a target each frame based on the configured TargetMode. The training
    /// pilot owns one TargetSensor; the scenario chooses which mode it should run in.
    ///
    /// Crystals come from <see cref="Crystal.Active"/> filtered by
    /// <see cref="Crystal.CanBeCollected"/> — the same live set Skim Race scoring
    /// uses. Enemies come from GameDataSO.Players.
    /// </summary>
    public class TargetSensor : ITrainingSensor
    {
        public enum TargetMode
        {
            ClosestCrystal = 0,
            ClosestEnemyVessel = 1,
            BothPreferCrystal = 2,
            BothPreferEnemy = 3,
        }

        readonly CellRuntimeDataSO _cellData;
        readonly GameDataSO _gameData;
        public TargetMode Mode = TargetMode.ClosestCrystal;

        IVessel _vessel;
        Vector3 _lastTargetPos;
        Vector3 _lastTargetVel;

        public TargetSensor(CellRuntimeDataSO cellData, GameDataSO gameData)
        {
            _cellData = cellData;
            _gameData = gameData;
        }

        public void Bind(IVessel vessel) => _vessel = vessel;

        public void OnEpisodeStart()
        {
            _lastTargetPos = Vector3.zero;
            _lastTargetVel = Vector3.zero;
        }

        public void Sample(DecisionContext ctx)
        {
            ctx.HasTarget = false;
            ctx.TargetKind = TargetKind.None;
            ctx.TargetPosition = Vector3.zero;
            ctx.TargetVelocity = Vector3.zero;

            bool wantCrystal = Mode == TargetMode.ClosestCrystal
                            || Mode == TargetMode.BothPreferCrystal
                            || Mode == TargetMode.BothPreferEnemy;
            bool wantEnemy = Mode == TargetMode.ClosestEnemyVessel
                          || Mode == TargetMode.BothPreferCrystal
                          || Mode == TargetMode.BothPreferEnemy;

            Vector3 myPos = ctx.Position;
            Domains myDomain = ctx.MyDomain;

            Vector3 crystalPos = default;
            float crystalDist = float.PositiveInfinity;
            if (wantCrystal)
            {
                // Crystal.Active is maintained OnEnable/OnDisable. The cell's item
                // list can hold stale / MissingReference entries across scene reuse
                // and can lag a collected crystal while it explodes — both read as
                // "no target" or a chase of a point already passed.
                var active = Crystal.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    Crystal crystal = active[i];
                    if (crystal == null) continue;
                    if (crystal.IsExploding) continue;
                    if (!crystal.CanBeCollected(myDomain)) continue;
                    float d = Vector3.SqrMagnitude(crystal.transform.position - myPos);
                    if (d < crystalDist) { crystalDist = d; crystalPos = crystal.transform.position; }
                }
            }

            Vector3 enemyPos = default;
            Vector3 enemyVel = default;
            float enemyDist = float.PositiveInfinity;
            if (wantEnemy && _gameData != null)
            {
                for (int i = 0; i < _gameData.Players.Count; i++)
                {
                    var p = _gameData.Players[i];
                    if (p == null || p.Vessel == null) continue;
                    if (p.Vessel == _vessel) continue;
                    if (p.Domain == myDomain && myDomain != Domains.Blue) continue;
                    Vector3 pos = p.Vessel.Transform.position;
                    float d = Vector3.SqrMagnitude(pos - myPos);
                    if (d < enemyDist)
                    {
                        enemyDist = d;
                        enemyPos = pos;
                        enemyVel = (pos - _lastTargetPos) / Mathf.Max(Time.deltaTime, 1e-3f);
                    }
                }
            }

            // Pick winner per mode.
            Vector3 chosen = default;
            Vector3 chosenVel = Vector3.zero;
            TargetKind kind = TargetKind.None;
            switch (Mode)
            {
                case TargetMode.ClosestCrystal:
                    if (!float.IsPositiveInfinity(crystalDist)) { chosen = crystalPos; kind = TargetKind.Crystal; }
                    break;
                case TargetMode.ClosestEnemyVessel:
                    if (!float.IsPositiveInfinity(enemyDist)) { chosen = enemyPos; chosenVel = enemyVel; kind = TargetKind.EnemyVessel; }
                    break;
                case TargetMode.BothPreferCrystal:
                    if (!float.IsPositiveInfinity(crystalDist)) { chosen = crystalPos; kind = TargetKind.Crystal; }
                    else if (!float.IsPositiveInfinity(enemyDist)) { chosen = enemyPos; chosenVel = enemyVel; kind = TargetKind.EnemyVessel; }
                    break;
                case TargetMode.BothPreferEnemy:
                    if (!float.IsPositiveInfinity(enemyDist)) { chosen = enemyPos; chosenVel = enemyVel; kind = TargetKind.EnemyVessel; }
                    else if (!float.IsPositiveInfinity(crystalDist)) { chosen = crystalPos; kind = TargetKind.Crystal; }
                    break;
            }

            if (kind != TargetKind.None)
            {
                ctx.HasTarget = true;
                ctx.TargetPosition = chosen;
                ctx.TargetVelocity = chosenVel;
                ctx.TargetKind = kind;
                ctx.TargetRange = (chosen - myPos).magnitude;
                Vector3 dir = (chosen - myPos).normalized;
                ctx.ObjectiveDirection = dir;
                ctx.DotForwardObjective = Vector3.Dot(dir, ctx.Forward);
                _lastTargetPos = chosen;
            }
        }
    }
}
