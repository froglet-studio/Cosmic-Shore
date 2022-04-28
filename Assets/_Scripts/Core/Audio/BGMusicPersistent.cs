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

    private AudioSource bgMusicSource;

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
        bgMusicSource = this.gameObject.AddComponent<AudioSource>();
    }

    public void PlayMusicClip()
    {
        bgMusicSource.volume = 1;
        bgMusicSource.Play();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        bgMusicSource.mute = isMuted;        
    }
}
