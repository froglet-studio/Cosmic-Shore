using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Store unit (2026-07-10; re-synced 2026-10-13 when the port followed upstream in
// retiring the economy backend) — the ported StoreScreen + PurchaseItemCard
// family + PurchaseConfirmationModal over upstream's local-only CatalogManager.
// The fixture authors the shelf and wallet straight onto the manager's public
// StoreShelve / Inventory and releases them through upstream's own
// LoadAllCatalogItems. Covers: UpdateView populating captain cards (3-per-row,
// overflow row stays off) + both balances; the purchase flow card → modal →
// confirm failing closed (no backend: wallet, inventory and ticket balance
// unchanged, card and confirm button re-armed); the manager's purchase calls
// running only their failure callback; the daily-reward claim crediting the
// wallet and the currency fan-out restating every card's affordability; the
// daily-ticket spend re-inventorying the ticket through the handler's
// completion; and the element-filtered crystal balance.
// ─────────────────────────────────────────────────────────────────────────────

public class StoreScreenTests : IDisposable
{
    readonly GameLoop loop;

    readonly StoreScreen store;
    readonly CatalogManager catalog;
    readonly CaptainManager captainManager;
    readonly PurchaseConfirmationModal modal;
    readonly Button confirmButton;
    readonly HorizontalLayoutGroup row0;
    readonly HorizontalLayoutGroup row1;
    readonly TMP_Text crystalBalanceText;
    readonly TMP_Text ticketBalanceText;
    readonly TMP_Text modalUnlockText;
    readonly PurchaseGameplayTicketCard ticketCard;
    readonly VirtualItem crystal;
    readonly VirtualItem ticket;

    public StoreScreenTests()
    {
        loop = new GameLoop(nameof(StoreScreenTests));
        ResetCatalogStatics();
        ClearSingleton<CatalogManager>();
        ClearSingleton<CaptainManager>();
        ClearSingleton<DailyRewardHandler>();

        captainManager = new GameObject("CaptainManager").AddComponent<CaptainManager>();
        new GameObject("DailyRewardHandler").AddComponent<DailyRewardHandler>();

        var catalogGo = new GameObject("CatalogManager");
        catalogGo.SetActive(false);
        catalog = catalogGo.AddComponent<CatalogManager>();
        var netVariable = ScriptableObject.CreateInstance<NetworkMonitorDataVariable>();
        netVariable.Value = new NetworkMonitorData
        {
            OnNetworkFound = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnNetworkLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };
        SetField(catalog, "_networkMonitorDataVariable", netVariable);
        SetField(catalog, "_captainManager", captainManager);
        catalogGo.SetActive(true);

        // Fixture: 500-crystal wallet, three encountered captains (150/300/450),
        // the 25-crystal daily ticket. The crystal is ONE shared instance on the
        // shelf + in the inventory, exactly like the menushell fixture.
        crystal = new VirtualItem
        {
            ItemId = "crystal-omni", Name = "Omni Crystal", ContentType = "Crystal",
            Tags = new List<string> { "Omni" }, Price = new List<ItemPrice>(), Amount = 500,
        };
        var captains = new List<Captain>();
        var shelve = CatalogManager.StoreShelve;
        shelve.allItems[crystal.ItemId] = crystal;
        shelve.crystals[crystal.ItemId] = crystal;
        (string name, VesselClassType cls, Element element, int cost)[] roster =
        {
            ("AURELIA", VesselClassType.Manta, Element.Space, 150),
            ("KORVAX", VesselClassType.Rhino, Element.Mass, 300),
            ("SIRRA", VesselClassType.Dolphin, Element.Time, 450),
        };
        int index = 0;
        foreach (var (name, cls, element, cost) in roster)
        {
            var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
            vessel.Name = cls.ToString();
            vessel.Class = cls;
            var soCaptain = ScriptableObject.CreateInstance<SO_Captain>();
            soCaptain.Name = name;
            soCaptain.Description = $"{name} desc";
            soCaptain.Vessel = vessel;
            soCaptain.PrimaryElement = element;
            captains.Add(new Captain(soCaptain) { Encountered = true });
            var item = new VirtualItem
            {
                ItemId = $"captain-{++index}", Name = name, Description = $"{name} desc",
                ContentType = "Captain", Tags = new List<string>(),
                Price = new List<ItemPrice> { new() { ItemId = "crystal-omni", Amount = cost, UnitAmount = 1 } },
            };
            shelve.allItems[item.ItemId] = item;
            shelve.captains[item.ItemId] = item;
        }
        ticket = new VirtualItem
        {
            ItemId = "ticket-dc", Name = "Daily Challenge Ticket", ContentType = "Ticket",
            Tags = new List<string>(),
            Price = new List<ItemPrice> { new() { ItemId = "crystal-omni", Amount = 25, UnitAmount = 1 } },
        };
        shelve.allItems[ticket.ItemId] = ticket;
        shelve.tickets[ticket.ItemId] = ticket;
        shelve.DailyChallengeTicket = ticket;
        CatalogManager.Inventory.crystals.Add(crystal);
        CatalogManager.Inventory.allItems.Add(crystal);
        captainManager.LoadLocalCaptains(captains);
        catalog.LoadAllCatalogItems(); // CatalogLoaded → StoreScreen.Start's UpdateView lane

        // ── modal rig ───────────────────────────────────────────────────────
        var audio = CosmicShore.Cli.AudioSystemRig.Create();
        var modalGo = new GameObject("PurchaseConfirmationModal", typeof(RectTransform));
        modalGo.SetActive(false);
        modalGo.AddComponent<CanvasGroup>();
        modal = modalGo.AddComponent<PurchaseConfirmationModal>();
        SetField(modal, "_captainManager", captainManager);
        SetField(modal, "audioSystem", audio); // ModalWindowIn/Confirm play cues through it
        TMP_Text ModalText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(modalGo.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }
        Image ModalImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(modalGo.transform, false);
            return go.AddComponent<Image>();
        }
        var modalPrice = ModalText("PriceLabel");
        modalUnlockText = ModalText("UnlockText");
        var confirmGo = new GameObject("ConfirmButton", typeof(RectTransform));
        confirmGo.transform.SetParent(modalGo.transform, false);
        confirmButton = confirmGo.AddComponent<Button>();
        confirmButton.transition = Selectable.Transition.None;
        confirmButton.onClick.AddListener(modal.Confirm);
        SetField(modal, "PriceLabel", modalPrice);
        SetField(modal, "UnlockText", modalUnlockText);
        SetField(modal, "CrystalBalanceText", ModalText("CrystalBalanceText"));
        SetField(modal, "TicketBalanceText", ModalText("TicketBalanceText"));
        SetField(modal, "ConfirmButton", confirmButton);
        SetField(modal, "IconEmitter", modalGo.AddComponent<IconEmitter>());
        SetField(modal, "CaptainImage", ModalImage("CaptainImage"));
        SetField(modal, "GameImage", ModalImage("GameImage"));
        SetField(modal, "TicketImage", ModalImage("TicketImage"));
        modalGo.SetActive(true);

        // ── screen rig (the menushell's shape) ──────────────────────────────
        var panelGo = new GameObject("StorePanel", typeof(RectTransform));
        panelGo.SetActive(false);

        var templates = new GameObject("Templates", typeof(RectTransform));
        templates.transform.SetParent(panelGo.transform, false);
        templates.SetActive(false);

        T MakeCard<T>(string name, Transform parent) where T : PurchaseItemCard
        {
            var cardGo = new GameObject(name, typeof(RectTransform));
            cardGo.transform.SetParent(parent, false);
            var bg = cardGo.AddComponent<Image>();
            var button = cardGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            TMP_Text Label(string child)
            {
                var go = new GameObject(child, typeof(RectTransform));
                go.transform.SetParent(cardGo.transform, false);
                return go.AddComponent<TextMeshProUGUI>();
            }
            Image StateImage(string child, bool active)
            {
                var go = new GameObject(child, typeof(RectTransform));
                go.transform.SetParent(cardGo.transform, false);
                var image = go.AddComponent<Image>();
                go.SetActive(active);
                return image;
            }
            var card = cardGo.AddComponent<T>();
            SetField(card, "PriceLabel", Label("PriceLabel"));
            SetField(card, "UnavailablePriceLabel", Label("UnavailablePriceLabel"));
            SetField(card, "ItemNameLabel", Label("ItemNameLabel"));
            SetField(card, "ItemDescriptionLabel", Label("ItemDescriptionLabel"));
            SetField(card, "ItemImage", StateImage("ItemImage", true));
            SetField(card, "PriceButton", StateImage("PriceButton", true));
            SetField(card, "UnavailableButton", StateImage("UnavailableButton", false));
            SetField(card, "PurchasedButton", StateImage("PurchasedButton", false));
            SetField(card, "BackgroundImage", bg);
            // Prefab-persistent listener stand-in (see the menushell wiring note).
            cardGo.AddComponent<PurchaseCardClickBinding>();
            return card;
        }

        var captainTemplate = MakeCard<PurchaseCaptainCard>("CaptainCardTemplate", templates.transform);
        SetField(captainTemplate, "_captainManager", captainManager);
        var gameTemplate = MakeCard<PurchaseGameCard>("GameCardTemplate", templates.transform);
        ticketCard = MakeCard<PurchaseGameplayTicketCard>("DailyChallengeTicketCard", panelGo.transform);

        HorizontalLayoutGroup MakeRow(string name, bool active)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panelGo.transform, false);
            var group = go.AddComponent<HorizontalLayoutGroup>();
            go.SetActive(active);
            return group;
        }
        var captainSection = new GameObject("CaptainPurchaseSection", typeof(RectTransform));
        captainSection.transform.SetParent(panelGo.transform, false);
        row0 = MakeRow("CaptainRow_0", active: true);
        row0.transform.SetParent(captainSection.transform, false);
        row1 = MakeRow("CaptainRow_1", active: false);
        row1.transform.SetParent(captainSection.transform, false);
        var gameSection = new GameObject("GamePurchaseSection", typeof(RectTransform));
        gameSection.transform.SetParent(panelGo.transform, false);
        gameSection.SetActive(false);
        var gameRow = MakeRow("GameRow_0", active: true);
        gameRow.transform.SetParent(gameSection.transform, false);

        TMP_Text Balance(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panelGo.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }
        crystalBalanceText = Balance("CrystalBalance");
        ticketBalanceText = Balance("TicketBalance");

        panelGo.AddComponent<MenuAudio>();
        store = panelGo.AddComponent<StoreScreen>();
        SetField(store, "_captainManager", captainManager);
        SetField(store, "CrystalBalance", crystalBalanceText);
        SetField(store, "TicketBalance", ticketBalanceText);
        SetField(store, "CaptainPurchaseSection", captainSection);
        SetField(store, "PurchaseCaptainPrefab", captainTemplate);
        SetField(store, "CaptainPurchaseRows", new List<HorizontalLayoutGroup> { row0, row1 });
        SetField(store, "PurchaseConfirmationModal", modal);
        SetField(store, "PurchaseConfirmationButton", confirmButton);
        SetField(store, "GamePurchaseSection", gameSection);
        SetField(store, "PurchaseGamePrefab", gameTemplate);
        SetField(store, "GamePurchaseRows", new List<HorizontalLayoutGroup> { gameRow });
        SetField(store, "DailyChallengeTicketCard", ticketCard);

        panelGo.SetActive(true);
        loop.Tick(1f / 60f); // Start → CatalogLoaded lane → UpdateView
    }

    public void Dispose()
    {
        loop.Dispose();
        ResetCatalogStatics();
        ClearSingleton<CatalogManager>();
        ClearSingleton<CaptainManager>();
        ClearSingleton<DailyRewardHandler>();
    }

    /// <summary>
    /// Upstream's per-domain-load reset ([RuntimeInitializeOnLoadMethod], data-only in
    /// this engine) — invoked directly so each rig starts from an empty shelf.
    /// </summary>
    static void ResetCatalogStatics()
        => typeof(CatalogManager)
            .GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null);

    static void ClearSingleton<T>() where T : Component
        => typeof(CosmicShore.Utility.SingletonPersistent<T>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

    void Tick(int frames = 1)
    {
        for (int i = 0; i < frames; i++)
            loop.Tick(1f / 60f);
    }

    List<PurchaseCaptainCard> CaptainCards()
        => Enumerable.Range(0, row0.transform.childCount)
            .Select(i => row0.transform.GetChild(i).GetComponent<PurchaseCaptainCard>())
            .Where(c => c != null)
            .ToList();

    PurchaseCaptainCard Card(string name)
        => CaptainCards().First(c =>
            ((TMP_Text)GetField(c, "ItemNameLabel")).text == name);

    [Fact]
    public void UpdateView_PopulatesCaptainCards_AndBalances()
    {
        var cards = CaptainCards();
        Assert.Equal(3, cards.Count);
        Assert.False(row1.gameObject.activeSelf); // no overflow at 3-per-row

        Assert.Equal(new[] { "150", "300", "450" },
            cards.Select(c => ((TMP_Text)GetField(c, "PriceLabel")).text).ToArray());
        Assert.Equal("AURELIA desc", ((TMP_Text)GetField(cards[0], "ItemDescriptionLabel")).text);

        Tick(70); // the crystal-balance count-up runs ~1s of unscaled frames
        Assert.Equal("500", crystalBalanceText.text);
        Assert.Equal("0", ticketBalanceText.text);
        Assert.Equal("25", ((TMP_Text)GetField(ticketCard, "PriceLabel")).text);
    }

    [Fact]
    public void PurchaseFlow_CardToModalToConfirm_FailsClosed_WithNoBackend()
    {
        var aurelia = Card("AURELIA");
        aurelia.GetComponent<Button>().onClick.Invoke(); // OnClickBuy → modal armed
        Assert.Equal("to unlock AURELIA?", modalUnlockText.text);

        confirmButton.onClick.Invoke(); // Confirm → Purchase → PurchaseItem → failure lane

        // Nothing is spent or granted: there is no economy backend upstream.
        Assert.Equal(500, catalog.GetCrystalBalance());
        Assert.False(CatalogManager.Inventory.ContainsCaptain("AURELIA"));

        // OnPurchaseFailed re-arms the card and the modal's confirm button.
        Assert.True(confirmButton.enabled);
        Assert.True(aurelia.GetComponent<Button>().enabled);
        Assert.True(((Image)GetField(aurelia, "PriceButton")).gameObject.activeSelf);
        Assert.False(((Image)GetField(aurelia, "PurchasedButton")).gameObject.activeSelf);

        // No purchase juice runs: well past the old flip timing the card is unowned.
        Tick(160);
        Assert.False(((Image)GetField(aurelia, "PurchasedButton")).gameObject.activeSelf);
        Tick(70);
        Assert.Equal("500", crystalBalanceText.text);
    }

    [Fact]
    public void TicketPurchase_FailsClosed_TicketBalanceUnchanged()
    {
        ticketCard.GetComponent<Button>().onClick.Invoke();
        Assert.Equal("to unlock Daily Challenge Ticket?", modalUnlockText.text);
        confirmButton.onClick.Invoke();

        Assert.Equal(500, catalog.GetCrystalBalance());
        Assert.Equal(0, catalog.GetDailyChallengeTicketBalance());
        Assert.True(confirmButton.enabled);
        Tick(160);
        Assert.Equal("0", ticketBalanceText.text);
    }

    [Fact]
    public void PurchaseCalls_RunOnlyTheFailureCallback()
    {
        int succeeded = 0, failed = 0;
        var item = CatalogManager.StoreShelve.captains.Values.First(x => x.Name == "AURELIA");

        catalog.PurchaseItem(item, item.Price[0], 1, () => succeeded++, () => failed++);
        catalog.PurchaseCaptainUpgrade(captainManager.GetCaptainByName("AURELIA"), () => succeeded++, () => failed++);

        Assert.Equal((0, 2), (succeeded, failed));
        Assert.Equal(500, catalog.GetCrystalBalance());
        Assert.Empty(CatalogManager.Inventory.captains);
    }

    [Fact]
    public void DailyRewardClaim_CreditsTheWallet_AndTheFanOutRestatesEveryCard()
    {
        // A wallet drawn down to 200 (authored, no event), then the daily claim:
        // +DailyRewardAmount → 300 through RewardClaimed → OnCurrencyBalanceChange.
        crystal.Amount = 200;
        DailyRewardHandler.Instance.Claim();

        Assert.Equal(200 + CatalogManager.DailyRewardAmount, catalog.GetCrystalBalance());

        // 300 affords AURELIA (150) and KORVAX (300) but not SIRRA (450).
        var korvax = Card("KORVAX");
        Assert.True(((Image)GetField(korvax, "PriceButton")).gameObject.activeSelf);
        Assert.True(korvax.GetComponent<Button>().enabled);
        var sirra = Card("SIRRA");
        Assert.True(((Image)GetField(sirra, "UnavailableButton")).gameObject.activeSelf);
        Assert.False(((Image)GetField(sirra, "PriceButton")).gameObject.activeSelf);
        Assert.False(sirra.GetComponent<Button>().enabled);

        Tick(70); // the crystal-balance count-up
        Assert.Equal("300", crystalBalanceText.text);
    }

    [Fact]
    public void UseDailyChallengeTicket_SpendsOne_AndTheCompletionReinventoriesIt()
    {
        int inventoryChanges = 0;
        CatalogManager.OnInventoryChange += () => inventoryChanges++;
        ticket.Amount = 2;

        catalog.UseDailyChallengeTicket();

        Assert.Equal(1, ticket.Amount);
        Assert.Contains(ticket, CatalogManager.Inventory.tickets);
        Assert.Equal(1, catalog.GetDailyChallengeTicketBalance());
        Assert.Equal(1, inventoryChanges);
    }

    [Fact]
    public void GetCrystalBalance_FiltersByElementTag()
    {
        Assert.Equal(500, catalog.GetCrystalBalance(Element.Omni));
        Assert.Equal(0, catalog.GetCrystalBalance(Element.Charge)); // no charge crystal held
    }

    /// <summary>Prefab-persistent Button.onClick stand-in (mirrors the menushell's binding).</summary>
    sealed class PurchaseCardClickBinding : MonoBehaviour
    {
        void Awake()
        {
            var button = GetComponent<Button>();
            var card = GetComponent<PurchaseCard>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(card.OnClickBuy);
        }
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
