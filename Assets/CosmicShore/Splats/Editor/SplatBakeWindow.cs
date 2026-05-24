using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    public class SplatBakeWindow : EditorWindow
    {
        private string _plyPath;
        private string _outputAssetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet.asset";
        private SplatDecimateSettings _settings = SplatDecimateSettings.Default;
        private Vector2 _scroll;
        private string _lastReport;

        [MenuItem("Tools/Splats/Bake Prism Set")]
        public static void Open()
        {
            var w = GetWindow<SplatBakeWindow>("Splat -> Prism Set");
            w.minSize = new Vector2(420, 480);
        }

        [MenuItem("Tools/Splats/Run Synthetic Pipeline Test")]
        public static void RunSyntheticPipelineTest()
        {
            string outAssetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(50);
            var settings = SplatDecimateSettings.Default;
            settings.voxelSize = 0.1f;
            settings.maxPrisms = 1000;
            var points = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            Debug.Log($"[SplatBake][Synthetic] {report.ToLogString()}");
            SplatSyntheticTest.AssertDecodedReasonable(points);
            SaveBakedAsset(outAssetPath, points, report, settings, sourcePath: "<synthetic>");
            Debug.Log($"[SplatBake][Synthetic] Saved {points.Length} points to {outAssetPath}.");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _plyPath = EditorGUILayout.TextField(".ply path", _plyPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string chosen = EditorUtility.OpenFilePanel("Pick a .ply", string.IsNullOrEmpty(_plyPath) ? Application.dataPath : Path.GetDirectoryName(_plyPath), "ply");
                if (!string.IsNullOrEmpty(chosen)) _plyPath = chosen;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputAssetPath = EditorGUILayout.TextField("Asset path", _outputAssetPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Decode", EditorStyles.boldLabel);
            _settings.flipHandedness = EditorGUILayout.Toggle("Flip Handedness (Z)", _settings.flipHandedness);
            _settings.minOpacity = EditorGUILayout.Slider("Min Opacity (pre-decimate)", _settings.minOpacity, 0f, 1f);
            EditorGUILayout.HelpBox("Global size tweak (scaleMultiplier) lives on SplatPrismSpawner at spawn time so you can iterate without re-baking.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Crop", EditorStyles.boldLabel);
            _settings.cropMode = (SplatCropMode)EditorGUILayout.EnumPopup("Crop Mode", _settings.cropMode);
            if (_settings.cropMode == SplatCropMode.CenteredFraction)
            {
                _settings.cropFraction = EditorGUILayout.Slider("Crop Fraction", _settings.cropFraction, 0.01f, 1f);
            }
            else if (_settings.cropMode == SplatCropMode.ExplicitAABB)
            {
                _settings.cropMin = EditorGUILayout.Vector3Field("Crop Min", _settings.cropMin);
                _settings.cropMax = EditorGUILayout.Vector3Field("Crop Max", _settings.cropMax);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Decimate", EditorStyles.boldLabel);
            _settings.voxelSize = EditorGUILayout.FloatField("Voxel Size", _settings.voxelSize);
            _settings.maxPrisms = EditorGUILayout.IntField("Max Prisms (hard cap 10000)", _settings.maxPrisms);
            if (_settings.maxPrisms > SplatPrismSet.MaxPrisms)
                _settings.maxPrisms = SplatPrismSet.MaxPrisms;

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_plyPath));
            if (GUILayout.Button("Bake", GUILayout.Height(36)))
            {
                try
                {
                    Bake();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Bake failed", ex.Message, "OK");
                }
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Run Synthetic Pipeline Test"))
                RunSyntheticPipelineTest();

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Bake()
        {
            EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Parsing PLY ...", 0.05f);
            try
            {
                var imported = SplatPlyImporter.Load(_plyPath);
                EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Decoding + decimating ...", 0.5f);
                var points = SplatDecimator.DecimateToPoints(imported.splats, _settings, out var report);
                EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Saving asset ...", 0.9f);
                SaveBakedAsset(_outputAssetPath, points, report, _settings, _plyPath);
                _lastReport = report.ToLogString();
                Debug.Log($"[SplatBake] Saved {points.Length} prisms to {_outputAssetPath}\n{_lastReport}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static SplatPrismSet SaveBakedAsset(string assetPath, SplatPoint[] points, DecimateReport report, SplatDecimateSettings settings, string sourcePath)
        {
            if (points.Length > SplatPrismSet.MaxPrisms)
                throw new InvalidOperationException($"Refusing to save {points.Length} points; hard cap is {SplatPrismSet.MaxPrisms}.");

            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var existing = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(assetPath);
            var so = existing != null ? existing : ScriptableObject.CreateInstance<SplatPrismSet>();
            so.sourcePlyPath = sourcePath;
            so.parsedCount = report.parsed;
            so.afterCropCount = report.afterCrop;
            so.afterVoxelCount = report.afterVoxel;
            so.finalCount = report.finalCount;
            so.sourceBoundsMin = report.boundsMin;
            so.sourceBoundsMax = report.boundsMax;
            so.voxelSize = settings.voxelSize;
            so.flippedHandedness = settings.flipHandedness;
            so.points = points;

            if (existing == null)
                AssetDatabase.CreateAsset(so, assetPath);
            else
                EditorUtility.SetDirty(so);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return so;
        }
    }
}
