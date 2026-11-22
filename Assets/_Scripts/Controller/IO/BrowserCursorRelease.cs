using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The browser's Escape, read as <see cref="OverviewGesture"/>. While the mouse is locked a browser
    /// keeps Escape for itself: it releases the pointer lock and never hands the key to the page, so
    /// in the WebGL build a mouse pilot pressed Escape, got the cursor back and could not leave a game
    /// or the freestyle flight (Shombith, 2026-11-22: "no way to exit the web game").
    ///
    /// <para>A release the GAME asks for (the overview opening and pausing the input strategy) happens
    /// inside a frame; the browser's happens between frames, in its own event loop. So the state is
    /// read twice a frame: first thing (order -32000) and last thing (order 32000). A lock held at the
    /// start and at the end of one frame and gone at the start of the next was taken by the browser.
    /// Only the WebGL player runs this; everywhere else Escape reaches the game.</para>
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class BrowserCursorRelease : MonoBehaviour
    {
        static int s_frame = -1;
        static bool s_releasedThisFrame;

        bool _lockedAtStart;
        bool _lockedAtEnd;

        /// <summary>True on the frame the browser released the mouse the game had locked.</summary>
        public static bool ReleasedThisFrame => s_frame == Time.frameCount && s_releasedThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_frame = -1;
            s_releasedThisFrame = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var go = new GameObject("[BrowserCursorRelease]");
            DontDestroyOnLoad(go);
            go.AddComponent<BrowserCursorRelease>();
            go.AddComponent<EndOfFrame>().Owner = go.GetComponent<BrowserCursorRelease>();
        }

        void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            s_releasedThisFrame = _lockedAtStart && _lockedAtEnd && !locked;
            s_frame = Time.frameCount;
            _lockedAtStart = locked;
        }

        void RecordEnd() => _lockedAtEnd = Cursor.lockState == CursorLockMode.Locked;

        /// <summary>The end-of-frame read, after every game script has set the cursor it wants.</summary>
        [DefaultExecutionOrder(32000)]
        sealed class EndOfFrame : MonoBehaviour
        {
            public BrowserCursorRelease Owner;
            void LateUpdate() { if (Owner) Owner.RecordEnd(); }
        }
    }
}
