using UnityEngine;

[CreateAssetMenu(fileName = "New_Tutorial_Stage", menuName = "Create SO/Tutorial Stage")]
public class TutorialStage : ScriptableObject
{
    [SerializeField]
    private string stageName;

    [SerializeField]
    Vector3 mutonSpawnOffset = new Vector3(0, 0, 20);

    [SerializeField]
    Vector3 jailBlockSpawnOffset = new Vector3(0, 0, 20);

    [SerializeField]
    private float respawnDistance = 30f;

    [SerializeField]
    private float playTime = 0f;

    [SerializeField]
    private int maxAttempts;

    [SerializeField]
    private bool hasMuton;

    [SerializeField]
    private bool usesFuelBar;

    [SerializeField]
    private bool usesGyro;

    [SerializeField]
    private bool usesTrails;

    [SerializeField]
    private bool usesJailBlockWall;

    [SerializeField]
    private NarrationLine lineOne;

    [SerializeField]
    private NarrationLine promptNarrationLine;

    [SerializeField]
    private NarrationLine retryNarrationLine;

    [SerializeField]
    private NarrationLine failureNarrationLine;
    
    [SerializeField]
    private float lineOneDisplayTime;

    [SerializeField]
    private float retryLineDisplayTime;

    [SerializeField]
    private float failLineDisplayTime;

    private bool isStarted;
    private bool hasCompleted;
    private GameObject uiPanel;
    private int remainingAttempts;

    public string StageName { get => stageName; }
    public NarrationLine PromptLine { get => lineOne; }
    public NarrationLine FailureLine { get => failureNarrationLine; }
    public NarrationLine RetryLine { get => retryNarrationLine; }
    public float PromptLineDisplayTime { get => lineOneDisplayTime; }
    public float RetryLineDisplayTime { get => retryLineDisplayTime; }
    public float FailLineDisplayTime { get => failLineDisplayTime; }
    public bool IsStarted { get => isStarted; set => isStarted = value; }
    public bool HasRemainingAttempts { get => remainingAttempts > 0; }
    public bool HasMuton { get => hasMuton; set => hasMuton = value; }
    public bool UsesFuelBar { get => usesFuelBar; set => usesFuelBar = value; }
    public bool UsesGyro { get => usesGyro; set => usesGyro = value; }
    public bool UsesTrails { get => usesTrails; set => usesTrails = value; }
    public bool HasCompleted { get => hasCompleted; }
    public bool UsesJailBlockWall { get => usesJailBlockWall; }
    public float RespawnDistance { get => respawnDistance; }
    public float PlayTime { get => playTime; }
    public Vector3 MutonSpawnOffset { get => mutonSpawnOffset; }
    public Vector3 JailBlockSpawnOffset { get => jailBlockSpawnOffset; }
    public GameObject UiPanel { get => uiPanel; set => uiPanel = value; }

    public void Begin()
    {
        remainingAttempts = maxAttempts;
        isStarted = true;
        if(uiPanel != null) { 
            uiPanel.SetActive(true); 
        }
    }

    public void Retry()
    {
        remainingAttempts--;
    }

    public void End()
    {
        hasCompleted = true;
        if (uiPanel != null) { 
            uiPanel.SetActive(false);
        }
    }
}
