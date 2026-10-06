using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Puts the project's URP assets back the way they were authored when Play ends.
    ///
    /// <c>GraphicsSettingsApplier.ApplyQuality</c> writes render scale, MSAA, the upscaling filter and
    /// - on a device tier that asks (MobileLow, or a tier simulated through FrogletTools ▸ Performance
    /// ▸ Device Tier) - HDR onto the ACTIVE pipeline asset at runtime. In a build that is in-memory
    /// only; in the Editor it is the asset itself, so without this a session that simulated MobileLow
    /// would leave <c>URP_Asset.asset</c> with HDR off, one save away from committing it for every
    /// platform. Snapshot on leaving Edit mode, restore on returning to it.
    ///
    /// EVERY URP asset Play could make active is snapshotted - the default pipeline and each quality
    /// level's own - because the applier changes the quality level and then writes whichever asset
    /// that level resolves to. Today every level inherits the default; a per-level mobile asset would
    /// otherwise be written and never restored. <c>Docs/PLATFORM_UNIFICATION.md</c>, Step 4.
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

        /// <summary>The default pipeline asset and every quality level's, each once.</summary>
        static List<UniversalRenderPipelineAsset> PipelineAssets()
        {
            var assets = new List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main)
                assets.Add(main);
            for (int i = 0; i < QualitySettings.count; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset level
                    && !assets.Contains(level))
                    assets.Add(level);
            return assets;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                var guids = new List<string>();
                foreach (var asset in PipelineAssets())
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
                        continue;
                    guids.Add(guid);
                    SessionState.SetBool(Key + guid + ".Hdr", asset.supportsHDR);
                    SessionState.SetFloat(Key + guid + ".RenderScale", asset.renderScale);
                    SessionState.SetInt(Key + guid + ".Msaa", asset.msaaSampleCount);
                    SessionState.SetInt(Key + guid + ".Upscaling", (int)asset.upscalingFilter);
                }
                SessionState.SetString(Key + "Assets", string.Join(";", guids));
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                string saved = SessionState.GetString(Key + "Assets", string.Empty);
                if (string.IsNullOrEmpty(saved)) return;

                foreach (string guid in saved.Split(';'))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (!asset) continue;
                    asset.supportsHDR = SessionState.GetBool(Key + guid + ".Hdr", asset.supportsHDR);
                    asset.renderScale = SessionState.GetFloat(Key + guid + ".RenderScale", asset.renderScale);
                    asset.msaaSampleCount = SessionState.GetInt(Key + guid + ".Msaa", asset.msaaSampleCount);
                    asset.upscalingFilter = (UpscalingFilterSelection)SessionState.GetInt(
                        Key + guid + ".Upscaling", (int)asset.upscalingFilter);
                }
                SessionState.EraseString(Key + "Assets");
            }
        }
    }
}
