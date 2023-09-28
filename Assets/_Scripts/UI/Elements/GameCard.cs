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

    MiniGames gameMode;
    public MiniGames GameMode 
    {
        get { return gameMode; }
        set
        {
            gameMode = value;
            UpdateCardView();
        }
    }

    void Start()
    {
        if (gameMode == MiniGames.Random)
            gameMode = MiniGames.BlockBandit;

        UpdateCardView();
    }

    void UpdateCardView()
    {
        SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
        GameTitle.text = game.Name;
        BackgroundImage.sprite = game.CardBackground;
        LockImage.sprite = LockIcon;
        LockImage.gameObject.SetActive(Locked);
    }

    public void OnCardClicked()
    {
        // Add highlight boarder

        // Set active and show details
        //LoadoutView.ExpandLoadout(Index);

        Debug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
    }
}