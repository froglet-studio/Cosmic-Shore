using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Hangar unit (2026-07-10) — the ported HangarScreen + HangarVesselGridCard +
// HangarVesselDetailView + VesselUnlockSystem, headless, in the menushell's
// wiring shape (grid container + inactive card template + detail view). Covers
// the screen contract: PopulateGrid sorts unlocked-first and stamps lock
// overlays; LoadView coalesces per frame (the double-load NavigateTo guard);
// the staggered card fade runs on UNSCALED time (pause-immune — the menu holds
// timeScale 0); the eye button toggles name visibility; card click →
// SelectVesselForDetail swaps grid → detail with name/description/unlock text;
// the unlock flow hides CONFIRM when the wallet can't afford the vessel and,
// when it can, spends through PlayerDataService, unlocks the SO, raises
// OnUnlockStateChanged (grid re-sorts), and flips the button to UNLOCKED.
// ─────────────────────────────────────────────────────────────────────────────

public class HangarScreenTests : IDisposable
{
    readonly GameLoop loop;

    readonly HangarScreen screen;
    readonly SO_VesselList vesselList;
    readonly SO_Vessel manta;      // unlocked
    readonly SO_Vessel dolphin;    // unlocked
    readonly SO_Vessel serpent;    // locked, 300
    readonly SO_Vessel sparrow;    // locked, 100
    readonly GameObject gridPanel;
    readonly Transform gridContainer;
    readonly GameObject detailPanel;
    readonly HangarVesselDetailView detailView;
    readonly Button eyeButton;
    readonly Button unlockButton;
    readonly TMP_Text unlockButtonText;
    readonly TMP_Text vesselNameText;
    readonly TMP_Text descriptionText;
    readonly GameObject unlockPanel;
    readonly Button confirmButton;
    readonly TMP_Text crystalAmountText;
    readonly PlayerDataService playerData;

    public HangarScreenTests()
    {
        loop = new GameLoop(nameof(HangarScreenTests));

        // Crystal wallet: a live PlayerDataService with a seeded local profile
        // (no UGSDataService — every repo path in the service null-guards).
        // The Instance static is cleared first: it can survive a prior test's
        // loop disposal (DontDestroyOnLoad), and a stale Instance makes Awake
        // destroy the newcomer — VesselUnlockSystem would then spend a dead
        // test's wallet.
        ClearServiceInstance();
        playerData = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
        SeedProfile(playerData, crystalBalance: 100);

        SO_Vessel MakeVessel(string name, int cost, bool locked)
        {
            var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
            vessel.Name = name;
            vessel.Description = $"{name} description";
            vessel.UnlockCost = cost;
            if (locked) vessel.Lock();
            return vessel;
        }

        manta = MakeVessel("Manta", 0, locked: false);
        dolphin = MakeVessel("Dolphin", 0, locked: false);
        serpent = MakeVessel("Serpent", 300, locked: true);
        sparrow = MakeVessel("Sparrow", 100, locked: true);
        vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
        // Locked ones FIRST in authored order so the unlocked-first sort is observable.
        vesselList.VesselList = new List<SO_Vessel> { serpent, sparrow, manta, dolphin };

        // Wire-then-activate (the menushell idiom): fields land before OnEnable.
        var panelGo = new GameObject("HangarPanel", typeof(RectTransform));
        panelGo.SetActive(false);
        screen = panelGo.AddComponent<HangarScreen>();

        gridPanel = new GameObject("GridPanel", typeof(RectTransform));
        gridPanel.transform.SetParent(panelGo.transform, false);
        var containerGo = new GameObject("GridContainer", typeof(RectTransform));
        containerGo.transform.SetParent(gridPanel.transform, false);
        gridContainer = containerGo.transform;

        // Card template: inactive, outside the container (PopulateGrid clears it).
        var template = new GameObject("GridCardTemplate", typeof(RectTransform));
        template.transform.SetParent(panelGo.transform, false);
        template.SetActive(false);
        template.AddComponent<CanvasGroup>();
        var cardBg = template.AddComponent<Image>();
        var cardButton = template.AddComponent<Button>();
        cardButton.transition = Selectable.Transition.None;
        var cardNameGo = new GameObject("Name", typeof(RectTransform));
        cardNameGo.transform.SetParent(template.transform, false);
        var cardNameText = cardNameGo.AddComponent<TextMeshProUGUI>();
        var lockOverlay = new GameObject("LockOverlay", typeof(RectTransform));
        lockOverlay.transform.SetParent(template.transform, false);
        var gridCard = template.AddComponent<HangarVesselGridCard>();
        SetField(gridCard, "vesselIcon", cardBg);
        SetField(gridCard, "vesselName", cardNameText);
        SetField(gridCard, "lockOverlay", lockOverlay);
        SetField(gridCard, "cardButton", cardButton);

        var eyeGo = new GameObject("EyeButton", typeof(RectTransform));
        eyeGo.transform.SetParent(panelGo.transform, false);
        eyeButton = eyeGo.AddComponent<Button>();
        eyeButton.transition = Selectable.Transition.None;

        // Detail view: inactive until SelectVesselForDetail (Awake sees wired fields).
        detailPanel = new GameObject("DetailPanel", typeof(RectTransform));
        detailPanel.transform.SetParent(panelGo.transform, false);
        detailPanel.SetActive(false);
        detailView = detailPanel.AddComponent<HangarVesselDetailView>();

        TMP_Text MakeText(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }
        Button MakeButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        vesselNameText = MakeText("VesselName", detailPanel.transform);
        descriptionText = MakeText("Description", detailPanel.transform);
        var backButton = MakeButton("Back", detailPanel.transform);
        var generalButton = MakeButton("General", detailPanel.transform);
        var generalBG = new GameObject("GeneralBG", typeof(RectTransform));
        generalBG.transform.SetParent(generalButton.transform, false);
        var descriptionPanel = new GameObject("DescriptionPanel", typeof(RectTransform));
        descriptionPanel.transform.SetParent(detailPanel.transform, false);
        var abilitiesPanel = new GameObject("AbilitiesPanel", typeof(RectTransform));
        abilitiesPanel.transform.SetParent(detailPanel.transform, false);
        abilitiesPanel.SetActive(false);
        unlockButton = MakeButton("Unlock", detailPanel.transform);
        unlockButtonText = MakeText("UnlockText", unlockButton.transform);
        unlockPanel = new GameObject("UnlockPanel", typeof(RectTransform));
        unlockPanel.transform.SetParent(detailPanel.transform, false);
        unlockPanel.SetActive(false);
        var spendPanel = new GameObject("SpendCrystalsPanel", typeof(RectTransform));
        spendPanel.transform.SetParent(unlockPanel.transform, false);
        confirmButton = MakeButton("Confirm", unlockPanel.transform);
        var spendDetailText = MakeText("SpendDetail", unlockPanel.transform);
        crystalAmountText = MakeText("CrystalAmount", unlockPanel.transform);

        SetField(detailView, "vesselNameText", vesselNameText);
        SetField(detailView, "backButton", backButton);
        SetField(detailView, "generalButton", generalButton);
        SetField(detailView, "generalButtonBG", generalBG);
        SetField(detailView, "descriptionPanel", descriptionPanel);
        SetField(detailView, "abilitiesPanel", abilitiesPanel);
        SetField(detailView, "vesselDescriptionText", descriptionText);
        SetField(detailView, "unlockButton", unlockButton);
        SetField(detailView, "unlockButtonText", unlockButtonText);
        SetField(detailView, "unlockPanel", unlockPanel);
        SetField(detailView, "spendCrystalsPanel", spendPanel);
        SetField(detailView, "confirmButton", confirmButton);
        SetField(detailView, "spendCrystalsDetailText", spendDetailText);
        SetField(detailView, "crystalAmountText", crystalAmountText);

        SetField(screen, "ShipList", vesselList);
        SetField(screen, "gridPanel", gridPanel);
        SetField(screen, "gridContainer", gridContainer);
        SetField(screen, "gridCardPrefab", gridCard);
        SetField(screen, "eyeButton", eyeButton);
        SetField(screen, "detailPanel", detailPanel);
        SetField(screen, "detailView", detailView);

        panelGo.SetActive(true);
        loop.Tick(1f / 60f);
    }

    public void Dispose()
    {
        loop.Dispose();
        ClearServiceInstance();
    }

    static void ClearServiceInstance()
        => typeof(PlayerDataService)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

    List<HangarVesselGridCard> Cards()
        => Enumerable.Range(0, gridContainer.childCount)
            .Select(i => gridContainer.GetChild(i).GetComponent<HangarVesselGridCard>())
            .Where(c => c != null && !c.gameObject.IsDestroyed)
            .ToList();

    void Tick(int frames = 1)
    {
        for (int i = 0; i < frames; i++)
            loop.Tick(1f / 60f);
    }

    [Fact]
    public void PopulateGrid_SortsUnlockedFirst_AndStampsLockOverlays()
    {
        screen.OnScreenEnter();
        Tick(); // Destroy() of stale children flushes end-of-frame

        var cards = Cards();
        Assert.Equal(4, cards.Count);
        // Authored order was [Serpent, Sparrow, Manta, Dolphin]; the unlocked pair
        // bubbles first and the sort is STABLE within each group.
        Assert.Equal(new[] { "Manta", "Dolphin", "Serpent", "Sparrow" },
            cards.Select(c => c.Ship.Name).ToArray());
        Assert.Equal(new[] { false, false, true, true },
            cards.Select(c => c.Ship.IsLocked).ToArray());

        // Lock overlay active exactly on the locked cards.
        foreach (var card in cards)
        {
            var overlay = (GameObject)GetField(card, "lockOverlay");
            Assert.Equal(card.Ship.IsLocked, overlay.activeSelf);
        }
    }

    [Fact]
    public void LoadView_CoalescesRepeatCallsWithinOneFrame()
    {
        // ScreenSwitcher.NavigateTo double-loads the hangar in one frame — the
        // guard must build the grid exactly once (a second run would destroy the
        // first run's cards mid-fade).
        screen.LoadView();
        var firstBuild = Cards();
        screen.LoadView(); // same frame → coalesced no-op
        Assert.Equal(firstBuild, Cards());

        Tick(); // next frame: an explicit re-load rebuilds
        screen.LoadView();
        Tick();
        Assert.Equal(4, Cards().Count);
    }

    [Fact]
    public void GridFadeIn_RunsOnUnscaledTime_WhilePaused()
    {
        screen.OnScreenEnter();
        Time.timeScale = 0f; // the menu holds the world paused
        try
        {
            Tick(90); // stagger 0.08s×4 + fade 0.25s ≪ 1.5s of unscaled ticks
            foreach (var card in Cards())
                Assert.Equal(1f, card.GetComponent<CanvasGroup>().alpha);
        }
        finally
        {
            Time.timeScale = 1f;
        }
    }

    [Fact]
    public void EyeButton_TogglesVesselNameVisibility()
    {
        screen.OnScreenEnter();
        Tick();

        eyeButton.onClick.Invoke();
        foreach (var card in Cards())
            Assert.False(((TMP_Text)GetField(card, "vesselName")).gameObject.activeSelf);

        eyeButton.onClick.Invoke();
        foreach (var card in Cards())
            Assert.True(((TMP_Text)GetField(card, "vesselName")).gameObject.activeSelf);
    }

    [Fact]
    public void CardClick_OpensDetail_WithVesselContent()
    {
        screen.OnScreenEnter();
        Tick();

        var serpentCard = Cards().First(c => c.Ship == serpent);
        ((Button)GetField(serpentCard, "cardButton")).onClick.Invoke();

        Assert.False(gridPanel.activeSelf);
        Assert.True(detailPanel.activeSelf);
        Assert.Equal("SERPENT", vesselNameText.text);
        Assert.Equal("Serpent description", descriptionText.text);
        Assert.Equal("UNLOCK - 300", unlockButtonText.text);
        Assert.True(unlockButton.interactable);
    }

    [Fact]
    public void UnlockFlow_InsufficientCrystals_HidesConfirm()
    {
        screen.OnScreenEnter();
        Tick();
        screen.SelectVesselForDetail(serpent); // costs 300; wallet holds 100
        unlockButton.onClick.Invoke();

        Assert.True(unlockPanel.activeSelf);
        Assert.False(confirmButton.gameObject.activeSelf);
        Assert.Equal("100", crystalAmountText.text);
    }

    [Fact]
    public void UnlockFlow_Purchase_SpendsWallet_UnlocksVessel_AndResortsGrid()
    {
        screen.OnScreenEnter();
        Tick();
        screen.SelectVesselForDetail(sparrow); // costs 100; wallet holds exactly 100

        unlockButton.onClick.Invoke();
        Assert.True(confirmButton.gameObject.activeSelf);

        confirmButton.onClick.Invoke();
        Assert.False(sparrow.IsLocked);
        Assert.Equal(0, playerData.GetCrystalBalance());
        Assert.False(unlockPanel.activeSelf);
        Assert.Equal("UNLOCKED", unlockButtonText.text);
        Assert.False(unlockButton.interactable);

        // OnUnlockStateChanged re-populated the grid: Sparrow joined the unlocked block.
        Tick();
        var cards = Cards();
        Assert.Equal(new[] { false, false, false, true },
            cards.Select(c => c.Ship.IsLocked).ToArray());
        Assert.Equal("Serpent", cards[3].Ship.Name);

        // Back returns to the grid.
        detailView.OnBackClicked();
        Assert.True(gridPanel.activeSelf);
        Assert.False(detailPanel.activeSelf);
    }

    [Fact]
    public void VesselUnlockSystem_LockAndUnlock_RaiseStateChanged()
    {
        screen.OnScreenEnter(); // the live screen refreshes on every state change
        Tick();
        int raised = 0;
        Action handler = () => raised++;
        VesselUnlockSystem.OnUnlockStateChanged += handler;
        try
        {
            Assert.False(VesselUnlockSystem.UnlockVessel(manta)); // already unlocked
            Assert.Equal(0, raised);
            Assert.True(VesselUnlockSystem.LockVessel(manta));
            Assert.True(manta.IsLocked);
            Assert.True(VesselUnlockSystem.UnlockVessel(manta));
            Assert.False(manta.IsLocked);
            Assert.Equal(2, raised);
        }
        finally
        {
            VesselUnlockSystem.OnUnlockStateChanged -= handler;
        }
    }

    static void SeedProfile(PlayerDataService service, int crystalBalance)
    {
        var profile = new PlayerProfileData { crystalBalance = crystalBalance };
        var property = typeof(PlayerDataService).GetProperty("CurrentProfile",
            BindingFlags.Public | BindingFlags.Instance)!;
        property.SetValue(service, profile);
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        (field ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }

    static object GetField(object target, string name)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        return (field ?? throw new MissingFieldException(target.GetType().Name, name)).GetValue(target);
    }
}
