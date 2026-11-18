using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The two Ark alarms of Frogma Rev 113's toasts (Compare: "ARK UNDER ATTACK", "BREACH CLOSE"), raised from the Ark's
    /// own signals (<see cref="ArkVoyageSignals.Signal"/>) for the escort and a job voyage alike, through the run's one
    /// banner path (<see cref="VoyageToasts"/>). The words are authored (<see cref="VoyageLines"/>); the rule is
    /// <see cref="Decide"/>, a pure function an edit-mode test runs.
    /// </summary>
    public sealed class ArkAlarmToasts
    {
        public enum Alarm
        {
            None = 0,
            UnderAttack = 1,
            BreachClose = 2,
        }

        /// <summary>What the alarms remember between signals.</summary>
        public struct Memory
        {
            /// <summary>When "under attack" last showed (unscaled seconds; it repeats no sooner than
            /// <see cref="UnderAttackRepeatSeconds"/>).</summary>
            public float UnderAttackAt;
            /// <summary>"Breach close" showed and the hull has not climbed back out of the margin since.</summary>
            public bool BreachShown;
        }

        /// <summary>"Breach close" shows when the hull is within this share above the breach line.</summary>
        public const float BreachMargin = 0.1f;
        public const float UnderAttackRepeatSeconds = 20f;

        /// <summary>
        /// The alarm <paramref name="signal"/> raises at <paramref name="now"/>, if any: the hull dropping within
        /// <see cref="BreachMargin"/> of a breach line (once, until it climbs back out), else the Ark starting to lose
        /// plates (no sooner than <see cref="UnderAttackRepeatSeconds"/> after the last time).
        /// </summary>
        public static Alarm Decide(ref Memory memory, ArkEscortSignalKind kind, in ArkEscortStatus status, float now)
        {
            if (!status.Active) return Alarm.None;
            bool close = status.BreachLine > 0f && status.HullTotal > 0 && status.Hull < status.BreachLine + BreachMargin;
            if (!close) memory.BreachShown = false;
            if (kind is ArkEscortSignalKind.HullChanged or ArkEscortSignalKind.ArkUnderAttack && close && !memory.BreachShown)
            {
                memory.BreachShown = true;
                return Alarm.BreachClose;
            }
            if (kind != ArkEscortSignalKind.ArkUnderAttack) return Alarm.None;
            if (memory.UnderAttackAt > 0f && now - memory.UnderAttackAt < UnderAttackRepeatSeconds) return Alarm.None;
            memory.UnderAttackAt = now;
            return Alarm.UnderAttack;
        }

        readonly IArkStatusSource _source;
        readonly System.Func<ArkwayRun> _sea;
        Memory _memory;
        bool _hooked;

        public ArkAlarmToasts(IArkStatusSource source, System.Func<ArkwayRun> sea)
        {
            _source = source;
            _sea = sea;
        }

        public void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            _memory = default;
            ArkVoyageSignals.Signal += OnSignal;
        }

        public void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            ArkVoyageSignals.Signal -= OnSignal;
        }

        void OnSignal(ArkEscortSignal signal)
        {
            if (!ReferenceEquals(ArkVoyageSignals.Current, _source)) return;
            var alarm = Decide(ref _memory, signal.Kind, signal.Status, UnityEngine.Time.unscaledTime);
            if (alarm == Alarm.None) return;
            var content = VoyageContentSO.Load();
            if (!content) return;
            var lines = content.Lines;
            var s = signal.Status;
            if (alarm == Alarm.BreachClose)
                VoyageToasts.Show(_sea(), ArkwayVoyageHud.Mood.Threat, lines.ToastBreachClose,
                    TemplateText.Fill(lines.ToastBreachCloseSub, "hull", UnityEngine.Mathf.RoundToInt(s.Hull * 100f).ToString(),
                        "breach", UnityEngine.Mathf.RoundToInt(s.BreachLine * 100f).ToString()));
            else
                VoyageToasts.Show(_sea(), ArkwayVoyageHud.Mood.Threat, lines.ToastArkUnderAttack,
                    TemplateText.Fill(lines.ToastArkUnderAttackSub, "threats", s.ThreatsNear.ToString()));
        }
    }
}
