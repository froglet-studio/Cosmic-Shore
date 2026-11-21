using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Writes what the player sees to a PNG, with no Game view: every enabled camera that draws to the screen is
    /// rendered in depth order into one RenderTexture, then every Screen Space Overlay canvas (the menus and the HUD,
    /// which otherwise draw straight to the screen after the cameras) through a temporary orthographic UI camera for
    /// one frame. It is the look-sheet kit's one capture (Docs/SKILL_TRAINING_PLAN.md section 3.1,
    /// Docs/LOOK_SHEET.md): a session that cannot open an editor reads the sheet this makes before anyone else does.
    ///
    /// <para>Works in batchmode as long as Unity has a graphics device (no <c>-nographics</c>; under Xvfb on a GPU-less
    /// machine Mesa's software renderer is enough for a sheet). When a Game view exists it also asks for the Game
    /// view's own screenshot (<see cref="CaptureGameView"/>), so the two methods can be compared on the first run.</para>
    ///
    /// <para>Known limits, to confirm on the first run with a real editor: a URP camera stack's overlay cameras do not
    /// render through <c>Camera.Render()</c>, so what they draw is missing; a canvas whose children sit on other
    /// layers than the canvas itself is drawn only for the canvas's layer.</para>
    /// </summary>
    public static class LookSheetCapture
    {
        [Serializable]
        public sealed class Record
        {
            public string label = "";
            public string path = "";
            public string scene = "";
            public string method = "";
            public string error = "";
            public int width;
            public int height;
            public int cameras;
            public int overlayCanvases;
            public int frame;
            public float gameTime;
            public string stamp = "";

            public bool Ok => string.IsNullOrEmpty(error);
        }

        /// <summary>Renders the scene and the overlay canvases into a PNG of the given size.</summary>
        public static Record Capture(string path, int width, int height, string label)
        {
            var record = new Record
            {
                label = label ?? "",
                path = path,
                width = width,
                height = height,
                method = "render-to-texture",
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                frame = Time.frameCount,
                gameTime = Time.time,
                stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            RenderTexture rt = null;
            Texture2D tex = null;
            Camera uiCamera = null;
            var retargeted = new List<(Canvas canvas, RenderMode mode, Camera camera, float plane)>();
            var previousActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                // Black first, so a frame nothing rendered into is visibly blank and verify.py fails it.
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = previousActive;

                var cameras = Camera.allCameras
                    .Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null)
                    .OrderBy(c => c.depth)
                    .ToList();
                foreach (var camera in cameras)
                {
                    var previousTarget = camera.targetTexture;
                    camera.targetTexture = rt;
                    try { camera.Render(); }
                    finally { camera.targetTexture = previousTarget; }
                }
                record.cameras = cameras.Count;

                var overlays = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                    .Where(c => c.isActiveAndEnabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    .OrderBy(c => c.sortingOrder)
                    .ToList();
                if (overlays.Count > 0)
                {
                    var go = new GameObject("[LookSheet UI camera]") { hideFlags = HideFlags.HideAndDontSave };
                    uiCamera = go.AddComponent<Camera>();
                    uiCamera.clearFlags = CameraClearFlags.Depth;
                    uiCamera.orthographic = true;
                    uiCamera.nearClipPlane = 0.01f;
                    uiCamera.farClipPlane = 100f;
                    uiCamera.depth = 1000f;
                    uiCamera.targetTexture = rt;
                    int mask = 0;
                    foreach (var canvas in overlays)
                    {
                        retargeted.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = uiCamera;
                        canvas.planeDistance = 1f;
                        mask |= 1 << canvas.gameObject.layer;
                    }
                    uiCamera.cullingMask = mask;
                    Canvas.ForceUpdateCanvases();
                    uiCamera.Render();
                    record.overlayCanvases = overlays.Count;
                }

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false);
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            catch (Exception e)
            {
                record.error = e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                RenderTexture.active = previousActive;
                foreach (var t in retargeted)
                {
                    if (t.canvas == null) continue;
                    t.canvas.renderMode = t.mode;
                    t.canvas.worldCamera = t.camera;
                    t.canvas.planeDistance = t.plane;
                }
                if (uiCamera != null) UnityEngine.Object.DestroyImmediate(uiCamera.gameObject);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
            return record;
        }

        /// <summary>
        /// The Game view's own screenshot, when there is a Game view (never in batchmode). Unity writes the file on a
        /// later frame, so the record only says it was asked for; verify.py checks the file.
        /// </summary>
        public static Record CaptureGameView(string path, int superSize, string label)
        {
            var record = new Record
            {
                label = label ?? "",
                path = path,
                method = "game-view",
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                frame = Time.frameCount,
                gameTime = Time.time,
                stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            if (Application.isBatchMode)
            {
                record.error = "no Game view in batchmode";
                return record;
            }
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, superSize));
            }
            catch (Exception e)
            {
                record.error = e.GetType().Name + ": " + e.Message;
            }
            return record;
        }

        /// <summary>Appends one record as a JSON line to <c>manifest.jsonl</c> in the output folder.</summary>
        public static void Append(string outDir, Record record)
        {
            try
            {
                Directory.CreateDirectory(outDir);
                File.AppendAllText(Path.Combine(outDir, "manifest.jsonl"), JsonUtility.ToJson(record) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LookSheet] manifest not written: " + e.Message);
            }
        }
    }
}
