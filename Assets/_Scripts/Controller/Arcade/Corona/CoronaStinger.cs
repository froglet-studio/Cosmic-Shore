using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Corona: a STINGER that skims the shells for energy, grazes rival Mantas to plant bombs, and
    /// dives to the heart to cash them in on an omni crystal. Server-only.
    ///
    /// <para><b>Steering only - the Manta's kit is untouched, and it presses nothing.</b> Every one of the Manta's
    /// Charge verbs is passive: the skimmer arms a bomb from any prism it brushes, a graze plants it, an omni crystal
    /// sets the board off (MANTA_STING_KABLOOM.md §0). The hull's own autopilot drive Soars on a straight stick
    /// (<c>MantaAnalogTurnBoostExecutor</c>). So the stinger only answers <see cref="AIPilot.SetExternalTargetProvider"/>
    /// with where to fly, and reads the bomb bay (<see cref="MantaStingActionExecutor"/>) to decide it.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Silenced and breaking off</b> (the rung's chance): to the heart's nearest omni crystal.</item>
    /// <item><b>Bombs out and time to cash</b> (as many as the rung waits for, or the fuse short): to the nearest omni
    /// crystal. Every planted bomb blooms at once, and the bigger board blooms bigger.</item>
    /// <item><b>A bomb of energy</b>: pass the cheapest rival not already carrying a bomb, led by its velocity, off its
    /// hull by the rung's graze offset (a plant needs the skimmer on the rival and the hull off it).</item>
    /// <item><b>No energy</b>: fly along the nearest shell at the rung's standoff - inside skimmer reach, outside hull
    /// reach - so the tiles pay energy.</item>
    /// </list>
    ///
    /// <para><b>Routing.</b> A straight line through a shell meets tiles, and a Manta's hull touching a tile sets off
    /// every bomb. Every goal is reached through the arena's own gates (<see cref="SpawnableCorona.GateDirection"/>):
    /// line up on a gate's axis outside it, fly through, and bend round the inner shell when a chord would cut it.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class CoronaStinger
    {
        sealed class Stinger
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public MantaStingActionExecutor Bay;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 PassSide;
            public Vector2 GateJitter;
            public Vector3 Steer;

            public bool WasSilenced;
            public bool BreakingOff;
        }

        readonly List<Stinger> _stingers = new();
        CoronaSettingsSO _settings;
        GameDataSO _gameData;
        SpawnableCorona _arena;
        Transform _frame;

        public bool IsArmed => _stingers.Count > 0;

        /// <summary>Arms every AI Manta on the roster round <paramref name="arena"/>.</summary>
        public void Arm(GameDataSO gameData, CoronaSettingsSO settings, SpawnableCorona arena)
        {
            Disarm();
            if (gameData == null || settings == null || !arena) return;
            _gameData = gameData;
            _settings = settings;
            _arena = arena;
            _frame = arena.transform;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) continue;   // the card seats only Mantas

                var stinger = new Stinger
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Bay = bay,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 87)),
                    Steer = _frame.position,
                };
                _stingers.Add(stinger);
                var captured = stinger;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _stingers.Count; i++)
                if (_stingers[i].Autopilot) _stingers[i].Autopilot.ClearExternalTargetProvider();
            _stingers.Clear();
        }

        public void Tick()
        {
            if (_settings == null || !_arena) return;
            float now = Time.time;
            for (int i = 0; i < _stingers.Count; i++)
            {
                var h = _stingers[i];
                var self = h.Player?.Vessel?.Transform;
                if (!self || !h.Autopilot || !h.Autopilot.AutoPilotEnabled || !h.Bay) continue;

                bool silenced = VesselSilence.IsSilenced(h.Status);
                if (silenced && !h.WasSilenced)
                    h.BreakingOff = h.Rng.NextDouble() <
                                    CoronaSettingsSO.Rung(_settings.aiBreakOffWhenSilencedChance, h.Level);
                if (!silenced) h.BreakingOff = false;
                h.WasSilenced = silenced;

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player)) Retarget(h, self, now);

                Vector3 local = ToLocal(self.position);
                h.Steer = ToWorld(Route(h, local, ToLocal(Goal(h, self, local))));
            }
        }

        // ── What to fly at ──

        Vector3 Goal(Stinger h, Transform self, Vector3 local)
        {
            if (h.BreakingOff && NearestOmniCrystal(self.position) is Vector3 refuge) return refuge;

            int planted = h.Bay.PlantedBombs.Count;
            float fuse = h.Bay.FuseRemaining;
            bool cash = planted > 0 &&
                        (planted >= CoronaSettingsSO.Rung(_settings.aiCashAtBombs, h.Level) ||
                         (fuse >= 0f && fuse <= CoronaSettingsSO.Rung(_settings.aiCashAtFuseSeconds, h.Level)));
            if (cash && NearestOmniCrystal(self.position) is Vector3 crystal) return crystal;

            if (h.Bay.ArmedBombs > 0 && h.Rival?.Vessel?.Transform is Transform rivalTf)
            {
                var status = h.Rival.Vessel.VesselStatus;
                float lead = CoronaSettingsSO.Rung(_settings.aiLeadSeconds, h.Level);
                Vector3 led = rivalTf.position + (status != null ? status.Course * status.Speed * lead : Vector3.zero);
                return led + h.PassSide * CoronaSettingsSO.Rung(_settings.aiGrazeOffset, h.Level);
            }

            return ToWorld(SkimPoint(h, self, local));
        }

        /// <summary>A point on the nearest shell's near side, at the rung's standoff, a little ahead of the hull.</summary>
        Vector3 SkimPoint(Stinger h, Transform self, Vector3 local)
        {
            float r = local.magnitude;
            int shell = NearestShell(r);
            float radius = _arena.ShellRadius(shell);
            float standoff = CoronaSettingsSO.Rung(_settings.aiSkimStandoff, h.Level) + SpawnableCorona.TileThickness * 0.5f;
            float side = r < radius ? -1f : 1f;
            Vector3 ahead = local + _frame.InverseTransformDirection(self.forward) * _settings.aiSkimLookahead;
            if (ahead.sqrMagnitude < 1e-4f) ahead = local;
            return ahead.normalized * (radius + side * standoff);
        }

        // ── How to get there: through the gates ──

        /// <summary>
        /// The band a radius lies in: 0 inside the innermost shell (the heart), k between shell k-1 and shell k,
        /// ShellCount outside the outer shell.
        /// </summary>
        int Band(float r)
        {
            int b = 0;
            for (int s = 0; s < _arena.ShellCount; s++)
                if (r > _arena.ShellRadius(s)) b = s + 1;
            return b;
        }

        int NearestShell(float r)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int s = 0; s < _arena.ShellCount; s++)
            {
                float d = Mathf.Abs(r - _arena.ShellRadius(s));
                if (d >= bestD) continue;
                bestD = d;
                best = s;
            }
            return best;
        }

        Vector3 Route(Stinger h, Vector3 from, Vector3 to)
        {
            int bandFrom = Band(from.magnitude), bandTo = Band(to.magnitude);
            if (bandFrom == bandTo) return InBand(bandFrom, from, to);

            // Cross the one shell between this band and the next one toward the goal.
            bool outward = bandTo > bandFrom;
            int shell = outward ? bandFrom : bandFrom - 1;
            float radius = _arena.ShellRadius(shell);
            Vector3 gate = BestGate(shell, from, to);
            Vector3 jittered = Jitter(h, gate, radius);

            // Inside the gate's mouth already: aim through it. Otherwise line up on its axis first.
            float mouth = radius * Mathf.Sin(_arena.GateHalfAngle * Mathf.Deg2Rad);
            Vector3 offAxis = from - jittered * Vector3.Dot(from, jittered);   // how far the hull is off the gate's axis
            bool lined = Vector3.Dot(from, jittered) > 0f && offAxis.magnitude < mouth * 0.6f &&
                         Mathf.Abs(from.magnitude - radius) < _settings.aiGateApproach * 1.5f;
            float through = outward ? _settings.aiGateApproach : -_settings.aiGateApproach;
            if (lined) return jittered * (radius + through);
            return InBand(bandFrom, from, jittered * (radius - through));
        }

        /// <summary>The gate of <paramref name="shell"/> that costs least: near the hull, then near the goal.</summary>
        Vector3 BestGate(int shell, Vector3 from, Vector3 to)
        {
            Vector3 df = from.sqrMagnitude > 1e-4f ? from.normalized : Vector3.forward;
            Vector3 dt = to.sqrMagnitude > 1e-4f ? to.normalized : df;
            Vector3 best = df;
            float bestScore = float.MinValue;
            for (int g = 0; g < _arena.GatesPerShell; g++)
            {
                Vector3 d = _arena.GateDirection(shell, g);
                float score = 0.6f * Vector3.Dot(d, df) + 0.4f * Vector3.Dot(d, dt);
                if (score <= bestScore) continue;
                bestScore = score;
                best = d;
            }
            return best;
        }

        /// <summary>The gate direction moved off centre by this run's gate error, still inside the mouth.</summary>
        Vector3 Jitter(Stinger h, Vector3 gate, float radius)
        {
            float err = CoronaSettingsSO.Rung(_settings.aiGateError, h.Level) *
                        Mathf.Tan(_arena.GateHalfAngle * Mathf.Deg2Rad);
            var u = Vector3.Cross(gate, Mathf.Abs(gate.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(gate, u);
            return (gate + (u * h.GateJitter.x + v * h.GateJitter.y) * err).normalized;
        }

        /// <summary>
        /// A straight line inside one band, bent round the band's inner shell when the chord would dip into it (two
        /// points on a sphere are joined by a chord that sinks toward the centre).
        /// </summary>
        Vector3 InBand(int band, Vector3 from, Vector3 to)
        {
            if (band == 0) return to;   // the heart has nothing inside it
            float inner = _arena.ShellRadius(band - 1) + _settings.aiBandClearance;
            Vector3 ab = to - from;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-4f) return to;
            float t = Mathf.Clamp01(-Vector3.Dot(from, ab) / len2);
            if ((from + ab * t).magnitude >= inner) return to;
            // Bend: a third of the way round, at the middle of the band.
            float outer = band < _arena.ShellCount ? _arena.ShellRadius(band) : inner + 2f * _settings.aiGateApproach;
            float mid = 0.5f * (inner - _settings.aiBandClearance + outer);
            Vector3 dir = Vector3.Slerp(from.normalized, to.sqrMagnitude > 1e-4f ? to.normalized : from.normalized, 0.35f);
            return dir.normalized * mid;
        }

        // ── Rivals ──

        void Retarget(Stinger h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, CoronaSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
            h.Rival = ChooseRival(h, self);
            var d = new Vector3((float)(h.Rng.NextDouble() * 2 - 1), (float)(h.Rng.NextDouble() * 2 - 1),
                                (float)(h.Rng.NextDouble() * 2 - 1));
            h.PassSide = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
            float a = (float)(h.Rng.NextDouble() * 2 * System.Math.PI);
            h.GateJitter = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (float)h.Rng.NextDouble();
        }

        IPlayer ChooseRival(Stinger h, Transform self)
        {
            float huntRange = CoronaSettingsSO.Rung(_settings.aiHuntRange, h.Level);
            float silencedFocus = Mathf.Max(1f, CoronaSettingsSO.Rung(_settings.aiSilencedPreyFocus, h.Level));
            int selfBand = Band(ToLocal(self.position).magnitude);
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                var tf = candidate.Vessel.Transform;
                if (MantaBomb.IsBombed(tf.gameObject)) continue;   // one bomb per target: a bombed rival is denied
                Vector3 to = tf.position - self.position;
                float distance = to.magnitude;
                if (distance > huntRange) continue;
                // A rival dead ahead costs its distance; one straight behind ~3x it; each shell between costs half again.
                float facing = distance > 1e-3f ? Vector3.Dot(self.forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
                cost *= 1f + 0.5f * Mathf.Abs(Band(ToLocal(tf.position).magnitude) - selfBand);
                if (!candidate.IsInitializedAsAI) cost /= _settings.aiHumanFocus;
                if (VesselSilence.IsSilenced(candidate.Vessel.VesselStatus)) cost /= silencedFocus;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;   // a plant never takes a team-mate
            return candidate.Vessel?.Transform != null;
        }

        Vector3 ToLocal(Vector3 world) => _frame.InverseTransformPoint(world);
        Vector3 ToWorld(Vector3 local) => _frame.TransformPoint(local);

        /// <summary>The nearest live OMNI crystal (the Manta's cash-out), or null. Elemental crystals - every creature's
        /// heart included - are skipped.</summary>
        static Vector3? NearestOmniCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded || c.crystalProperties.IsElemental) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }
    }
}
