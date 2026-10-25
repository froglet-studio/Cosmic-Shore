using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI's sword hand. An autopiloted Rhino used to fly with its blade parked: the sword is
    /// posed by the triggers and nothing in <see cref="AIPilot"/> writes triggers, so an AI Rhino
    /// never swung, never energized, and therefore never popped a super-shield or laid an
    /// afterimage - the whole kit was a human-only feature. This writes the trigger MIRRORS
    /// (<c>InputStatus.LeftTriggerAnalog / RightTriggerAnalog</c>) the way a pilot's fingers do, on
    /// the machine that owns the AI's input, so the pose, the stance, the swipe recovery and the
    /// energize all run through the shipped sword code and replicate exactly as a human's do
    /// (<see cref="ShieldSwipeActionExecutor"/>).
    ///
    /// <para><b>What it decides</b>, every <see cref="thinkSeconds"/>:</para>
    /// <list type="bullet">
    /// <item><b>Energize</b> when the meter can pay and hardened mass is near (or the meter is
    /// nearly full and anything is in reach): hold both triggers - the chop stance - until the blade
    /// ignites.</item>
    /// <item><b>Swing</b> at the nearest hostile prism in reach: pull the trigger on its side for one
    /// stroke, release, and let the swipe recovery run before the next.</item>
    /// <item>Otherwise hold the blade at rest.</item>
    /// </list>
    /// <para>It never steers (the pilot owns the sticks) and never fights a human: it drives only
    /// while the vessel's autopilot is on, and lets go of both triggers the frame it stops.</para>
    /// </summary>
    public sealed class RhinoSwordAutopilot : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The sword's brain (ScaleSkimmerObject). Found under the vessel when empty.")]
        [SerializeField] ShieldSkimmerScaleDriver sword;

        [Tooltip("The Rhino's VesselStatus (this GameObject's).")]
        [SerializeField] VesselStatus vesselStatus;

        [Header("Decisions")]
        [Tooltip("Seconds between target searches.")]
        [SerializeField, Min(0.02f)] float thinkSeconds = 0.15f;

        [Tooltip("Reach searched for a target, as a multiple of the blade's current length.")]
        [SerializeField, Min(0.5f)] float reachMultiple = 1.1f;

        [Tooltip("How long one stroke holds its trigger, seconds.")]
        [SerializeField, Min(0.05f)] float strokeSeconds = 0.3f;

        [Tooltip("Pause after a stroke before the next, seconds - longer than the swipe recovery so a " +
                 "stroke is never swallowed by it.")]
        [SerializeField, Min(0f)] float restSeconds = 0.45f;

        [Header("Energize")]
        [Tooltip("Energize when the meter holds at least the energize cost plus this margin.")]
        [SerializeField, Range(0f, 1f)] float energizeMargin = 0.05f;

        [Tooltip("With no hardened mass near, energize anyway once the meter is this full.")]
        [SerializeField, Range(0f, 1f)] float energizeWhenFull = 0.75f;

        [Tooltip("Hardened (super-shielded) mass within this multiple of the reach makes energizing worth it.")]
        [SerializeField, Min(0.5f)] float hardenedSearchMultiple = 1.6f;

        [Tooltip("Give up holding the stance after this long, seconds (the blade could not ignite).")]
        [SerializeField, Min(0.5f)] float stanceTimeoutSeconds = 2f;

        [SerializeField] ShieldSkimmerScaleConfigSO swordConfig;

        enum Intent { Rest = 0, Stroke = 1, Recover = 2, Stance = 3 }

        readonly List<Prism> _nearby = new(64);
        Intent _intent;
        float _intentUntil;
        float _nextThink;
        float _strokeSign;
        bool _driving;

        IVesselStatus Status => vesselStatus;

        /// <summary>True while this component owns the trigger mirrors - the swipe executor follows
        /// them through an autopilot instead of dropping them.</summary>
        public bool DrivesTriggers => _driving;

        void Awake()
        {
            if (!vesselStatus) TryGetComponent(out vesselStatus);
            if (!sword) sword = GetComponentInChildren<ShieldSkimmerScaleDriver>(true);
        }

        void OnDisable() => LetGo();

        void Update()
        {
            var input = ShouldDrive() ? Status.InputStatus : null;
            if (input == null)
            {
                LetGo();
                return;
            }

            _driving = true;
            float now = Time.time;
            if (now >= _nextThink)
            {
                _nextThink = now + thinkSeconds;
                Think(now);
            }

            float lt = 0f, rt = 0f;
            switch (_intent)
            {
                case Intent.Stance:
                    lt = rt = 1f;
                    if (sword.IsEnergized || now >= _intentUntil) Begin(Intent.Rest, now, 0f);
                    break;
                case Intent.Stroke:
                    if (_strokeSign > 0f) rt = 1f; else lt = 1f;
                    if (now >= _intentUntil) Begin(Intent.Recover, now, restSeconds);
                    break;
                case Intent.Recover:
                    if (now >= _intentUntil) Begin(Intent.Rest, now, 0f);
                    break;
            }

            input.LeftTriggerAnalog = lt;
            input.RightTriggerAnalog = rt;
        }

        // The machine that writes this vessel's input: the server for an AI player (its owner), the
        // local machine off the network. A write anywhere else would land in InputStatus's local
        // fallback and do nothing, so this is a correctness gate as much as a cost one.
        bool ShouldDrive()
        {
            if (!sword || vesselStatus == null || !Status.AutoPilotEnabled) return false;
            var player = Status.Player;
            if (player == null) return false;
            return player.IsNetworkOwner || player is not NetworkBehaviour { IsSpawned: true };
        }

        void Think(float now)
        {
            // A stroke or a stance plays out; a recover waits. Only a resting hand picks anew.
            if (_intent != Intent.Rest) return;

            var swing = sword.Skimmer ? sword.Skimmer.SwingKinematics : null;
            var index = PrismSpatialIndex.Instance;
            if (swing == null || !swing.IsReady || index == null || !index.IsAvailable) return;

            Transform ship = Status.ShipTransform;
            float reach = Mathf.Max(5f, 2f * swing.HalfLength) * reachMultiple;

            index.QuerySphere(ship.position, reach * hardenedSearchMultiple, _nearby);

            Prism nearest = null;
            float nearestSq = reach * reach;
            bool hardenedNear = false;
            var domain = Status.Domain;
            for (int i = 0; i < _nearby.Count; i++)
            {
                var p = _nearby[i];
                if (!p || p.destroyed || p.Domain == domain || sword.IsOwnAfterimage(p)) continue;
                if (p.prismProperties is { IsSuperShielded: true }) hardenedNear = true;
                float sq = (p.transform.position - ship.position).sqrMagnitude;
                if (sq < nearestSq)
                {
                    nearestSq = sq;
                    nearest = p;
                }
            }
            _nearby.Clear();

            if (CanEnergize() && (hardenedNear || (nearest && sword.Energy01 >= energizeWhenFull)))
            {
                Begin(Intent.Stance, now, stanceTimeoutSeconds);
                return;
            }

            if (!nearest) return;
            float side = ship.InverseTransformPoint(nearest.transform.position).x;
            _strokeSign = side >= 0f ? 1f : -1f;
            Begin(Intent.Stroke, now, strokeSeconds);
        }

        bool CanEnergize()
        {
            if (sword.EnergizePhase != RhinoSwordEnergizePhase.Idle) return false;
            float cost = swordConfig ? swordConfig.EnergizeCostFraction : 0.1f;
            return sword.Energy01 >= cost + energizeMargin;
        }

        void Begin(Intent intent, float now, float seconds)
        {
            _intent = intent;
            _intentUntil = now + seconds;
        }

        void LetGo()
        {
            if (!_driving) return;
            _driving = false;
            _intent = Intent.Rest;
            var input = vesselStatus != null ? Status.InputStatus : null;
            if (input == null) return;
            input.LeftTriggerAnalog = 0f;
            input.RightTriggerAnalog = 0f;
        }
    }
}
