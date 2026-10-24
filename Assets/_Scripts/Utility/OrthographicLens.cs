using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Utility
{
    /// <summary>
    /// No orthographic game camera ever gets a Panini projection (Garrett, 2026-10-22: "make sure anyone else using
    /// orthographic view never applies a panini ... by default").
    ///
    /// <para><b>Why it is global.</b> Panini is a perspective lens: it bends a wide field of view back towards straight
    /// lines. On an orthographic picture there is no field of view to correct, so it only bows straight edges into curves
    /// (the Wrasse's map bowed its lower bar). The game's volumes carry Panini (the speed tunnel drives it on the shared
    /// profile, <c>PostProcessingManager.SetSpeedTunnelPanini</c>), and any camera that adopts the game's image
    /// (<see cref="OffscreenCameraSetup.AdoptGameCameraImage"/>) blends those volumes. So a new orthographic view would
    /// inherit the bow unless its author knew to remove it. This hook removes it for every one.</para>
    ///
    /// <para><b>How.</b> URP raises <c>beginCameraRendering</c> BEFORE it updates a camera's volumes. For an
    /// orthographic Game camera this switches the camera to its own volume stack
    /// (<see cref="VolumeFrameworkUpdateMode.ViaScripting"/>), updates that stack from the same volumes, mask and trigger
    /// URP would use, and holds <see cref="PaniniProjection"/>'s distance at 0 in it. Every other effect (bloom, the
    /// tonemapper, vignette) is untouched. It costs the same single volume update URP would have made. A camera that
    /// turns perspective again gets its old update mode back. A camera whose author already set ViaScripting keeps its
    /// own stack and updates; only the Panini in it is zeroed.</para>
    ///
    /// <para>Opt out per camera with <see cref="AllowPanini"/>. Nothing does today.</para>
    /// </summary>
    public static class OrthographicLens
    {
        // cameras this hook switched to ViaScripting, with the update mode to give back when they turn perspective
        static readonly Dictionary<Camera, VolumeFrameworkUpdateMode> s_switched = new();
        static readonly HashSet<Camera> s_allowed = new();
        static readonly List<Camera> s_dead = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            s_switched.Clear();
            s_allowed.Clear();
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.beginCameraRendering += Begin;
        }

        /// <summary>Let <paramref name="cam"/> keep the volumes' Panini while orthographic (<paramref name="allow"/> true),
        /// or put it back under this guard (false).</summary>
        public static void AllowPanini(Camera cam, bool allow)
        {
            if (!cam) return;
            if (allow) s_allowed.Add(cam);
            else s_allowed.Remove(cam);
        }

        static void Begin(ScriptableRenderContext _, Camera cam)
        {
            // in edit mode URP refreshes every camera's volumes itself (and drops a local stack), so there is nothing to hold
            if (!Application.isPlaying || !cam || cam.cameraType != CameraType.Game) return;
            if (!cam.TryGetComponent(out UniversalAdditionalCameraData data) || data.renderType != CameraRenderType.Base) return;

            if (!cam.orthographic || s_allowed.Contains(cam))
            {
                if (s_switched.Remove(cam, out var previous)) cam.SetVolumeFrameworkUpdateMode(previous);
                return;
            }

            if (!s_switched.ContainsKey(cam))
            {
                var mode = cam.GetVolumeFrameworkUpdateMode();
                if (data.requiresVolumeFrameworkUpdate)
                {
                    Prune();
                    s_switched[cam] = mode;
                    cam.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
                }
            }
            // ours: refresh it as URP would have; the author's own ViaScripting stack is theirs to refresh
            if (s_switched.ContainsKey(cam) || data.volumeStack == null) cam.UpdateVolumeStack(data);

            var stack = data.volumeStack;
            if (stack == null) return;
            var panini = stack.GetComponent<PaniniProjection>();
            if (panini) panini.distance.value = 0f;
        }

        // destroyed cameras leave the table (only when a new one joins, so the steady frame allocates nothing)
        static void Prune()
        {
            foreach (var cam in s_switched.Keys) if (!cam) s_dead.Add(cam);
            foreach (var cam in s_dead) s_switched.Remove(cam);
            s_dead.Clear();
            s_allowed.RemoveWhere(c => !c);
        }
    }
}
