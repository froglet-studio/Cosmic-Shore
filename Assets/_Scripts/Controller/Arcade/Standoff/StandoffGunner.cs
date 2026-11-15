using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Standoff: a GUNNER that makes passes on rival Grizzlies and falls back behind the posts.
    /// Server-only.
    ///
    /// <para><b>Steering only - the Grizzly's kit is untouched, and it presses nothing.</b> An AI Grizzly already has a
    /// weapon it uses on its own: the hull's autopilot bomb-jump (<c>GrizzlyTriggerBombExecutor.AutopilotDrive</c>,
    /// GRIZZLY_TRIGGER_BOMBS.md) fires a full trigger bomb whenever the stick is straight, freezes it just ahead, flies
    /// past it and blows it - through the replicated press and release, so every peer runs the same bomb. A full blast
    /// reaches 100 u and is a scored MissileBlast hit on every rival inside it. So the gunner only answers
    /// <see cref="AIPilot.SetExternalTargetProvider"/> with where to fly: a straight line that passes a rival inside a
    /// blast's reach IS an attack. A second presser would fight the bomb-jump (the Flatline lesson), so there is none.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Silenced and breaking off</b> (the rung's chance): behind the nearest post, on its far side from the
    /// centre, until the silence ends.</item>
    /// <item><b>A rival in range</b>: a pass beside the cheapest rival, led by its velocity, off its hull by the rung's
    /// pass offset (inside the blast's reach, outside its hull).</item>
    /// <item><b>Nobody in range</b>: work round the posts - fly to the post nearest the nose, behind it, then the next.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class StandoffGunner
    {
        sealed class Gunner
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 PassSide;
            public int Post = -1;
            public Vector3 Steer;

            public bool WasSilenced;
            public bool BreakingOff;
        }

        readonly List<Gunner> _gunners = new();
        StandoffSettingsSO _settings;
        GameDataSO _gameData;
        SpawnableStandoff _arena;
        Transform _frame;

        public bool IsArmed => _gunners.Count > 0;

        /// <summary>Arms every AI Grizzly on the roster round <paramref name="arena"/>.</summary>
        public void Arm(GameDataSO gameData, StandoffSettingsSO settings, SpawnableStandoff arena)
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

                var gunner = new Gunner
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 125)),
                    Steer = _frame.position,
                };
                _gunners.Add(gunner);
                var captured = gunner;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _gunners.Count; i++)
                if (_gunners[i].Autopilot) _gunners[i].Autopilot.ClearExternalTargetProvider();
            _gunners.Clear();
        }

        public void Tick()
        {
            if (_settings == null || !_arena) return;
            float now = Time.time;
            for (int i = 0; i < _gunners.Count; i++)
            {
                var h = _gunners[i];
                var self = h.Player?.Vessel?.Transform;
                if (!self || !h.Autopilot || !h.Autopilot.AutoPilotEnabled) continue;

                bool silenced = VesselSilence.IsSilenced(h.Status);
                if (silenced && !h.WasSilenced)
                    h.BreakingOff = h.Rng.NextDouble() <
                                    StandoffSettingsSO.Rung(_settings.aiBreakOffWhenSilencedChance, h.Level);
                if (!silenced) h.BreakingOff = false;
                h.WasSilenced = silenced;

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player)) Retarget(h, self, now);

                h.Steer = Goal(h, self);
            }
        }

        // ── What to fly at ──

        Vector3 Goal(Gunner h, Transform self)
        {
            if (h.BreakingOff) return Cover(NearestPost(self.position, self.forward, -1));

            if (h.Rival?.Vessel?.Transform is Transform rivalTf)
            {
                var status = h.Rival.Vessel.VesselStatus;
                float lead = StandoffSettingsSO.Rung(_settings.aiLeadSeconds, h.Level);
                Vector3 led = rivalTf.position + (status != null ? status.Course * status.Speed * lead : Vector3.zero);
                return led + h.PassSide * StandoffSettingsSO.Rung(_settings.aiPassOffset, h.Level);
            }

            // Nobody in range: post to post.
            if (h.Post < 0 ||
                (self.position - Cover(h.Post)).sqrMagnitude < _settings.aiPostReachedDistance * _settings.aiPostReachedDistance)
                h.Post = NearestPost(self.position, self.forward, h.Post);
            return Cover(h.Post);
        }

        /// <summary>The cover point of post <paramref name="post"/>: on its far side from the centre, in world space.</summary>
        Vector3 Cover(int post)
        {
            if (post < 0) return _frame.position;
            var d = _arena.PostDirection(post);
            return _frame.TransformPoint(d * (_arena.PostRadiusOf(post) + _settings.aiCoverStandoff));
        }

        /// <summary>The post nearest ahead of the hull (cheaper in front than behind), other than <paramref name="skip"/>.</summary>
        int NearestPost(Vector3 from, Vector3 forward, int skip)
        {
            int best = -1;
            float bestCost = float.MaxValue;
            for (int p = 0; p < _arena.PostCount; p++)
            {
                if (p == skip) continue;
                Vector3 to = Cover(p) - from;
                float distance = to.magnitude;
                float facing = distance > 1e-3f ? Vector3.Dot(forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = p;
            }
            return best;
        }

        // ── Rivals ──

        void Retarget(Gunner h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, StandoffSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
            h.Rival = ChooseRival(h, self);
            var d = new Vector3((float)(h.Rng.NextDouble() * 2 - 1), (float)(h.Rng.NextDouble() * 2 - 1),
                                (float)(h.Rng.NextDouble() * 2 - 1));
            h.PassSide = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
        }

        IPlayer ChooseRival(Gunner h, Transform self)
        {
            float huntRange = StandoffSettingsSO.Rung(_settings.aiHuntRange, h.Level);
            float silencedFocus = Mathf.Max(1f, StandoffSettingsSO.Rung(_settings.aiSilencedPreyFocus, h.Level));
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
                // A rival dead ahead costs its distance; one straight behind ~3x it.
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
            if (candidate.Domain == self.Domain) return false;   // a blast never scores on a team-mate
            return candidate.Vessel?.Transform != null;
        }
    }
}
