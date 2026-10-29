using CosmicShore.Core;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Switchback's FUN TYPE picker (SWITCHBACK.md "Fun types"): pick the fun type first, then the intensity as
    /// ever, so every (fun type, intensity) pair is its own world.
    ///
    /// <para><b>Built in code, deliberately:</b> it is a test surface for comparing the plans, not a designed
    /// control, so it lives here rather than in the launch-panel prefabs. Two buttons in a strip that floats just
    /// ABOVE the intensity row (a <see cref="LayoutElement"/> with <c>ignoreLayout</c>, so the row's own layout is
    /// untouched): <c>FUN: &lt;type&gt; &gt;</c> cycles Classic, Level Shapes, Par &amp; Medals, Live Features,
    /// Combined, Blast Run; <c>SEED: RANDOM / FIXED</c> toggles one known course per world
    /// (<see cref="SwitchbackFunTypes.WorldSeed"/>). Both are remembered on this machine (PlayerPrefs
    /// <c>Switchback.FunType</c>, <c>Switchback.FixedSeed</c>).</para>
    ///
    /// <para>Shown for Switchback only, and only to the HOST: a guest's course comes from the host (the fun type
    /// rides the course broadcast). A weekly challenge or the quest funnel hides it and flies Classic, random seed -
    /// the challenge is the same ask for every player.</para>
    /// </summary>
    public partial class ArcadeGameConfigureModal
    {
        const string FunTypePrefKey = "Switchback.FunType";
        const string FixedSeedPrefKey = "Switchback.FixedSeed";

        RectTransform _funTypeStrip;
        TMP_Text _funTypeLabel;
        TMP_Text _fixedSeedLabel;
        SwitchbackFunType _switchbackFunType;
        bool _switchbackFixedSeed;
        bool _funTypePrefsLoaded;

        bool FunTypePickerApplies =>
            _selectedGame && _selectedGame.Mode == GameModes.Switchback && !IsClientMode && !_weeklyChallengeLocked
            && !QuestArcadeConstraints.AppliesTo(_selectedGame.Mode);

        /// <summary>The fun type and seed mode this card launches with (Classic and random off the picker).</summary>
        void SyncSwitchbackFunType()
        {
            if (!gameData) return;
            LoadFunTypePrefs();
            bool applies = FunTypePickerApplies;
            gameData.SwitchbackFunType = applies ? _switchbackFunType : SwitchbackFunType.Baseline;
            gameData.SwitchbackFixedSeed = applies && _switchbackFixedSeed;
        }

        void LoadFunTypePrefs()
        {
            if (_funTypePrefsLoaded) return;
            _funTypePrefsLoaded = true;
            _switchbackFunType = SwitchbackFunTypes.Sanitize(PlayerPrefs.GetInt(FunTypePrefKey, 0));
            _switchbackFixedSeed = PlayerPrefs.GetInt(FixedSeedPrefKey, 0) != 0;
        }

        /// <summary>Show, hide, re-anchor and re-label the strip for the card as it now stands.</summary>
        void RefreshSwitchbackFunTypePicker()
        {
            LoadFunTypePrefs();
            bool applies = FunTypePickerApplies;
            Transform anchor = applies ? FunTypeAnchor() : null;

            if (!anchor)
            {
                if (_funTypeStrip) _funTypeStrip.gameObject.SetActive(false);
                SyncSwitchbackFunType();
                return;
            }

            if (!_funTypeStrip) BuildFunTypeStrip(anchor);
            else if (_funTypeStrip.parent != anchor) _funTypeStrip.SetParent(anchor, false);
            _funTypeStrip.gameObject.SetActive(true);
            _funTypeStrip.SetAsLastSibling();
            RelabelFunTypeStrip();
            SyncSwitchbackFunType();
        }

        /// <summary>The intensity row's container: the stepper when the panel has one, else the first pip's parent.</summary>
        Transform FunTypeAnchor()
        {
            var row = ActiveIntensityButtons;
            for (int i = 0; i < row.Count; i++)
            {
                if (!row[i]) continue;
                var stepper = row[i].GetComponentInParent<IntensityStepper>(true);
                return stepper ? stepper.transform : row[i].transform.parent;
            }
            return null;
        }

        void BuildFunTypeStrip(Transform anchor)
        {
            var go = new GameObject("SwitchbackFunTypePicker", typeof(RectTransform), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            _funTypeStrip = (RectTransform)go.transform;
            _funTypeStrip.SetParent(anchor, false);
            _funTypeStrip.anchorMin = _funTypeStrip.anchorMax = new Vector2(0.5f, 1f);
            _funTypeStrip.pivot = new Vector2(0.5f, 0f);
            _funTypeStrip.anchoredPosition = new Vector2(0f, 12f);
            _funTypeStrip.sizeDelta = new Vector2(520f, 48f);

            TMP_FontAsset font = null;
            var sample = _activePanel ? _activePanel.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample) font = sample.font;

            _funTypeLabel = MakeStripButton("FunType", 0f, 0.68f, font, CycleSwitchbackFunType);
            _fixedSeedLabel = MakeStripButton("FixedSeed", 0.70f, 1f, font, ToggleSwitchbackFixedSeed);
        }

        TMP_Text MakeStripButton(string name, float xMin, float xMax, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_funTypeStrip, false);
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGo = new GameObject("Label", typeof(RectTransform));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-8f, 0f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        void CycleSwitchbackFunType()
        {
            if (!FunTypePickerApplies) return;
            var order = SwitchbackFunTypes.PickerOrder;
            int at = System.Array.IndexOf(order, _switchbackFunType);
            _switchbackFunType = order[(at + 1 + order.Length) % order.Length];
            PlayerPrefs.SetInt(FunTypePrefKey, (int)_switchbackFunType);
            PlayerPrefs.Save();
            RelabelFunTypeStrip();
            SyncSwitchbackFunType();
        }

        void ToggleSwitchbackFixedSeed()
        {
            if (!FunTypePickerApplies) return;
            _switchbackFixedSeed = !_switchbackFixedSeed;
            PlayerPrefs.SetInt(FixedSeedPrefKey, _switchbackFixedSeed ? 1 : 0);
            PlayerPrefs.Save();
            RelabelFunTypeStrip();
            SyncSwitchbackFunType();
        }

        void RelabelFunTypeStrip()
        {
            if (_funTypeLabel) _funTypeLabel.text = $"FUN: {SwitchbackFunTypes.Label(_switchbackFunType)} >";
            if (_fixedSeedLabel) _fixedSeedLabel.text = _switchbackFixedSeed ? "SEED: FIXED" : "SEED: RANDOM";
        }
    }
}
