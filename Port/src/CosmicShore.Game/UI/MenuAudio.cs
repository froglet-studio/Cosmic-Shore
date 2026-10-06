// Ported from Assets/_Scripts/UI/MenuAudio.cs (Arc F) — verbatim; UnityEngine →
// CosmicShore.Engine, Reflex.Attributes → CosmicShore.Engine.Injection.
using CosmicShore.Core;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;

namespace CosmicShore.UI
{
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField] MenuAudioCategory category;

        [Inject] AudioSystem audioSystem;

        public void PlayAudio()
        {
            audioSystem.PlayMenuAudio(category);
        }
    }
}
