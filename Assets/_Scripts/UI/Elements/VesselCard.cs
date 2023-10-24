using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VesselCard : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] SO_GameList AllGames;
    [SerializeField] Sprite LockIcon;
    [SerializeField] public bool Locked;

    [Header("Placeholder Locations")]
    [SerializeField] TMP_Text VesselName;
    [SerializeField] Image BackgroundImage;
    [SerializeField] Image LockImage;
    [SerializeField] int Index;

    SO_Vessel vessel;
    public SO_Vessel Vessel
    {
        get { return vessel; }
        set
        {
            vessel = value;
            UpdateCardView();
        }
    }

    void Start()
    {
        UpdateCardView();
    }

    void UpdateCardView()
    {
        //SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
        //GameTitle.text = game.Name;
        //BackgroundImage.sprite = game.CardBackground;
        //LockImage.sprite = LockIcon;
        //LockImage.gameObject.SetActive(Locked);
    }

    public void OnCardClicked()
    {
        // Add highlight boarder

        // Set active and show details
        //LoadoutView.ExpandLoadout(Index);

        //Debug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
    }
}