using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Make a runtime, off-screen camera draw the way the GAME'S camera draws.
    ///
    /// <para><b>A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's DEFAULTS, not the
    /// project's</b> — no post-processing, no volume layer mask, SDR. This project's world is
    /// authored almost entirely HDR-emissive against the gameplay volume's tonemapper, so a camera
    /// that skips that renders a flat, colourless, near-black version of a world the game shows
    /// lit.</para>
    ///
    /// <para><b>This helper exists because that finding has now been rediscovered FOUR times</b> —
    /// <c>ModePreviewArena.AdoptGameCameraSettings</c>, <c>ConnectingArenaPreview.AdoptUrpSettings</c>,
    /// <c>ToyPreviewCamera.EnsureRig</c> and the Serpent's scope window — and every rediscovery cost
    /// a playtest, because <b>a picture that renders WRONG and a picture that does not render at all
    /// are the same report</b>. The first three each framed a bright subject (an arena, a lifeform,
    /// the pilot's own hull) and so read as merely low quality; the fourth framed open space, where
    /// the whole picture IS the skybox and the volume, and read as the window being gone.</para>
    ///
    /// <para><b>The load-bearing half lives on <see cref="UniversalAdditionalCameraData"/>, not on
    /// <see cref="Camera"/></b>: post-processing, the volume layer mask, anti-aliasing and shadows
    /// are all there. Copying the base <see cref="Camera"/> fields alone gets the framing and the
    /// clear right and none of the image — which is precisely the shape of the bug, since framing
    /// is the half that looks obviously wrong when you get it wrong.</para>
    ///
    /// <para><b>Post-processing defaults to ON, and that default is the rule.</b> Shadows and
    /// anti-aliasing are the two a small window may honestly decline — each is a whole extra pass
    /// and neither is legible at a few hundred pixels — but the tonemapper is not a quality
    /// setting here, it is what makes the image an image. A caller that switches it off is saying
    /// its window is competing with a full-screen render for the same frame, and should say so.</para>
    ///
    /// <para><b>Clip planes are deliberately NOT copied.</b> All four sites derive their own and
    /// each records why: a camera framing a whole arena from outside, a camera sitting on a hull,
    /// and a camera a few units from a toy want completely different planes, and a borrowed far
    /// plane clips the subject away — which, again, reads as the window being broken.</para>
    /// </summary>
    public static class OffscreenCameraSetup
    {
        /// <summary>
        /// Copy WHAT the game's camera sees and what it clears to — never how far it sees, which
        /// is the caller's own shot to derive.
        /// </summary>
        /// <param name="excludeUiLayer">Drop the UI layer from the culling mask. True for any
        /// window drawn on a canvas, or the preview renders the panel inside its own picture, one
        /// frame stale, forever.</param>
        public static void AdoptGameCameraFraming(Camera target, bool excludeUiLayer = true)
        {
            if (target == null) return;

            var source = Camera.main;
            if (source != null)
            {
                target.clearFlags = source.clearFlags;
                target.backgroundColor = source.backgroundColor;
                target.cullingMask = source.cullingMask;
            }
            else
            {
                target.clearFlags = CameraClearFlags.Skybox;
                target.cullingMask = ~0;
            }

            if (!excludeUiLayer) return;
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) target.cullingMask &= ~(1 << ui);
        }

        /// <summary>
        /// Copy HOW the game's camera draws: HDR, the volume layer mask and the post-processing
        /// stack. See the class summary on why post-processing defaults to on.
        /// </summary>
        public static void AdoptGameCameraImage(Camera target, bool postProcessing = true,
                                                bool antiAliasing = false, bool shadows = false)
        {
            if (target == null) return;

            var source = Camera.main;
            if (source != null)
            {
                target.allowHDR = source.allowHDR;
                target.allowMSAA = source.allowMSAA;
            }

            var to = target.GetUniversalAdditionalCameraData();
            if (to == null) return;

            var from = source != null ? source.GetUniversalAdditionalCameraData() : null;

            // The volume mask is what decides whether this camera is inside the game's post
            // profile at all. URP's default is layer 0 only, so an un-adopted camera can sit
            // outside the very volume the world is authored against.
            to.volumeLayerMask = from != null ? from.volumeLayerMask : (LayerMask)~0;
            to.renderPostProcessing = postProcessing && (from == null || from.renderPostProcessing);
            to.renderShadows = shadows && (from == null || from.renderShadows);
            to.antialiasing = antiAliasing && from != null ? from.antialiasing : AntialiasingMode.None;
            if (from != null) to.antialiasingQuality = from.antialiasingQuality;
        }
    }
}
