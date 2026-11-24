using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Kelpwood: a HUNTER that locks rival pilots with the Angler's eraser. Server-only.
    ///
    /// <para><b>Input-only - the Angler's kit is untouched.</b> A lock is the Angler's own Charge: while the left
    /// trigger is held, each time the eraser's 1.5 degree beam comes onto a rival vessel one creature leaves the school
    /// and hunts it, and every bite is a Strike (<c>AnglerCommandExecutor.UpdateBeamLock</c>, ANGLER.md §1e). The hunter
    /// steers the nose onto a rival through <see cref="AIPilot.SetExternalTargetProvider"/> and, while the rival is
    /// inside the rung's gate, HOLDS the left trigger on the hull's <see cref="IInputStatus"/> - the channel a pad
    /// writes, replicated like every input, and the same way the hull's own autopilot
    /// (<c>AnglerLeapfrogAIPolicySO</c>, on the prefab) writes the right trigger. It reads the executor's school count
    /// and writes nothing else. The leapfrog keeps its own throws and dashes toward the same target.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Short of creatures</b> (fewer than <see cref="KelpwoodSettingsSO.aiRefuelBelowSchool"/>): fly to the
    /// nearest omni crystal - each is one creature for an Angler (AnglerTameByCrystalEffect).</item>
    /// <item><b>Silenced</b> (it cannot score until the silence ends): roll the rung's chance to break off - to the
    /// nearest omni crystal, or straight away from its rival through the kelp.</item>
    /// <item><b>Otherwise pick a rival</b> (the cheapest by distance and bearing, cheaper if human or silenced), put the
    /// nose on its led position off by the rung's aim error, and press the eraser for
    /// <see cref="KelpwoodSettingsSO.aiLockHoldSeconds"/> when the rival is inside the gate, the interval has passed
    /// and fewer than the rung's hunters are out.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class KelpwoodHunter
    {
        sealed class Hunter
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public AnglerCommandExecutor Angler;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 AimError;
            public Vector3 Steer;

            public bool Pressing;
            public float ReleaseAt;
            public float NextLockAt;

            public bool WasSilenced;
            public bool BreakingOff;
        }

        readonly List<Hunter> _hunters = new();
        KelpwoodSettingsSO _settings;
        GameDataSO _gameData;
        Vector3 _centre;
        float _kelpRadius;

        public bool IsArmed => _hunters.Count > 0;

        /// <summary>Arms every AI Angler on the roster around the kelp ball at <paramref name="centre"/>.</summary>
        public void Arm(GameDataSO gameData, KelpwoodSettingsSO settings, Vector3 centre, float kelpRadius)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            _kelpRadius = kelpRadius;

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
                var angler = status.Transform.GetComponentInChildren<AnglerCommandExecutor>(true);
                if (!angler) continue;   // the card seats only Anglers; anything else flies the platform's steering

                var hunter = new Hunter
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Angler = angler,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 86)),
                    Steer = centre,
                };
                _hunters.Add(hunter);
                var captured = hunter;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Lets go of every trigger and hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                SetEraser(h, false);
                if (h.Autopilot) h.Autopilot.ClearExternalTargetProvider();
            }
            _hunters.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                var self = h.Player?.Vessel?.Transform;
                if (!self || !h.Autopilot || !h.Autopilot.AutoPilotEnabled || !h.Angler)
                {
                    SetEraser(h, false);
                    continue;
                }

                if (h.Pressing && now >= h.ReleaseAt) SetEraser(h, false);

                bool silenced = VesselSilence.IsSilenced(h.Status);
                if (silenced && !h.WasSilenced)
                    h.BreakingOff = h.Rng.NextDouble() <
                                    KelpwoodSettingsSO.Rung(_settings.aiBreakOffWhenSilencedChance, h.Level);
                if (!silenced) h.BreakingOff = false;
                h.WasSilenced = silenced;

                bool shortOfCreatures = h.Angler.TamedCount < _settings.aiRefuelBelowSchool;
                if (h.BreakingOff || shortOfCreatures)
                {
                    if (NearestOmniCrystal(self.position) is Vector3 crystal)
                    {
                        h.Steer = crystal;
                        continue;
                    }
                    if (h.BreakingOff)
                    {
                        h.Steer = AwayFromRival(h, self);
                        continue;
                    }
                }

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player))
                    Retarget(h, self, now);
                h.Steer = AimPoint(h, self);
                TryLock(h, self, now, silenced);
            }
        }

        void TryLock(Hunter h, Transform self, float now, bool silenced)
        {
            if (h.Pressing || silenced || now < h.NextLockAt) return;
            if (h.Angler.TamedCount <= 0) return;
            if (h.Angler.HunterCount >= KelpwoodSettingsSO.Rung(_settings.aiMaxHuntersOut, h.Level)) return;
            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return;
            Vector3 to = rivalTf.position - self.position;
            if (to.sqrMagnitude < 1e-4f) return;
            if (Vector3.Angle(self.forward, to) > KelpwoodSettingsSO.Rung(_settings.aiLockGateDegrees, h.Level)) return;

            SetEraser(h, true);
            h.ReleaseAt = now + _settings.aiLockHoldSeconds;
            h.NextLockAt = now + KelpwoodSettingsSO.Rung(_settings.aiLockIntervalSeconds, h.Level);
        }

        /// <summary>The eraser is the left trigger: the channel a pad writes, replicated to every peer.</summary>
        static void SetEraser(Hunter h, bool held)
        {
            if (!held && !h.Pressing) return;
            h.Pressing = held;
            var input = h.Status?.InputStatus;
            if (input != null) input.LeftTriggerAnalog = held ? 1f : 0f;
        }

        void Retarget(Hunter h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, KelpwoodSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
            h.Rival = ChooseRival(h, self);
            // A fresh aim error for this run: a random direction, at up to the rung's error.
            var d = new Vector3((float)(h.Rng.NextDouble() * 2 - 1), (float)(h.Rng.NextDouble() * 2 - 1),
                                (float)(h.Rng.NextDouble() * 2 - 1));
            h.AimError = (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right) *
                         (KelpwoodSettingsSO.Rung(_settings.aiAimError, h.Level) * (float)h.Rng.NextDouble());
        }

        IPlayer ChooseRival(Hunter h, Transform self)
        {
            float huntRange = KelpwoodSettingsSO.Rung(_settings.aiHuntRange, h.Level);
            float silencedFocus = Mathf.Max(1f, KelpwoodSettingsSO.Rung(_settings.aiSilencedPreyFocus, h.Level));
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                Vector3 to = candidate.Vessel.Transform.position - self.position;
                float distance = to.magnitude;
                if (distance > huntRange) continue;
                // A rival dead ahead costs its distance; one straight behind costs ~3x it.
                float facing = distance > 1e-3f ? Vector3.Dot(self.forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
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
            if (candidate.Domain == self.Domain) return false;   // a lock never takes a team-mate
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>Where the nose goes: the led rival, off by this run's aim error. With nobody in range, the core.</summary>
        Vector3 AimPoint(Hunter h, Transform self)
        {
            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = h.Rival.Vessel.VesselStatus;
            float lead = KelpwoodSettingsSO.Rung(_settings.aiLeadSeconds, h.Level);
            return rivalTf.position + h.AimError + (status != null ? status.Course * status.Speed * lead : Vector3.zero);
        }

        /// <summary>A point across the kelp from the rival, inside the ball: the escape when there is no crystal.</summary>
        Vector3 AwayFromRival(Hunter h, Transform self)
        {
            var rivalTf = h.Rival?.Vessel?.Transform;
            Vector3 away = rivalTf ? self.position - rivalTf.position : self.position - _centre;
            if (away.sqrMagnitude < 1e-4f) away = self.forward;
            return _centre + away.normalized * (_kelpRadius * 0.8f);
        }

        /// <summary>The nearest live OMNI crystal (one creature for an Angler), or null. Elemental crystals - every
        /// creature's heart included - are skipped.</summary>
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
