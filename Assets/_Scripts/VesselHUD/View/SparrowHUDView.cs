using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDView : VesselHUDView
    {
        [Header("Missiles")]
        [SerializeField] private Sprite[] missileIcons;
        [SerializeField] private Image missileIcon;

        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private Color boostNormalColor;
        [SerializeField] private Color boostFullColor;
        [SerializeField] private Color overheatingColor;

        [Header("Boost Animation")]
        [SerializeField] private float boostFillTweenDuration = 0.15f;

        [Header("Weapon Mode")]
        [SerializeField] private Image weaponModeIcon;
        [SerializeField] private Sprite[] weaponModeIcons = new Sprite[2];

        [Header("Blocked Input Highlights")]
        [SerializeField] private Color blockedInputColor = Color.red;
        [SerializeField] private float blockedPulseDuration = 0.4f;

        readonly Dictionary<InputEvents, Tween> _blockTweens = new();
        private Tween _boostFillTween;

        public override void Initialize()
        {
            if (missileIcon)
                missileIcon.enabled = false;

            if (boostFill)
            {
                boostFill.fillAmount = 0f;
                boostFill.color = boostNormalColor;
            }

            if (weaponModeIcon)
                weaponModeIcon.enabled = false;

            foreach (var h in highlights.Where(h => h.image))
            {
                h.image.enabled = false;
                h.image.color = Color.white;
            }
        }

        #region Missiles

        public void InitializeMissileIcon()
        {
            if (!missileIcon || missileIcons == null || missileIcons.Length == 0)
                return;

            var sprite = missileIcons[^1];
            if (!sprite) return;

            missileIcon.sprite = sprite;
            missileIcon.enabled = true;
        }

        public void HideMissileIcon()
        {
            if (missileIcon)
                missileIcon.enabled = false;
        }

        public void SetMissilesFromAmmo01(float ammo01)
        {
            if (!missileIcon || missileIcons == null || missileIcons.Length == 0)
                return;

            var maxState = missileIcons.Length - 1;
            var state = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(ammo01) * maxState),
                0, maxState);

            var sprite = missileIcons[state];
            if (!sprite) return;

            missileIcon.sprite = sprite;
            missileIcon.enabled = true;
        }

        #endregion

        #region Boost / Heat

        public void SetBoostState(float heat01, bool overheated)
        {
            if (!boostFill) return;

            var clamped = Mathf.Clamp01(heat01);

            // Smooth fill interpolation instead of instant snap
            _boostFillTween?.Kill();
            _boostFillTween = boostFill.DOFillAmount(clamped, boostFillTweenDuration)
                .SetEase(Ease.Linear);

            if (overheated)
                boostFill.color = overheatingColor;
            else if (Mathf.Approximately(clamped, 1f))
                boostFill.color = boostFullColor;
            else
                boostFill.color = boostNormalColor;
        }

        #endregion

        #region Weapon Mode

        public void SetWeaponMode(bool isStationary)
        {
            if (!weaponModeIcon || weaponModeIcons == null || weaponModeIcons.Length < 2)
                return;

            var idx = isStationary ? 1 : 0;
            var sprite = weaponModeIcons[idx];
            if (!sprite) return;

            weaponModeIcon.sprite = sprite;
            weaponModeIcon.enabled = true;
        }

        #endregion

        #region Blocked Input Highlights

        public void HandleInputEventBlocked(InputEventBlockPayload payload)
        {
            if (payload.Started)
                StartBlockedHighlight(payload.Input);
            else if (payload.Ended)
                StopBlockedHighlight(payload.Input);
        }

        void StartBlockedHighlight(InputEvents input)
        {
            var img = FindHighlightImage(input);
            if (!img) return;

            // Kill existing tween for this input
            if (_blockTweens.TryGetValue(input, out var existing))
                existing?.Kill();

            img.enabled = true;
            img.color = blockedInputColor;

            // Pulse between blocked color and a dimmed version
            var dimColor = blockedInputColor * 0.5f;
            dimColor.a = 1f;
            _blockTweens[input] = img.DOColor(dimColor, blockedPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        void StopBlockedHighlight(InputEvents input)
        {
            if (_blockTweens.TryGetValue(input, out var tween))
            {
                tween?.Kill();
                _blockTweens.Remove(input);
            }

            var img = FindHighlightImage(input);
            if (!img) return;

            img.color = Color.white;
            img.enabled = false;
        }

        Image FindHighlightImage(InputEvents ev)
        {
            for (var i = 0; i < highlights.Count; i++)
                if (highlights[i].input == ev)
                    return highlights[i].image;
            return null;
        }

        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _boostFillTween?.Kill();
            foreach (var tween in _blockTweens.Values)
                tween?.Kill();
            _blockTweens.Clear();
        }
    }
}
