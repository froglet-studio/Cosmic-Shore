using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="MenuAudio"/> is wired as a Button's PERSISTENT onClick listener across most of
    /// the game's UI, and <c>UnityEvent.Invoke</c> runs persistent listeners BEFORE runtime ones
    /// without guarding either (<c>InvokableCallList.PrepareInvoke</c> builds the call list as
    /// persistent-then-runtime). So a throw here does not merely lose a sound - it aborts the
    /// invoke and silently eats every runtime listener behind it.
    ///
    /// <para>That is not hypothetical. It is what made the arcade's 13th card unplayable for four
    /// rounds of debugging: <c>ArcadeExploreView.EnsureGridCapacity</c> creates a card row at
    /// RUNTIME, Reflex only injects objects present at scene load, so those cards' <c>[Inject]
    /// AudioSystem</c> was null, <c>PlayAudio</c> threw, and the runtime listener that opens the
    /// configure modal (<c>SelectGame</c>) never ran. The card rendered correctly, reported itself
    /// interactable and passed a raycast - it just did nothing.</para>
    ///
    /// <para>The root-cause fix is at the creating site (that method now calls
    /// <c>GameObjectInjector.InjectRecursive</c>), but that has to be remembered once per spawn
    /// site forever. This pins the second half: an un-injected MenuAudio must fail SAFE, so the
    /// next spawn site that forgets loses a sound instead of a button.</para>
    /// </summary>
    public class MenuAudioResilienceTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("MenuAudioTestSubject");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void UninjectedPlayAudio_DoesNotThrow()
        {
            // AddComponent is exactly what a runtime Instantiate produces from Reflex's point of
            // view: a live component nobody injected. Before the fix this threw a
            // NullReferenceException, which is the whole bug.
            var audio = _go.AddComponent<MenuAudio>();

            Assert.DoesNotThrow(() => audio.PlayAudio(),
                "An un-injected MenuAudio must not throw - it sits on the persistent onClick list " +
                "of most menu Buttons, and a throw there aborts UnityEvent.Invoke before the " +
                "runtime listener that actually does the work.");
        }

        [Test]
        public void UninjectedPlayAudio_IsQuietOnRepeatedPresses()
        {
            // A menu button is pressed over and over. The missing-injection report has to be
            // once per component, not once per press, or it is indistinguishable from spam and
            // gets ignored - which is how the original silence survived so long.
            var audio = _go.AddComponent<MenuAudio>();

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 25; i++) audio.PlayAudio();
            });
        }
    }
}
