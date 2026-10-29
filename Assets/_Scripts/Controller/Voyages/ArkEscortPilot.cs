using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An AI seat on an Ark escort (<see cref="ArkEscortRun"/>): the vessel's own <see cref="AIPilot"/> flies, this only
    /// says where to. Each seat hunts a different one of the hostile creatures nearest the Ark (seat 0 the nearest,
    /// seat 1 the next...), so a squad spreads over the threats instead of chasing one; with none in reach it holds its
    /// own station around the Ark, ahead of the bow on the side it was seated, where the next world's creatures come from.
    /// </summary>
    public sealed class ArkEscortPilot : MonoBehaviour
    {
        /// <summary>How far from the Ark a creature counts as a threat to hunt.</summary>
        public const float GuardRadius = 900f;

        const float RetargetSeconds = 0.5f;
        const float StationRadius = 260f;

        ArkEscortRun _run;
        AIPilot _pilot;
        int _seat;
        float _nextRetarget;
        Fauna _target;
        readonly List<Fauna> _threats = new();

        public void Arm(ArkEscortRun run, int seat)
        {
            _run = run;
            _seat = seat;
            _target = null;
            _nextRetarget = 0f;
            if (!_pilot) TryGetComponent(out _pilot);
            if (_pilot) _pilot.SetExternalTargetProvider(Target);
        }

        public void Disarm()
        {
            if (_pilot) _pilot.ClearExternalTargetProvider();
            _run = null;
            _target = null;
        }

        Vector3 Target()
        {
            var ark = _run ? _run.Ark : null;
            if (!ark) return transform.position + transform.forward * 100f;

            if (Time.time >= _nextRetarget || !_target)
            {
                _nextRetarget = Time.time + RetargetSeconds;
                _run.CountThreats(ark.Position, GuardRadius, _threats);
                _target = _threats.Count > 0 ? _threats[Mathf.Min(_seat, _threats.Count - 1)] : null;
            }
            // Fly at the creature's heart: a faster vessel through the heart jousts it (Fauna.Jousted), the one hit that
            // kills a grazer outright whatever the hull's guns.
            if (_target && _target.IsAlivePrey)
            {
                var heart = _target.LivingHeart;
                return heart ? heart.transform.position : _target.transform.position;
            }

            float angle = 120f * _seat + 30f;
            var side = Quaternion.AngleAxis(angle, ark.Forward) * ark.transform.up;
            return ark.Position + ark.Forward * StationRadius + side * StationRadius;
        }

        void OnDestroy() => Disarm();
    }
}
