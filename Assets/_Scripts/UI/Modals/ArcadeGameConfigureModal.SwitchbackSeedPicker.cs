using CosmicShore.Core;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Switchback's SEED picker: one <c>SEED: RANDOM / FIXED</c> button floating just ABOVE the intensity row. FIXED
    /// flies one known Blast Run course per intensity (<c>SwitchbackBlastRun.FixedSeed</c>, 45_501-45_504) so a tester
    /// can compare runs; RANDOM is a fresh course every match. Remembered on this machine (PlayerPrefs
    /// <c>Switchback.FixedSeed</c>).
    ///
    /// <para>Switchback used to pick a "fun type" here too (FUN: Classic / Level Shapes / Par &amp; Medals / Live
    /// Features / Combined / Blast Run). Since 2026-10-29 Blast Run is the only Switchback, so that button is gone and
    /// its stale PlayerPrefs (<c>Switchback.FunType</c> and the old per-fun-type personal bests <c>Switchback.PB.*</c>)
    /// are deleted the first time this panel loads its prefs.</para>
    ///
    /// <para><b>Built in code, deliberately:</b> a test surface, not a designed control, so it lives here rather than
    /// in the launch-panel prefabs (a <see cref="LayoutElement"/> with <c>ignoreLayout</c>, so the row's own layout is
    /// untouched). Shown for Switchback only, and only to the HOST: a guest's course comes from the host. A weekly
    /// challenge or the quest funnel hides it and flies a random seed - the challenge is the same ask for every
    /// player. Every path flies Blast Run.</para>
    /// </summary>
    public partial class ArcadeGameConfigureModal
    {
        const string FixedSeedPrefKey = "Switchback.FixedSeed";

        /// <summary>The retired fun-type picker's key, and the names its personal-best keys were built from
        /// (<c>Switchback.PB.&lt;type&gt;.I&lt;1-4&gt;</c>). Deleted on load; never written again.</summary>
        const string StaleFunTypePrefKey = "Switchback.FunType";
        static readonly string[] StaleFunTypeNames =
            { "Baseline", "Plan1_LevelShapes", "Plan2_LiveFeatures", "Plan3_ParMedals", "Combined", "BlastRun" };

        RectTransform _seedStrip;
        TMP_Text _fixedSeedLabel;
        bool _switchbackFixedSeed;
        bool _seedPrefsLoaded;

        bool SeedPickerApplies =>
            _selectedGame && _selectedGame.Mode == GameModes.Switchback && !IsClientMode && !_weeklyChallengeLocked
            && !QuestArcadeConstraints.AppliesTo(_selectedGame.Mode);

        /// <summary>The seed mode this card launches with (random off the picker).</summary>
        void SyncSwitchbackSeedMode()
        {
            if (!gameData) return;
            LoadSeedPrefs();
            gameData.SwitchbackFixedSeed = SeedPickerApplies && _switchbackFixedSeed;
        }

        void LoadSeedPrefs()
        {
            if (_seedPrefsLoaded) return;
            _seedPrefsLoaded = true;
            _switchbackFixedSeed = PlayerPrefs.GetInt(FixedSeedPrefKey, 0) != 0;
            DeleteStaleFunTypePrefs();
        }

        /// <summary>Drop what the retired fun-type picker left behind. Safe to run every launch: a missing key is a no-op.</summary>
        static void DeleteStaleFunTypePrefs()
        {
            bool any = PlayerPrefs.HasKey(StaleFunTypePrefKey);
            PlayerPrefs.DeleteKey(StaleFunTypePrefKey);
            foreach (var name in StaleFunTypeNames)
                for (int i = 1; i <= 4; i++)
                {
                    string key = $"Switchback.PB.{name}.I{i}";
                    if (!PlayerPrefs.HasKey(key)) continue;
                    PlayerPrefs.DeleteKey(key);
                    any = true;
                }
            if (any) PlayerPrefs.Save();
        }

        /// <summary>Show, hide, re-anchor and re-label the button for the card as it now stands.</summary>
        void RefreshSwitchbackSeedPicker()
        {
            LoadSeedPrefs();
            Transform anchor = SeedPickerApplies ? SeedPickerAnchor() : null;

            if (!anchor)
            {
                if (_seedStrip) _seedStrip.gameObject.SetActive(false);
                SyncSwitchbackSeedMode();
                return;
            }

            if (!_seedStrip) BuildSeedStrip(anchor);
            else if (_seedStrip.parent != anchor) _seedStrip.SetParent(anchor, false);
            _seedStrip.gameObject.SetActive(true);
            _seedStrip.SetAsLastSibling();
            RelabelSeedStrip();
            SyncSwitchbackSeedMode();
        }

        /// <summary>The intensity row's container: the stepper when the panel has one, else the first pip's parent.</summary>
        Transform SeedPickerAnchor()
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

        void BuildSeedStrip(Transform anchor)
        {
            var go = new GameObject("SwitchbackSeedPicker", typeof(RectTransform), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            _seedStrip = (RectTransform)go.transform;
            _seedStrip.SetParent(anchor, false);
            _seedStrip.anchorMin = _seedStrip.anchorMax = new Vector2(0.5f, 1f);
            _seedStrip.pivot = new Vector2(0.5f, 0f);
            _seedStrip.anchoredPosition = new Vector2(0f, 12f);
            _seedStrip.sizeDelta = new Vector2(200f, 48f);

            TMP_FontAsset font = null;
            var sample = _activePanel ? _activePanel.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample) font = sample.font;

            _fixedSeedLabel = MakeStripButton("FixedSeed", 0f, 1f, font, ToggleSwitchbackFixedSeed);
        }

        TMP_Text MakeStripButton(string name, float xMin, float xMax, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_seedStrip, false);
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

        void ToggleSwitchbackFixedSeed()
        {
            if (!SeedPickerApplies) return;
            _switchbackFixedSeed = !_switchbackFixedSeed;
            PlayerPrefs.SetInt(FixedSeedPrefKey, _switchbackFixedSeed ? 1 : 0);
            PlayerPrefs.Save();
            RelabelSeedStrip();
            SyncSwitchbackSeedMode();
        }

        void RelabelSeedStrip()
        {
            if (_fixedSeedLabel) _fixedSeedLabel.text = _switchbackFixedSeed ? "SEED: FIXED" : "SEED: RANDOM";
        }
    }
}
