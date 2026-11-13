using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The boot splash's bar (Rev 94 C, Docs/MENU_REDESIGN_B.md section 177.2): a <see cref="KitStepBar"/> that
    /// draws <see cref="BootLoadProgress"/>. One on the Bootstrap splash and one in the sign-in scene; both draw
    /// the same model (<see cref="Model"/>), so the percentage carries over when one scene hands the screen to
    /// the other.
    ///
    /// <para>It feeds the model from two places and measures nothing itself: the boot status channel (each
    /// status names the <see cref="BootStep"/> it starts) and, each frame it can be seen, the prism builder and
    /// the load veil. It draws nothing while nobody can see it (<see cref="KitSightline"/>): the splash spends the
    /// session faded out. A boot goes on being measured out of sight until it ends, so it always ends.</para>
    ///
    /// <para><b>The splash as the scene-transition overlay.</b> When the bar comes back into sight and no boot
    /// is filling it, a new load starts from 0. No boot step runs on a return to the menu, so that load is the
    /// world build alone over the whole bar: the boot's step labels are hidden and the authored
    /// <see cref="returnWords"/> line shows in place of a step's name.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Boot Step Bar")]
    public class BootStepBar : KitStepBar
    {
        [Header("Boot")]
        [Tooltip("Inbound: the boot status channel. Each status names the boot step it starts.")]
        [SerializeField] ScriptableEventBootStatusRequest requestEvent;

        [Header("Steps under the bar")]
        [Tooltip("The chrome theme the marks and labels are painted from.")]
        [SerializeField] UIThemeSO theme;
        [Tooltip("One mark per step: lit once the step has started, filled once it is done.")]
        [SerializeField] Image[] stepMarks = new Image[0];
        [Tooltip("One label per step: lit once the step has started.")]
        [SerializeField] TMP_Text[] stepWords = new TMP_Text[0];
        [Tooltip("A step's mark before the step is done.")]
        [SerializeField] Sprite markOpen;
        [Tooltip("A step's mark once the step is done.")]
        [SerializeField] Sprite markDone;
        [Tooltip("The row of marks and labels. Hidden on a load that is not a boot: those steps are not running.")]
        [SerializeField] CanvasGroup stepRow;

        [Header("A load that is not a boot")]
        [Tooltip("The running step's name over the bar (the base bar writes it). Hidden while the line below shows.")]
        [SerializeField] CanvasGroup bootWords;
        [Tooltip("The line shown in place of a step's name when the splash covers a return to the menu. " +
                 "Its words are authored on its text.")]
        [SerializeField] CanvasGroup returnWords;

        static BootLoadProgress s_model = new();

        /// <summary>The one progress of this boot or load, shared by every bar that draws it.</summary>
        public static BootLoadProgress Model => s_model;

        KitSightline _sight;
        bool _wasVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_model = new BootLoadProgress();

        void Awake() => _sight = new KitSightline(transform, null, null);

        void OnTransformParentChanged() => _sight?.Refresh(transform);

        void OnEnable() => requestEvent.OnRaised += HandleRequest;

        void OnDisable() => requestEvent.OnRaised -= HandleRequest;

        void HandleRequest(BootStatusRequest request)
        {
            if (request.Mode == BootStatusMode.Status) s_model.Report(request.Step);
        }

        // LateUpdate, not Update: the base bar's own Update moves its glint.
        void LateUpdate()
        {
            bool visible = _sight.Visible;
            if (visible && !_wasVisible && !s_model.BootInProgress) s_model.Reset();
            _wasVisible = visible;
            // Out of sight, a boot is still measured (four static reads, nothing drawn): a boot whose splash
            // lifts before the world settles must still end, or the next load would inherit a half-filled boot.
            if (!visible && !s_model.BootInProgress) return;

            float value = s_model.Tick(EnvironmentLoadVeil.IsCovering, PrismTrailBuilder.IsLayingInProgress,
                                       PrismTrailBuilder.LayProgress, PrismTrailBuilder.GrowRemainingCount);
            if (visible) Show(value, s_model.Done);
        }

        protected override void OnShown(float value01, int step, bool done)
        {
            bool boot = s_model.IsBoot;
            KitVisibility.SetShown(stepRow, boot);
            // Done shows the base bar's done word on either kind of load.
            KitVisibility.SetShown(bootWords, boot || done);
            KitVisibility.SetShown(returnWords, !boot && !done);

            Color lit = UITheme.Resolve(theme, UIColorToken.TextLight);
            Color waiting = UITheme.Resolve(theme, UIColorToken.NeutralLightest);
            var ends = BootLoadProgress.StepEnds;
            int count = Mathf.Min(ends.Length, Mathf.Max(stepMarks.Length, stepWords.Length));
            for (int i = 0; i < count; i++)
            {
                bool started = done || i <= step;
                bool finished = done || value01 >= ends[i];
                Color colour = started ? lit : waiting;
                if (i < stepWords.Length && stepWords[i]) stepWords[i].color = colour;
                if (i < stepMarks.Length && stepMarks[i])
                {
                    stepMarks[i].color = colour;
                    stepMarks[i].sprite = finished ? markDone : markOpen;
                }
            }
        }
    }
}
