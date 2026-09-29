using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Test-scene diagnostic for <see cref="SafeAreaFitter"/>. Draws the live
    /// <see cref="Screen.safeArea"/>, resolution, orientation, and the anchors the watched fitter
    /// has applied, so the fitter can be judged against numbers rather than by eye.
    ///
    /// Scene-local harness for <c>SafeAreaFitterTestScene.unity</c> — do NOT put this on a shipping
    /// canvas. It uses IMGUI on purpose: no font asset, sprite, or TMP dependency, so the test scene
    /// stays a self-contained two-script scene, and it draws in the FULL screen rect (outside the
    /// safe area) which is exactly where the reference numbers belong.
    /// </summary>
    public class SafeAreaTestReadout : MonoBehaviour
    {
        [Tooltip("The fitter to report on. Leave empty to find one in the scene at Start.")]
        [SerializeField] private SafeAreaFitter watched;

        [Tooltip("Screen-space size of the readout box, in unscaled pixels.")]
        [SerializeField] private Vector2 boxSize = new Vector2(520f, 190f);

        private GUIStyle _style;

        private void Start()
        {
            if (!watched) watched = FindFirstObjectByType<SafeAreaFitter>();
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };

            Rect safeArea = Screen.safeArea;
            bool fullScreen = SafeAreaFitter.IsFullScreenSafeArea(safeArea, Screen.width, Screen.height);

            GUILayout.BeginArea(new Rect(12f, 12f, boxSize.x, boxSize.y));
            GUILayout.Label($"resolution   {Screen.width} x {Screen.height}", _style);
            GUILayout.Label($"orientation  {Screen.orientation}", _style);
            GUILayout.Label($"safeArea     x{safeArea.x:0} y{safeArea.y:0} w{safeArea.width:0} h{safeArea.height:0}", _style);
            GUILayout.Label($"full screen  {fullScreen}  (fitter no-op when true)", _style);

            if (watched)
            {
                var rt = (RectTransform)watched.transform;
                GUILayout.Label($"anchors      min {rt.anchorMin.x:0.0000},{rt.anchorMin.y:0.0000}  " +
                                $"max {rt.anchorMax.x:0.0000},{rt.anchorMax.y:0.0000}", _style);
                GUILayout.Label($"insets       {(watched.InsetsApplied ? "applied" : "none")}", _style);
            }
            else
            {
                GUILayout.Label("no SafeAreaFitter found in scene", _style);
            }
            GUILayout.EndArea();
        }
    }
}
