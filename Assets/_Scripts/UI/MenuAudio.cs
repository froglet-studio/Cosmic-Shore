using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Plays one authored menu sound. Almost always wired as a Button's PERSISTENT onClick
    /// listener, which is what makes the null-handling below load-bearing rather than defensive
    /// housekeeping.
    ///
    /// <para><b>A persistent listener that throws eats every runtime listener behind it.</b>
    /// <c>UnityEvent.Invoke</c> builds its call list as persistent-then-runtime
    /// (<c>InvokableCallList.PrepareInvoke</c>) and guards none of them, so an exception here
    /// aborts the invoke before the code that actually does something ever runs. On the arcade
    /// grid that was <c>ArcadeExploreView.SelectGame</c>: the card rendered, reported itself
    /// interactable, passed a raycast - and opened nothing.</para>
    ///
    /// <para>The way that happened is worth stating, because it will happen again: Reflex
    /// populates <c>[Inject]</c> for objects present at SCENE LOAD and for anything a call site
    /// explicitly injects. A UI object created at RUNTIME by <c>Instantiate</c> is neither, so
    /// its injected fields are null. The real fix is at the creating site
    /// (<c>GameObjectInjector.InjectRecursive</c>, which <c>ArcadeExploreView</c> now does), but
    /// that fix has to be remembered once per spawn site, and this component sits on the click
    /// path of most of the game's UI. So it also fails SAFE and LOUD here: the press keeps
    /// working, and the missing injection reports itself instead of hiding inside a dead
    /// button.</para>
    /// </summary>
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField] MenuAudioCategory category;

        [Inject] AudioSystem audioSystem;

        // Reported once per component, not once per press: a menu button is pressed repeatedly
        // and a per-press error is indistinguishable from spam.
        bool _reportedMissingSystem;

        public void PlayAudio()
        {
            // The static instance is the same object DI hands out; it is the fallback for an
            // object nobody injected, never a replacement for injecting it.
            var system = audioSystem ? audioSystem : AudioSystem.Instance;

            if (!system)
            {
                if (!_reportedMissingSystem)
                {
                    _reportedMissingSystem = true;
                    CSDebug.LogWarningFormat(
                        "{0} on '{1}' has no AudioSystem - no injection and no live instance, so " +
                        "this sound is silent. If this object was created at runtime, inject it at " +
                        "the creating site with GameObjectInjector.InjectRecursive.",
                        nameof(MenuAudio), name);
                }
                return;
            }

            if (!audioSystem && !_reportedMissingSystem)
            {
                _reportedMissingSystem = true;
                CSDebug.LogWarningFormat(
                    "{0} on '{1}' was never injected and is falling back to AudioSystem.Instance. " +
                    "A runtime-created UI object needs GameObjectInjector.InjectRecursive at its " +
                    "creating site - other [Inject] fields on the same object are still null.",
                    nameof(MenuAudio), name);
            }

            system.PlayMenuAudio(category);
        }
    }
}
