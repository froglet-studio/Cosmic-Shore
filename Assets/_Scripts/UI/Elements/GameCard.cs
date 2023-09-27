using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameCard : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] SO_GameList AllGames;
    [SerializeField] Sprite LockIcon;
    [SerializeField] public bool Locked;

    [Header("Placeholder Locations")]
    [SerializeField] TMP_Text GameTitle;
    [SerializeField] Image BackgroundImage;
    [SerializeField] Image LockImage;
    [SerializeField] int Index;
    public MiniGames GameMode { get; private set; }

    void Start()
    {
        GameMode = MiniGames.BlockBandit;
        UpdateCardView();
    }

    public void SetGameMode(MiniGames gameMode)
    {
        GameMode = gameMode;
        UpdateCardView();
    }

    void UpdateCardView()
    {
        SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == GameMode).FirstOrDefault();
        GameTitle.text = game.Name;
        BackgroundImage.sprite = game.CardBackground;

        if (Locked)
        {
            LockImage.sprite = LockIcon;
            LockImage.gameObject.SetActive(true);
        }
    }

    public void OnCardClicked()
    {
        // Add highlight boarder

        // Set active and show details
        //LoadoutView.ExpandLoadout(Index);

        Debug.Log($"GameCard - Clicked: Gamemode: {GameMode}");
    }
}