using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Waystation's garden ruins in the world (WAYSTATION.md, "Garden ruins"): every peer lays
    /// <see cref="WaystationRuins"/>' prisms from the broadcast course, in the walls' prism and in domain colours of
    /// their own, once a course, at I4 only. They are scenery with the game's own prism rules: a hull breaks them,
    /// nothing grows back, and they are returned to the pool with the walls. A wheel turns: its prisms are POSED here
    /// every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of its owner,
    /// not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class WaystationController
    {
        const string RuinOwnerId = "WaystationRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the course, so they read " +
                 "apart from the towers' walls and lanterns (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as " +
                 "the walls.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Lay the ruins of the course this peer was sent. Called once the walls' prism is known to be set.</summary>
        void LayRuins()
        {
            _ruinParts.Clear();
            _ruinWheels.Clear();
            WaystationRuins.Lay(_course, Intensity, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            WaystationRuins.RacingLine(_course, Intensity, _ruinLine);
            _ruinRoot = new GameObject($"{ModeName}Ruins").transform;
            _ruinRoot.SetParent(transform, false);   // the rings' frame, as the walls
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(wallPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle, in the course's frame.</summary>
        void TickRuins()
        {
            if (_ruinWheels.Count == 0) return;
            float seconds = RuinClock;
            for (int w = 0; w < _ruinWheels.Count; w++)
            {
                var wheel = _ruinWheels[w];
                for (int i = wheel.First; i < wheel.First + wheel.Count && i < _ruinPrisms.Count; i++)
                {
                    var prism = _ruinPrisms[i];
                    if (!prism || prism.destroyed || prism.ownerID != RuinOwnerId) continue;
                    SpearlineRuins.Pose(_ruinParts[i], wheel, seconds, out var position, out var forward, out var up);
                    prism.transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                }
            }
        }

        // ── The autopilot and the ruins ──────────────────────────────────────────────────────────────────────────────
        // Inputs only (2026-11-25, Yash: "AI is hitting prisms on the way a lot"). On a long gap the bot drifted
        // hundreds of units off the straight leg while it lined its belly up, then dropped through the ruins from the
        // side: the ruins keep a lane clear round the flown line, not round wherever a hull happens to be. So while
        // ruins stand, a bot does not drop with a ruin on its fall line, and with a ruin ahead it steers back into the
        // clear lane. A person's controls, the Drop and every vessel number are untouched; I1-I3 lay no ruins.

        [Header("AI and the ruins (I4)")]
        [Tooltip("How near a ruin prism's centre a bot's straight path may pass before the bot treats the path as " +
                 "blocked: it will not drop along it, and steers back into the lane the ruins keep clear (60 u). " +
                 "0 = the bots ignore the ruins.")]
        [SerializeField, Min(0f)] float aiRuinClearance = 24f;
        [Tooltip("How far ahead along its way a cruising bot looks for a ruin (u). The drop's fall line is checked " +
                 "along its whole length whatever this is.")]
        [SerializeField, Min(0f)] float aiRuinLookahead = 240f;

        readonly List<Vector3> _ruinPoints = new();
        readonly List<RaceGate> _ruinLine = new();
        int _ruinPointsFrame = -1;

        /// <summary>The standing ruin prisms' world positions, read once a frame (a wheel's prisms move).</summary>
        List<Vector3> RuinPoints()
        {
            if (_ruinPointsFrame == Time.frameCount) return _ruinPoints;
            _ruinPointsFrame = Time.frameCount;
            _ruinPoints.Clear();
            for (int i = 0; i < _ruinPrisms.Count; i++)
            {
                var prism = _ruinPrisms[i];
                if (!prism || prism.destroyed || prism.ownerID != RuinOwnerId) continue;
                _ruinPoints.Add(prism.transform.position);
            }
            return _ruinPoints;
        }

        bool AiMindsRuins => aiRuinClearance > 0f && _ruinPrisms.Count > 0;

        /// <summary>True when a standing ruin is on the straight fall from <paramref name="hull"/> to <paramref name="gate"/>.</summary>
        bool RuinOnFallLine(Vector3 hull, Vector3 gate) =>
            AiMindsRuins && WaystationRuins.FirstOnPath(RuinPoints(), hull, gate, aiRuinClearance) >= 0;

        /// <summary>
        /// With a ruin between the bot and <paramref name="goal"/>, the point to fly at instead: back in the clear lane
        /// of the leg it is on (the last ring to this ring, or to this ring's press point on a tower).
        /// </summary>
        bool TrySteerRoundRuins(IPlayer pilot, Vector3 goal, out Vector3 target)
        {
            target = goal;
            if (!AiMindsRuins) return false;
            var status = pilot?.Vessel?.VesselStatus;
            if (status == null || pilot.RoundStats == null) return false;
            Vector3 hull = status.Transform.position;
            if (!WaystationRuins.LaneLegTo(_ruinLine, pilot.RoundStats.SwitchesThreaded, out var legStart, out var legEnd))
            {
                // The first ring, or past the end: no leg to return to, so the lane is the way to the goal itself.
                Vector3 chord = RaceCourseGeometry.SafeNormalize(goal - hull, transform.forward);
                return WaystationRuins.TrySteerRound(RuinPoints(), hull, goal, new RaceGate(hull, chord, 0f),
                                                     new RaceGate(goal, chord, 0f), aiRuinClearance, aiRuinLookahead, out target);
            }
            return WaystationRuins.TrySteerRound(RuinPoints(), hull, goal, InWorld(legStart), InWorld(legEnd),
                                                 aiRuinClearance, aiRuinLookahead, out target);
        }

        /// <summary>A gate of the course's frame, in the world's.</summary>
        RaceGate InWorld(in RaceGate gate) =>
            new(transform.TransformPoint(gate.Position), transform.TransformDirection(gate.Axis), gate.Radius);

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
            {
                var prism = _ruinPrisms[i];
                if (!prism || prism.destroyed || prism.ownerID != RuinOwnerId) continue;
                if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinPoints.Clear();
            _ruinPointsFrame = -1;
            _ruinLine.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            if (_ruinRoot && _ruinRoot.childCount == 0) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
