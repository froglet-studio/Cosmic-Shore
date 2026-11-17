using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI HELM of the Orca's three element games: it says where the hull flies, and nothing else. Server-only.
    ///
    /// <para><b>Steering only - the Orca's kit is untouched, and it presses nothing.</b> The Orca's three gunner seats
    /// already have their own AI (<c>OrcaCrewAI.cs</c>): each turns toward the nearest rival hull, hostile prism or
    /// crystal in its reach, fires its gun, launches and flies its missile, and the bomb seat bursts its bomb on a rival.
    /// The helm's own AI raises the shield against a closing missile and burns on a long straight. What no seat decides is
    /// WHERE THE HULL GOES, so every one of these games is played by putting the gunners in reach of the right thing. The
    /// helm answers <see cref="AIPilot.SetExternalTargetProvider"/> with that point (Corona's and Flatline's shape: a hull
    /// whose own seats already press its verbs gets a mode AI that only steers).</para>
    ///
    /// <para><b>The three goals.</b></para>
    /// <list type="bullet">
    /// <item><see cref="Goal.Hunt"/> (Pod Hunt): stand off the cheapest rival hull at the rung's range, swinging round it,
    /// so all three gunners have it inside their missiles' ~720 u reach and its own missiles have to come round cover.</item>
    /// <item><see cref="Goal.Shelf"/> (Ice Shelf): fly a closed loop round a home point of its own (one per AI, spread
    /// round the nucleus), so its trail is a ring the shield's dropped danger prisms can stand on.</item>
    /// <item><see cref="Goal.Raid"/> (Depth Charge): close on the nearest plant not in its colour, to the rung's range, so
    /// the gunners (who pick prisms inside 250 u) and the bomb are working on it.</item>
    /// </list>
    /// <para>In every goal, a crew whose magazines are all short goes for the nearest omni crystal first: one gives every
    /// seat a missile (<c>OrcaOmniCrystalEffectSO</c>).</para>
    ///
    /// <para><b>Any hull the autopilot flies is steered</b>, not only an AI player's: when a person takes a gunner seat the
    /// hull's own autopilot flies it, and that hull should go somewhere useful too. A hull a person is flying from the helm
    /// is left alone (<see cref="AIPilot.AutoPilotEnabled"/> is false and the provider is never asked).</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>); a person's hull flown
    /// by its autopilot uses the middle rung.</para>
    /// </summary>
    public sealed class OrcaArcadeHelm
    {
        public enum Goal { Hunt = 0, Shelf = 1, Raid = 2 }

        sealed class Helm
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public OrcaCrew Crew;
            public System.Random Rng;

            public IPlayer Rival;
            public Vector3 RaidPoint;
            public bool HasRaidPoint;
            public float NextRetargetAt;
            public float Side;
            public Vector3 Home;
            public int LoopIndex;
            public Vector3 Steer;
        }

        /// <summary>The middle rung, for a person's hull the autopilot flies while they are on a gun.</summary>
        const int HumanHullLevel = 2;

        readonly List<Helm> _helms = new();
        OrcaArcadeSettingsSO _settings;
        GameDataSO _gameData;
        Goal _goal;
        Vector3 _centre;
        float _homeRadius;

        public bool IsArmed => _helms.Count > 0;

        /// <summary>Arms every Orca on the roster. <paramref name="homeRadius"/> is how far from the centre the shelf
        /// goal spreads its home points (Ice Shelf passes the nucleus radius; the other goals ignore it).</summary>
        public void Arm(GameDataSO gameData, OrcaArcadeSettingsSO settings, Goal goal, Vector3 centre, float homeRadius)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _goal = goal;
            _centre = centre;
            _homeRadius = Mathf.Max(0f, homeRadius);

            var players = gameData.Players;
            int count = players.Count;
            for (int i = 0; i < count; i++)
            {
                var p = players[i];
                var status = p?.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                var crew = status.Transform.GetComponentInChildren<OrcaCrew>(true);
                if (!crew) continue;   // the cards seat only Orcas

                var helm = new Helm
                {
                    Player = p,
                    Level = p.IsInitializedAsAI ? (int)gameData.AIDifficultyFor(p) : HumanHullLevel,
                    Status = status,
                    Autopilot = autopilot,
                    Crew = crew,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 99 + (int)goal)),
                    Steer = centre,
                };
                helm.Side = helm.Rng.NextDouble() < 0.5 ? -1f : 1f;
                helm.Home = HomeFor(i, count);
                _helms.Add(helm);
                var captured = helm;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _helms.Count; i++)
                if (_helms[i].Autopilot) _helms[i].Autopilot.ClearExternalTargetProvider();
            _helms.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _helms.Count; i++)
            {
                var h = _helms[i];
                var self = h.Status?.Transform;
                if (!self || !h.Autopilot || !h.Autopilot.AutoPilotEnabled || !h.Crew) continue;

                if (ShortOfMissiles(h) && NearestOmniCrystal(self.position) is Vector3 crystal)
                {
                    h.Steer = crystal;
                    continue;
                }

                switch (_goal)
                {
                    case Goal.Hunt:
                        if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player))
                        {
                            h.NextRetargetAt = now + Retarget(h);
                            h.Rival = ChooseRival(h, self);
                        }
                        h.Steer = StandOff(h, self);
                        break;

                    case Goal.Shelf:
                        h.Steer = NextLoopPoint(h, self);
                        break;

                    case Goal.Raid:
                        if (now >= h.NextRetargetAt || !h.HasRaidPoint)
                        {
                            h.NextRetargetAt = now + Retarget(h);
                            h.HasRaidPoint = TryChooseRaid(h, self, out h.RaidPoint);
                        }
                        h.Steer = h.HasRaidPoint ? Approach(self.position, h.RaidPoint, Rung(_settings.aiStandoff, h) * 0.5f)
                                                 : _centre;
                        break;
                }
            }
        }

        float Rung(float[] ladder, Helm h) => OrcaArcadeSettingsSO.Rung(ladder, h.Level);

        float Retarget(Helm h) => Mathf.Max(0.2f, Rung(_settings.aiRetargetSeconds, h));

        bool ShortOfMissiles(Helm h)
        {
            for (int s = 1; s < OrcaCrew.Seats; s++)
                if (h.Crew.MissilesAt(s) >= _settings.aiRefuelBelowMissiles) return false;
            return true;
        }

        // ── Hunt ─────────────────────────────────────────────────────────────

        IPlayer ChooseRival(Helm h, Transform self)
        {
            float range = Rung(_settings.aiScanRange, h);
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                float distance = Vector3.Distance(candidate.Vessel.Transform.position, self.position);
                if (distance > range) continue;
                float cost = distance;
                if (!candidate.IsInitializedAsAI) cost /= _settings.aiHumanFocus;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;   // a blast spares the gunner's own domain
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>The rung's range off the led rival, swung 35 degrees to this helm's side so the hull circles it
        /// instead of ramming it. With nobody in range, the centre.</summary>
        Vector3 StandOff(Helm h, Transform self)
        {
            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = h.Rival.Vessel.VesselStatus;
            var led = rivalTf.position + (status != null ? status.Course * status.Speed * Rung(_settings.aiLeadSeconds, h)
                                                         : Vector3.zero);
            var from = self.position - led;
            if (from.sqrMagnitude < 1e-4f) from = -rivalTf.forward;
            from.Normalize();
            var tangent = Vector3.Cross(from, rivalTf.up);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.Cross(from, Vector3.right);
            tangent.Normalize();
            const float swing = 35f * Mathf.Deg2Rad;
            var dir = (from * Mathf.Cos(swing) + tangent * (h.Side * Mathf.Sin(swing))).normalized;
            return led + dir * Rung(_settings.aiStandoff, h);
        }

        // ── Shelf ────────────────────────────────────────────────────────────

        /// <summary>Home points spread evenly round the centre at the home radius, one per roster seat, so two AIs do not
        /// lay their shelves on top of each other.</summary>
        Vector3 HomeFor(int index, int count)
        {
            if (_homeRadius <= 0f || count <= 0) return _centre;
            float a = 2f * Mathf.PI * index / count;
            return _centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (_homeRadius * 0.6f);
        }

        Vector3 LoopPoint(Helm h, int k)
        {
            int n = Mathf.Max(3, _settings.aiShelfLoopPoints);
            float a = h.Side * 2f * Mathf.PI * (k % n) / n;
            return h.Home + new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.5f) * 0.25f, Mathf.Sin(a)) * _settings.aiShelfLoopRadius;
        }

        /// <summary>The loop point the helm is flying to, advanced when the hull is near it.</summary>
        Vector3 NextLoopPoint(Helm h, Transform self)
        {
            var point = LoopPoint(h, h.LoopIndex);
            if ((point - self.position).sqrMagnitude < 0.35f * 0.35f * _settings.aiShelfLoopRadius * _settings.aiShelfLoopRadius)
            {
                h.LoopIndex++;
                point = LoopPoint(h, h.LoopIndex);
            }
            return point;
        }

        // ── Raid ─────────────────────────────────────────────────────────────

        /// <summary>The nearest plant inside the rung's scan range that does not wear this helm's colour (its own colour
        /// never scores in a destruction race).</summary>
        bool TryChooseRaid(Helm h, Transform self, out Vector3 point)
        {
            point = default;
            float range = Rung(_settings.aiScanRange, h);
            float bestSqr = range * range;
            bool found = false;
            var own = h.Status.Domain;
            var live = FloraHeartRegistry.Live;
            if (live == null) return false;
            for (int i = 0; i < live.Count; i++)
            {
                var flora = live[i];
                if (!flora || flora.IsDying || flora.Domain == own) continue;
                var at = flora.HeartTransform ? flora.HeartTransform.position : flora.transform.position;
                float d = (at - self.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                point = at;
                found = true;
            }
            return found;
        }

        /// <summary>A point <paramref name="range"/> short of <paramref name="target"/> on the line from the hull.</summary>
        static Vector3 Approach(Vector3 from, Vector3 target, float range)
        {
            var to = target - from;
            float d = to.magnitude;
            return d <= range || d < 1e-3f ? target : target - to / d * range;
        }

        // ── Shared ───────────────────────────────────────────────────────────

        /// <summary>The nearest live OMNI crystal, or null. Elemental crystals are skipped (they do not reload).</summary>
        static Vector3? NearestOmniCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded || c.crystalProperties.Element != Element.Omni) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }
    }
}
