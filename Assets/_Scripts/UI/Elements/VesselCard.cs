using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VesselCard : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] Sprite LockIcon;
    [SerializeField] public bool Locked;

    [Header("Placeholder Locations")]
    [SerializeField] TMP_Text VesselName;
    [SerializeField] Image BorderImage;
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

    public SquadMenu SquadMenu;

    void Start()
    {
        UpdateCardView();
    }

    void UpdateCardView()
    {
        if (vessel == null) return;

        //SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
        VesselName.text = vessel.Name;
        BackgroundImage.sprite = vessel.Image;
        LockImage.sprite = LockIcon;
        LockImage.gameObject.SetActive(Locked);
    }

    public void OnCardClicked()
    {
        Debug.Log($"VesselCard - Clicked: Vessel Name: { vessel.Name }");

        if (SquadMenu != null)
        {
            SquadMenu.AssignVessel(vessel);
        }

        // Add highlight border
        BorderImage.color = Color.yellow;
    }

    public void OnUpgradeButtonClicked()
    {

    }
}