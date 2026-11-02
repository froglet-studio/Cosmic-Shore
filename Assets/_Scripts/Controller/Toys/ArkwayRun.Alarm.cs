using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Two telegraphs the voyage adds over its own rules (Garrett's playtest, 2026-11-01: "i kept getting distracted and
    /// then dying before i realized there was a threat"):
    ///   • <b>The damage alarm.</b> Whenever the Ark loses a hull prism (<see cref="Ark.HullPrismLost"/>: a real loss, not a
    ///     payload taken from the hold) the objective arrow that names the Ark wears Fire, the danger colour, for
    ///     <see cref="DamageAlarmSeconds"/>; every further loss restarts it. While it burns the arrow also marks the Ark
    ///     when it is on screen and close, so the marker over the hull says "this is being hurt".
    ///   • <b>The leash shell</b> (<see cref="ArkLeashShell"/>): the leash drawn where it is, fading in as the pilot nears it.
    /// </summary>
    public sealed partial class ArkwayRun
    {
        /// <summary>Seconds the arrow burns after the latest hull loss.</summary>
        public const float DamageAlarmSeconds = 5f;

        /// <summary>Fire, the danger family (ElementalBarsConfigSO.FireColor, the Rhino's danger prisms).</summary>
        static readonly Color FireColour = new(1f, 0.3294f, 0.1020f, 1f);

        /// <summary>The leash shell's ghost colour: the neutral Blue family, pale and cold.</summary>
        static readonly Color LeashShellColour = new(0.26f, 0.52f, 0.85f, 1f);

        Ark _alarmArk;
        float _alarmUntil = -1f;
        bool _alarmShown;
        ArkLeashShell _leashShell;

        /// <summary>True while the damage alarm burns.</summary>
        public bool DamageAlarmActive => _alarmUntil >= 0f && Time.time < _alarmUntil;

        void HookDamageAlarm()
        {
            UnhookDamageAlarm();
            _alarmArk = _ark;
            if (_alarmArk) _alarmArk.HullPrismLost += OnHullPrismLostAlarm;
        }

        void UnhookDamageAlarm()
        {
            if (_alarmArk) _alarmArk.HullPrismLost -= OnHullPrismLostAlarm;
            _alarmArk = null;
            _alarmUntil = -1f;
            SetAlarmShown(false);
        }

        void OnHullPrismLostAlarm(Prism prism, PrismKind kind) => _alarmUntil = Time.time + DamageAlarmSeconds;

        void LateUpdate()
        {
            if (!_arrow) return;
            bool burning = _running && DamageAlarmActive;
            if (!burning && _alarmUntil >= 0f && Time.time >= _alarmUntil) _alarmUntil = -1f;
            SetAlarmShown(burning);
        }

        void SetAlarmShown(bool shown)
        {
            if (!_arrow) { _alarmShown = false; return; }
            if (shown == _alarmShown) return;
            _alarmShown = shown;
            _arrow.ShowWhenOnScreen = shown;
            if (shown) _arrow.SetAccent(FireColour);
            else _arrow.ClearAccent();
        }

        // ── The leash shell ──────────────────────────────────────────────────

        void EnsureLeashShell()
        {
            if (_leashShell) return;
            _leashShell = ArkLeashShell.Create(transform, LeashShellColour);
        }

        void DestroyLeashShell()
        {
            if (!_leashShell) return;
            Destroy(_leashShell.gameObject);
            _leashShell = null;
        }

        /// <summary>The leash this tick, drawn: the shell sits on the Ark at the leash radius and shows by how near the
        /// pilot is to it, pulsing while they are past it.</summary>
        void TrackLeashShell(float leash, Vector3 pilot)
        {
            EnsureLeashShell();
            if (_leashShell && _ark) _leashShell.Track(_ark.Position, leash, pilot, _leashBreachedAt >= 0f);
        }
    }
}
