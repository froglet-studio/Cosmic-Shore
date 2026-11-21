using System;
using System.IO;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The escort sim's camera: with <c>-arkEscortShots &lt;dir&gt;</c> it renders the Ark of the first escort under way to
    /// PNGs (exterior, the tunnel, the leash shell, the HUD), so the Ark's look is checked from a batch run rather than by
    /// eye in a build. A tool, never a player path: <see cref="ArkEscortSim"/> adds it only when the argument is given.
    /// With <c>-arkEscortShotsOnly</c> the escort is given up once the shots are taken (a look-only run).
    /// </summary>
    public sealed class ArkEscortShots : MonoBehaviour
    {
        public const string ShotsArg = "-arkEscortShots";
        public const string ShotsOnlyArg = "-arkEscortShotsOnly";
        const int Width = 1600, Height = 900;
        const float SettleSeconds = 25f;

        string _dir;
        bool _only;

        public static void Install(string dir, bool only)
        {
            var go = new GameObject("ArkEscortShots");
            DontDestroyOnLoad(go);
            var shots = go.AddComponent<ArkEscortShots>();
            shots._dir = dir;
            shots._only = only;
            shots.RunAsync(shots.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                ArkEscortRun run = null;
                // An escort under way with its Ark laid.
                while (!ct.IsCancellationRequested)
                {
                    run = FindAnyObjectByType<ArkEscortRun>();
                    if (run && run.IsActive && run.Ark && run.Ark.AliveCount > 0) break;
                    await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: ct);
                }
                float until = Time.time + SettleSeconds;
                while (Time.time < until) await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);
                if (!run || !run.Ark) return;

                await TakeAll(run, ct);

                // The damage alarm on the HUD: the next hull loss, as the game view shows it (overlay UI included).
                var ark = run.Ark;
                bool lost = false;
                void OnLost(Prism p, Data.PrismKind k) => lost = true;
                ark.HullPrismLost += OnLost;
                float deadline = Time.unscaledTime + 240f;
                while (!lost && Time.unscaledTime < deadline && ark) await UniTask.Delay(250, DelayType.Realtime, cancellationToken: ct);
                if (ark) ark.HullPrismLost -= OnLost;
                if (lost)
                {
                    await UniTask.Delay(400, DelayType.Realtime, cancellationToken: ct);
                    ScreenCapture.CaptureScreenshot(Path.Combine(_dir, "hud_alarm.png"));
                    Log("hud_alarm.png (game view, after a hull loss)");
                }
                else Log("no hull loss in 240 s - no HUD alarm shot");
                await UniTask.Delay(1500, DelayType.Realtime, cancellationToken: ct);
                if (_only && run) run.GiveUp("shots taken");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log($"threw: {e}"); }
        }

        async UniTask TakeAll(ArkEscortRun run, CancellationToken ct)
        {
            var ark = run.Ark;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;   // a still Ark for every view (rendering does not need time)
            try
            {
                var main = Camera.main;
                // Ark-local poses: x right, y up, z nose (the model's frame).
                Shot(ark, main, "ark_front34", new Vector3(170f, 80f, 240f), Vector3.zero);
                Shot(ark, main, "ark_stern34", new Vector3(-190f, 75f, -250f), new Vector3(0f, 0f, -30f));
                Shot(ark, main, "ark_side", new Vector3(320f, 25f, 0f), Vector3.zero);
                Shot(ark, main, "ark_top", new Vector3(0f, 330f, -60f), new Vector3(0f, 0f, -10f));
                Shot(ark, main, "ark_below", new Vector3(140f, -210f, 120f), Vector3.zero);
                Shot(ark, main, "ark_stern_close", new Vector3(-40f, 18f, -150f), new Vector3(0f, -4f, -80f));
                Shot(ark, main, "ark_wide", new Vector3(500f, 220f, 650f), Vector3.zero, 50f);
                Shot(ark, main, "tunnel_mouth", new Vector3(0f, -12f, 150f), new Vector3(0f, -17.5f, 80f), 50f);
                Shot(ark, main, "tunnel_hangar", new Vector3(0f, -16f, 96f), new Vector3(0f, -17.5f, 30f), 75f);
                Shot(ark, main, "tunnel_inside", new Vector3(0f, -17.5f, 40f), new Vector3(0f, -18.5f, -20f), 75f);
                Shot(ark, main, "tunnel_exit", new Vector3(0f, -75f, -95f), new Vector3(0f, -30f, -48f), 50f);
                Shot(ark, main, "tunnel_exit_inside", new Vector3(0f, -31f, -50f), new Vector3(0f, -18f, -26f), 75f);
                LeashShots(ark, main);
            }
            finally { Time.timeScale = prevScale; }
            await UniTask.Yield(ct);
        }

        void LeashShots(Ark ark, Camera main)
        {
            var shell = ArkLeashShell.Current;
            if (!shell || shell.Radius <= 0f) { Log("no leash shell - no leash shots"); return; }
            float r = shell.Radius;
            // From just inside the surface, looking along it (the pilot about to cross), and from outside looking back.
            var outward = (ark.transform.right + ark.transform.up * 0.3f).normalized;
            var inside = ark.Position + outward * (r * 0.97f);
            shell.PreviewReveal(inside, 1f);
            ShotWorld(main, "leash_inside", inside - outward * 40f + ark.transform.forward * 10f,
                inside + outward * 60f + ark.transform.forward * 120f, 75f);
            var outside = ark.Position + outward * (r * 1.04f);
            shell.PreviewReveal(outside, 1f);
            ShotWorld(main, "leash_outside", outside + outward * 20f, ark.Position, 75f);
            shell.PreviewReveal(Vector3.zero, 0f);
        }

        void Shot(Ark ark, Camera main, string name, Vector3 localEye, Vector3 localTarget, float fov = 40f)
        {
            var t = ark.transform;
            ShotWorld(main, name, t.TransformPoint(localEye), t.TransformPoint(localTarget), fov, t.up);
        }

        void ShotWorld(Camera main, string name, Vector3 eye, Vector3 target, float fov, Vector3? up = null)
        {
            var go = new GameObject("ArkEscortShotCamera");
            var cam = go.AddComponent<Camera>();
            if (main) cam.CopyFrom(main);
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 20000f);
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, up ?? Vector3.up));
            var data = cam.GetUniversalAdditionalCameraData();
            var src = main ? main.GetUniversalAdditionalCameraData() : null;
            data.renderPostProcessing = true;
            if (src)
            {
                data.volumeLayerMask = src.volumeLayerMask;
                data.antialiasing = src.antialiasing;
            }
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
                Log($"{name}.png");
            }
            catch (Exception e) { Log($"{name} failed: {e.Message}"); }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                rt.Release();
                Destroy(rt);
                Destroy(go);
            }
        }

        static void Log(string line) => CSDebug.LogWarning($"[ArkEscortShots] {line}");
    }
}
