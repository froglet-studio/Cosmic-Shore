using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Puts the project's URP asset back the way it was authored when Play ends.
    ///
    /// <c>GraphicsSettingsApplier.ApplyQuality</c> writes render scale, MSAA, the upscaling filter and
    /// - on a device tier that asks (MobileLow, or a tier simulated through FrogletTools ▸ Performance
    /// ▸ Device Tier) - HDR onto the ACTIVE pipeline asset at runtime. In a build that is in-memory
    /// only; in the Editor it is the asset itself, so without this a session that simulated MobileLow
    /// would leave <c>URP_Asset.asset</c> with HDR off, one save away from committing it for every
    /// platform. Snapshot on leaving Edit mode, restore on returning to it.
    /// <c>Docs/PLATFORM_UNIFICATION.md</c>, Step 4.
    /// </summary>
    [InitializeOnLoad]
    static class UrpAssetPlayModeRestore
    {
        // SessionState, not statics: it survives the domain reload a project with domain reload ON
        // runs between leaving Edit mode and entering Play.
        const string Key = "CosmicShore.UrpAssetPlayModeRestore.";

        static UrpAssetPlayModeRestore()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static UniversalRenderPipelineAsset ActiveAsset =>
            GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            var asset = ActiveAsset;
            if (!asset) return;

            if (change == PlayModeStateChange.ExitingEditMode)
            {
                SessionState.SetBool(Key + "Saved", true);
                SessionState.SetBool(Key + "Hdr", asset.supportsHDR);
                SessionState.SetFloat(Key + "RenderScale", asset.renderScale);
                SessionState.SetInt(Key + "Msaa", asset.msaaSampleCount);
                SessionState.SetInt(Key + "Upscaling", (int)asset.upscalingFilter);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "Saved", false))
            {
                asset.supportsHDR = SessionState.GetBool(Key + "Hdr", asset.supportsHDR);
                asset.renderScale = SessionState.GetFloat(Key + "RenderScale", asset.renderScale);
                asset.msaaSampleCount = SessionState.GetInt(Key + "Msaa", asset.msaaSampleCount);
                asset.upscalingFilter = (UpscalingFilterSelection)SessionState.GetInt(Key + "Upscaling",
                    (int)asset.upscalingFilter);
                SessionState.EraseBool(Key + "Saved");
            }
        }
    }
}
