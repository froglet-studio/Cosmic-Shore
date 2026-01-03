using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Weapon Mode")]
        [SerializeField] private Image weaponModeIcon;
        [SerializeField] private Sprite[] weaponModeIcons = new Sprite[2];

        [Header("Blocked Input Highlights")]
        [SerializeField] private Color blockedInputColor = Color.red;

        readonly Dictionary<InputEvents, Coroutine> _blockEnforcers = new();

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
            boostFill.fillAmount = clamped;

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

            if (_blockEnforcers.TryGetValue(input, out var running) && running != null)
                StopCoroutine(running);

            _blockEnforcers[input] = StartCoroutine(EnforceBlockedHighlight(img, input));
        }

        void StopBlockedHighlight(InputEvents input)
        {
            if (_blockEnforcers.TryGetValue(input, out var running) && running != null)
                StopCoroutine(running);

            _blockEnforcers.Remove(input);

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

        IEnumerator EnforceBlockedHighlight(Image img, InputEvents input)
        {
            while (true)
            {
                if (!img) yield break;
                img.enabled = true;
                img.color = blockedInputColor;
                yield return null;
            }
        }

        #endregion
    }
}
