using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Keeps every on-screen game camera's horizontal view at least as wide as the 16:9 view its
    /// field of view was authored for.
    ///
    /// <para>Every FOV in the project (the settings slider's 90°, the menu camera's poses, the speed
    /// tunnel's drop, the sniper scope) is a VERTICAL angle tuned on a 16:9 screen. Unity holds the
    /// vertical angle, so a narrower screen loses the sides. At 4:3 on an iPad the view was a quarter
    /// narrower and read as zoomed in: the menu's cell membrane filled half the screen. Wider
    /// screens (phones, ultrawide) were already right, because they only gain at the sides.</para>
    ///
    /// <para>The fix is Unity's own physical-camera gate fit: a 16:9 sensor with
    /// <see cref="Camera.GateFitMode.Overscan"/>.
    /// <list type="bullet">
    /// <item>16:9: identical to before.</item>
    /// <item>Wider: identical to before (the vertical angle holds).</item>
    /// <item>Narrower (4:3, 16:10): the 16:9 width is kept and the extra height is shown.</item>
    /// </list>
    /// <see cref="Camera.fieldOfView"/> keeps its meaning: setting it sets the focal length against
    /// the sensor's height, so every script that writes or reads it works unchanged. Screen-to-world
    /// rays use the same projection, so 3D picking (the menu crystal) stays exact.</para>
    ///
    /// <para>Applied from <see cref="RenderPipelineManager.beginCameraRendering"/>, so it covers
    /// every camera without a per-controller hook, including ones spawned later. Skipped: cameras
    /// rendering to a texture (previews and portals own their aspect), orthographic cameras, and
    /// non-game cameras (Scene view, previews). After the first frame the per-camera cost is three
    /// property reads.</para>
    /// </summary>
    public static class CameraAspectFit
    {
        /// <summary>The aspect every field of view in the project was authored at.</summary>
        public const float DesignAspect = 16f / 9f;

        /// <summary>A 16:9 sensor, 36 mm wide; only its aspect matters.</summary>
        public static readonly Vector2 SensorSize = new(36f, 36f / DesignAspect);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType != CameraType.Game || cam.targetTexture != null || cam.orthographic) return;
            Apply(cam);
        }

        /// <summary>
        /// Puts <paramref name="cam"/> on the 16:9 overscan gate, keeping its current vertical field
        /// of view. Idempotent and cheap when already applied.
        /// </summary>
        public static void Apply(Camera cam)
        {
            if (!cam) return;
            if (cam.usePhysicalProperties && cam.gateFit == Camera.GateFitMode.Overscan
                && cam.sensorSize == SensorSize) return;

            // Read before switching: turning physical properties on re-derives the angle from the
            // focal length, which would discard the authored value.
            float fov = cam.fieldOfView;
            cam.usePhysicalProperties = true;
            cam.sensorSize = SensorSize;
            cam.gateFit = Camera.GateFitMode.Overscan;
            cam.fieldOfView = fov;
        }

        /// <summary>
        /// The vertical field of view a camera with the authored <paramref name="designVerticalFov"/>
        /// actually shows at <paramref name="aspect"/> (width / height) under this fit. Pure;
        /// unit-tested.
        /// </summary>
        public static float EffectiveVerticalFov(float designVerticalFov, float aspect)
        {
            if (aspect >= DesignAspect || aspect <= 0f) return designVerticalFov;
            float halfTan = Mathf.Tan(designVerticalFov * 0.5f * Mathf.Deg2Rad) * DesignAspect / aspect;
            return 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
        }
    }
}
