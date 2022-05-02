using UnityEngine;

/// <summary>
/// Persistent Background Music through all scenes
/// Place script and AudioSource on GameObject in initial scene.
/// Written by John Zak
/// </summary>

public class BGMusicPersistent : MonoBehaviour
{
    private static BGMusicPersistent instance = null;
    public static BGMusicPersistent Instance { get { return instance; } }

    [SerializeField]
    private AudioSource bgMusicSource1;
    [SerializeField]
    private AudioSource bgMusicSource2;

    private bool isMuted = false;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        else
        {
            instance = this;
        }
        DontDestroyOnLoad(this.gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        bgMusicSource1 = this.gameObject.AddComponent<AudioSource>();
        bgMusicSource2 = this.gameObject.AddComponent<AudioSource>();
    }
    
    public void PlayMusicClip(AudioSource audioSource)
    {
        audioSource.volume = 1;
        audioSource.Play();
    }

    public void ToggleMute(AudioSource audioSource)
    {
        isMuted = !isMuted;
        audioSource.mute = isMuted;        
    }
}
