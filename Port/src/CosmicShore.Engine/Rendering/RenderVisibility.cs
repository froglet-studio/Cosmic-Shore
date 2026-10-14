namespace CosmicShore.Engine
{
    /// <summary>
    /// Port engine extension: <see cref="Renderer.isVisible"/> as Unity means it - "some camera drew this renderer in
    /// the last frame that was rendered" - rather than "it is enabled and active".
    ///
    /// The render backend calls <see cref="NoteCameraRender"/> as each camera starts drawing and
    /// <see cref="NoteSeenByCamera"/> on every renderer that passes that camera's frustum test. A renderer is visible
    /// while its stamp is the newest rendered frame's: any camera that frame counts, and a frame the game ticks without
    /// drawing (headless catch-up, a skipped present) keeps the last frame's answer, as Unity keeps it until it renders
    /// again. Nothing renders headless, so nothing is visible there - Unity's answer with no graphics device too.
    ///
    /// Why it matters: the game skips purely visual work on renderers no camera sees (the crystals' Charge arcs, flip
    /// wave, depth prisms and space squares read <c>isVisible</c>). With every renderer reading visible, a grown
    /// Bloomrush arena rebuilt every Charge crystal's arc mesh every frame and ran at 3-5 fps headless.
    /// </summary>
    public partial class Renderer
    {
        static int s_renderedFrame = -1;   // Time.frameCount of the newest rendered frame
        static long s_renderEpoch;         // advances once per rendered frame; 0 = nothing has ever rendered
        long _seenEpoch;

        /// <summary>A camera is about to draw (render backend). The first camera of a new frame opens a new epoch.</summary>
        public static void NoteCameraRender()
        {
            if (Time.frameCount == s_renderedFrame && s_renderEpoch > 0) return;
            s_renderedFrame = Time.frameCount;
            s_renderEpoch++;
        }

        /// <summary>This renderer passed the current camera's culling (render backend).</summary>
        public void NoteSeenByCamera() => _seenEpoch = s_renderEpoch;

        /// <summary>Drawn by a camera in the newest rendered frame (see the class remarks).</summary>
        bool SeenInLastRenderedFrame => s_renderEpoch > 0 && _seenEpoch == s_renderEpoch;

        /// <summary>Tests: forget every render (as at process start, before any camera has drawn).</summary>
        internal static void ResetRenderVisibility()
        {
            s_renderedFrame = -1;
            s_renderEpoch = 0;
        }
    }
}
