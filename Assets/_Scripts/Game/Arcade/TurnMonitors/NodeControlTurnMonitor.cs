using CosmicShore.Game.Arcade;
using UnityEngine;

/// <summary>
/// Composite turn monitor that ends when control of the node has been lost for TimeBasedTurnMonitor seconds
/// </summary>
public class NodeControlTurnMonitor : TimeBasedTurnMonitor
{
    [SerializeField] private Node monitoredNode;
    private MiniGame game;
    private Teams playerTeam;

    protected override void Start()
    {
        base.Start(); // Call parent Start() to initialize UI colors

        game = GetComponent<MiniGame>();
        if (game != null && game.ActivePlayer != null)
        {
            playerTeam = game.ActivePlayer.Team;
        }
    }

    public override bool CheckForEndOfTurn()
    {
        if (paused) return false;

        if (timerActive && elapsedTime > duration)
        {
            return true; // The turn ends when the timer expires
        }

        return false;
    }

    protected override void Update()
    {
        if (paused) return;

        // If the player loses control, start the countdown
        if (monitoredNode.ControllingTeam != playerTeam)
        {
            if (!timerActive)
            {
                ShowTimer();
                StartTimer();
            }
        }
        else
        {
            // If the player regains control, stop and reset the timer
            if (timerActive)
            {
                StopTimer();
                HideTimer();
            }
        }

        base.Update(); // Let `TimeBasedTurnMonitor` handle timer updates
    }

    void ShowTimer()
    {
        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) uiImages[i].gameObject.SetActive(true);

        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) uiTexts[i].gameObject.SetActive(true);
    }

    void HideTimer()
    {
        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) uiImages[i].gameObject.SetActive(false);

        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) uiTexts[i].gameObject.SetActive(false);
    }

    public override void NewTurn(string playerName)
    {
        base.NewTurn(playerName); // Reset timer when a new turn starts

        if (game != null && game.ActivePlayer != null)
        {
            playerTeam = game.ActivePlayer.Team;
        }
    }
}
