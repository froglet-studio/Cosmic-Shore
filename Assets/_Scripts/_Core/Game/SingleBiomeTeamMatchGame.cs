using UnityEngine;

public class SingleBiomeTeamMatchGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Player FriendlyAI;
    [SerializeField] Player HostileOne;
    [SerializeField] Player HostileTwo;
    //[SerializeField] Vector3 CrystalStartPosition;

    protected override void Start()
    {
        base.Start();

        gameMode = MiniGames.SingleBiomeTeamMatch;

        /*HostileOne.gameObject.SetActive(true);

        if (NumberOfPlayers == 2)
        {
            FriendlyAI.gameObject.SetActive(true);
            HostileTwo.gameObject.SetActive(true);
        }*/
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        TrailSpawner.NukeTheTrails();
        //Crystal.transform.position = CrystalStartPosition;
    }
}