using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class SparrowHUDView : VesselHUDView
    {
        [Header("Missiles")]
        [SerializeField] private Sprite[] missileIcons;
        [SerializeField] private Image missileIcon;

        [Header("Strafing Roll Charge")]
        [Tooltip("The ring on the boost ability icon. This is NOT a gauge — the boost has no heat and " +
                 "no meter any more. It is a binary charge pip for the strafing roll: full = a roll is " +
                 "available on this boost press, empty = it has been spent. The next boost press " +
                 "re-arms it.")]
        [FormerlySerializedAs("boostFill")]
        [SerializeField] private Image rollChargeIndicator;

        // NOTE: deliberately NOT [FormerlySerializedAs] off the retired boost-gauge colours - those
        // were authored white / white / red, so inheriting them would paint "armed" and "spent"
        // identically. The stale overrides are deleted from Sparrow.prefab; these are the record.
        [Tooltip("Ring colour while a strafing roll is available.")]
        [SerializeField] private Color rollArmedColor = new(0.55f, 0.9f, 1f, 1f);

        [Tooltip("Ring colour once the roll has been spent (until the next boost press re-arms it).")]
        [SerializeField] private Color rollSpentColor = new(0.35f, 0.4f, 0.45f, 0.5f);

        [Header("Strafing Roll Animation")]
        [Tooltip("Seconds the ring takes to wipe empty when the roll is spent / fill when re-armed. " +
                 "Nothing pops in or out.")]
        [FormerlySerializedAs("boostFillTweenDuration")]
        [SerializeField] private float rollChargeTweenDuration = 0.15f;

        [Tooltip("Scale punch played on the ring the instant a roll is spent, so the consume reads even " +
                 "at the edge of vision.")]
        [SerializeField] private float rollSpendPunchScale = 0.3f;

        [Header("Weapon Mode")]
        [SerializeField] private Image weaponModeIcon;
        [SerializeField] private Sprite[] weaponModeIcons = new Sprite[2];

        [Header("Blocked Input Highlights")]
        [SerializeField] private Color blockedInputColor = Color.red;
        [SerializeField] private float blockedPulseDuration = 0.4f;

        readonly Dictionary<InputEvents, Tween> _blockTweens = new();
        private Tween _rollChargeTween;
        private Tween _rollPunchTween;

        public override void Initialize()
        {
            if (missileIcon)
                missileIcon.enabled = false;

            if (rollChargeIndicator)
            {
                // Spent until the first boost press arms it — SparrowHUDController re-seeds from
                // BarrelRollController.IsRollArmed right after this.
                rollChargeIndicator.fillAmount = 0f;
                rollChargeIndicator.color = rollSpentColor;
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

        #region Strafing Roll Charge

        /// <summary>
        /// Shows whether the strafing roll is available right now. Driven by
        /// <see cref="CosmicShore.Gameplay.BarrelRollController.OnRollChargeChanged"/> — the boost
        /// itself is indefinite and has nothing to meter, so this ring is a binary charge pip, not a
        /// gauge: full while the boost press's short arm window is live, empty once the roll is
        /// spent OR the window lapses unfired. Only a real spend gets the punch — see
        /// <see cref="CosmicShore.Data.RollChargeState"/>.
        /// </summary>
        public void SetRollCharge(RollChargeState state)
        {
            if (!rollChargeIndicator) return;

            bool armed = state == RollChargeState.Armed;

            _rollChargeTween?.Kill();
            _rollChargeTween = rollChargeIndicator
                .DOFillAmount(armed ? 1f : 0f, rollChargeTweenDuration)
                .SetEase(armed ? Ease.OutQuad : Ease.InQuad)
                .SetLink(rollChargeIndicator.gameObject);

            rollChargeIndicator.color = armed ? rollArmedColor : rollSpentColor;

            // Spending the roll is the beat worth feeling; arming it rides the fill, and a window
            // that lapsed unfired consumed nothing — punching there would announce a roll the
            // pilot never got.
            if (state != RollChargeState.Spent || rollSpendPunchScale <= 0f) return;

            _rollPunchTween?.Kill();
            rollChargeIndicator.rectTransform.localScale = Vector3.one;
            _rollPunchTween = rollChargeIndicator.rectTransform
                .DOPunchScale(Vector3.one * rollSpendPunchScale, rollChargeTweenDuration * 2f, 1, 0.5f)
                .SetLink(rollChargeIndicator.gameObject);
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
            _rollChargeTween?.Kill();
            _rollPunchTween?.Kill();
            foreach (var tween in _blockTweens.Values)
                tween?.Kill();
            _blockTweens.Clear();
        }
    }
}
