using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Applies the per-scene render cuts (<see cref="GraphicsCuts"/>: the device tier's and the quality
    /// preset's) as each scene loads, and again whenever the graphics settings are applied. Today that
    /// is the skybox swap: a scene authors its sky in its lighting settings (<c>RenderSettings.skybox</c>),
    /// so the replacement is put in place after every load and active-scene change, before the scene's
    /// first frame - and the authored sky is put back when a preset that baked it is lifted.
    ///
    /// The other Step 4 choices are applied where their owner already is: HDR in
    /// <c>GraphicsSettingsApplier.ApplyQuality</c>, the membrane capsule cap in
    /// <c>CapsuleMembrane.Awake</c>, the wormhole exact-view cap in <c>WormholeView</c>.
    /// <c>Docs/PLATFORM_UNIFICATION.md</c>, Step 4.
    ///
    /// Only the sky MATERIAL changes. Ambient light and reflections were baked from the authored sky
    /// and stay as they are, so a swapped scene is lit exactly as authored.
    /// </summary>
    public static class PlatformRenderApplier
    {
        /// <summary>The sky the active scene authored, and what this applier drew in its place (the
        /// same material when nothing was swapped) - so a later apply can put the authored one back.</summary>
        static Material s_authoredSky;
        static Material s_drawnSky;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_authoredSky = null;
            s_drawnSky = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Enter Play Mode runs without a domain reload, so the previous session's handlers are
            // still attached to these static events: detach before attaching, never stack.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySkybox();

        static void OnActiveSceneChanged(Scene previous, Scene next) => ApplySkybox();

        /// <summary>Draw the active scene's sky, or its replacement when a cut in force has one.</summary>
        public static void ApplySkybox()
        {
            // The handlers outlive Play (no domain reload), and an edit-mode active-scene change
            // would otherwise write the tier's sky into a scene someone is editing.
            if (!Application.isPlaying) return;

            // The sky now drawn is this applier's swap only while it is the last one drawn; a newly
            // loaded scene brings its own authored sky back with its lighting settings.
            var current = RenderSettings.skybox;
            var authored = current && current == s_drawnSky ? s_authoredSky : current;

            var replacement = GraphicsCuts.SkyboxReplacementFor(authored);
            var wanted = replacement ? replacement : authored;
            if (wanted != current) RenderSettings.skybox = wanted;

            s_authoredSky = authored;
            s_drawnSky = wanted;
        }
    }
}
