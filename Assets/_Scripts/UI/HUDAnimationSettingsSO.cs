using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    [CreateAssetMenu(
        fileName = "HUDAnimationSettings",
        menuName = "ScriptableObjects/UI/HUD Animation Settings")]
    public class HUDAnimationSettingsSO : ScriptableObject
    {
        [Header("Score Card — Entrance")]
        [Tooltip("Duration of the card scale+fade entrance when first created")]
        public float cardEntranceDuration = 0.3f;
        [Tooltip("Initial scale for the entrance pop (0 = invisible)")]
        public float cardEntranceStartScale = 0.6f;
        public Ease cardEntranceEase = Ease.OutBack;
        [Tooltip("Delay between each successive card entrance (stagger)")]
        public float cardEntranceStagger = 0.08f;

        [Header("Score Card — Score Punch")]
        [Tooltip("Scale overshoot when score changes (1.0 = no punch)")]
        public float scorePunchScale = 1.15f;
        [Tooltip("Duration of the punch pop")]
        public float scorePunchDuration = 0.2f;
        public Ease scorePunchEase = Ease.OutBack;

        [Header("Score Card — Counter Roll")]
        [Tooltip("Duration to animate from old score to new score")]
        public float counterRollDuration = 0.35f;
        public Ease counterRollEase = Ease.OutQuad;

        [Header("Score Card — Score Color Flash")]
        [Tooltip("Color flash when score increases")]
        public Color scoreGainColor = new Color(0.2f, 1f, 0.4f, 1f);
        [Tooltip("Color flash when score decreases")]
        public Color scoreLossColor = new Color(1f, 0.3f, 0.2f, 1f);
        [Tooltip("Duration of the color flash")]
        public float scoreColorFlashDuration = 0.4f;

        [Header("Countdown Timer")]
        [Tooltip("Ease for sprite scale grow during each countdown beat")]
        public Ease countdownScaleEase = Ease.OutQuad;
        [Tooltip("Alpha fade-in duration for each countdown sprite")]
        public float countdownFadeInDuration = 0.1f;
        [Tooltip("Tint applied during the final countdown sprite (index 3 = GO)")]
        public Color countdownUrgentColor = new Color(1f, 0.3f, 0.2f, 1f);
        [Tooltip("Index at which urgent color starts (0-based, 0=first sprite)")]
        public int countdownUrgentStartIndex = 2;

        [Header("HUD View Toggle")]
        [Tooltip("Duration for HUD canvas group fade in/out")]
        public float hudFadeDuration = 0.25f;
        public Ease hudFadeInEase = Ease.OutQuad;
        public Ease hudFadeOutEase = Ease.InQuad;

        [Header("Connecting Panel")]
        [Tooltip("Duration for connecting panel fade in/out")]
        public float connectingFadeDuration = 0.3f;

        [Header("Scoreboard")]
        [Tooltip("Duration for scoreboard panel entrance")]
        public float scoreboardEntranceDuration = 0.35f;
        [Tooltip("Vertical offset the scoreboard slides in from")]
        public float scoreboardSlideOffset = 120f;
        public Ease scoreboardEntranceEase = Ease.OutCubic;
        [Tooltip("Delay between each player row appearing")]
        public float scoreboardRowStagger = 0.1f;
        [Tooltip("Duration for banner text scale punch")]
        public float bannerPunchDuration = 0.3f;
        [Tooltip("Banner text punch scale overshoot")]
        public float bannerPunchScale = 1.2f;

        [Header("Vessel HUD")]
        [Tooltip("Duration for vessel HUD show/hide fade")]
        public float vesselHudFadeDuration = 0.2f;

        [Header("Time")]
        public bool useUnscaledTime = true;
    }
}
