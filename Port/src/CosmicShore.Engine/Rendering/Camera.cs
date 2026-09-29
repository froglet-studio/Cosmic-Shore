using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }

    [Flags]
    public enum DepthTextureMode { None = 0, Depth = 1, DepthNormals = 2, MotionVectors = 4 }

    public enum CameraType { Game = 1, SceneView = 2, Preview = 4, VR = 8, Reflection = 16 }

    /// <summary>
    /// A camera (original contract: UnityEngine.Camera). Pose lives on the Transform; projection
    /// and target state live here. The window renderer draws <see cref="main"/> (or the highest-depth
    /// enabled camera without a target texture) every frame; a camera with a
    /// <see cref="targetTexture"/> is drawn off-screen by the renderer's <see cref="RenderRequested"/> hook.
    /// </summary>
    public class Camera : Behaviour
    {
        static readonly List<Camera> s_All = new();
        static Camera s_Main;

        /// <summary>First enabled camera tagged MainCamera (falls back to the first enabled camera).</summary>
        public static Camera main
        {
            get
            {
                if (s_Main != null && s_Main.isActiveAndEnabled) return s_Main;
                Camera fallback = null;
                foreach (var c in s_All)
                {
                    if (!c.isActiveAndEnabled) continue;
                    if (c.gameObject.tag == "MainCamera") return c;
                    fallback ??= c;
                }
                return s_Main ?? fallback;
            }
            internal set => s_Main = value;
        }

        public static Camera current { get; internal set; }
        public static Camera[] allCameras
        {
            get
            {
                var list = new List<Camera>();
                foreach (var c in s_All) if (c.isActiveAndEnabled) list.Add(c);
                return list.ToArray();
            }
        }
        public static int allCamerasCount => allCameras.Length;
        public static int GetAllCameras(Camera[] cameras)
        {
            var all = allCameras;
            int n = Math.Min(all.Length, cameras.Length);
            Array.Copy(all, cameras, n);
            return n;
        }

        public delegate void CameraCallback(Camera cam);
        public static CameraCallback onPreCull, onPreRender, onPostRender;

        /// <summary>Renderer hook: draw this camera into its target texture now (original: Camera.Render()).</summary>
        public static Action<Camera> RenderRequested;

        public bool useOcclusionCulling = true;
        public Color backgroundColor = new(0.192f, 0.302f, 0.475f, 0f);
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public bool orthographic;
        public float orthographicSize = 5f;
        public float depth;
        public int cullingMask = -1;
        public int eventMask = -1;
        public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        public RenderTexture targetTexture;
        public bool allowMSAA = true;
        public bool allowHDR = true;
        public bool allowDynamicResolution;
        public bool forceIntoRenderTexture;
        public int targetDisplay;
        public DepthTextureMode depthTextureMode;
        public CameraType cameraType = CameraType.Game;
        public Rect rect = new(0f, 0f, 1f, 1f);
        public float[] layerCullDistances = new float[32];
        public bool layerCullSpherical;
        public bool usePhysicalProperties;
        public Vector2 sensorSize = new(36f, 24f);
        public Vector2 lensShift;
        public float focalLength = 50f;
        public bool stereoEnabled => false;
        public Matrix4x4 cullingMatrix => projectionMatrix * worldToCameraMatrix;

        float? _aspect;
        Matrix4x4? _projection, _worldToCamera;

        public Camera()
        {
            s_All.Add(this);
        }

        internal override void DestroyComponentNow()
        {
            s_All.Remove(this);
            if (ReferenceEquals(s_Main, this)) s_Main = null;
            base.DestroyComponentNow();
        }

        public int pixelWidth => (int)MathF.Max(1f, (targetTexture != null ? targetTexture.width : Screen.width) * rect.width);
        public int pixelHeight => (int)MathF.Max(1f, (targetTexture != null ? targetTexture.height : Screen.height) * rect.height);
        public int scaledPixelWidth => pixelWidth;
        public int scaledPixelHeight => pixelHeight;
        public Rect pixelRect
        {
            get
            {
                float w = targetTexture != null ? targetTexture.width : Screen.width;
                float h = targetTexture != null ? targetTexture.height : Screen.height;
                return new Rect(rect.x * w, rect.y * h, rect.width * w, rect.height * h);
            }
            set
            {
                float w = targetTexture != null ? targetTexture.width : Screen.width;
                float h = targetTexture != null ? targetTexture.height : Screen.height;
                rect = new Rect(value.x / w, value.y / h, value.width / w, value.height / h);
            }
        }

        public float aspect
        {
            get => _aspect ?? (float)pixelWidth / pixelHeight;
            set => _aspect = value;
        }

        public void ResetAspect() => _aspect = null;

        public Matrix4x4 projectionMatrix
        {
            get => _projection ?? (orthographic
                ? Matrix4x4.Ortho(-orthographicSize * aspect, orthographicSize * aspect, -orthographicSize, orthographicSize, nearClipPlane, farClipPlane)
                : Matrix4x4.Perspective(fieldOfView, aspect, nearClipPlane, farClipPlane));
            set => _projection = value;
        }

        public Matrix4x4 nonJitteredProjectionMatrix { get => projectionMatrix; set => projectionMatrix = value; }
        public Matrix4x4 previousViewProjectionMatrix => projectionMatrix * worldToCameraMatrix;
        public void ResetProjectionMatrix() => _projection = null;

        /// <summary>World → camera space (OpenGL convention: camera looks down −Z).</summary>
        public Matrix4x4 worldToCameraMatrix
        {
            get
            {
                if (_worldToCamera.HasValue) return _worldToCamera.Value;
                var flipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
                return flipZ * Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
            }
            set => _worldToCamera = value;
        }

        public Matrix4x4 cameraToWorldMatrix => worldToCameraMatrix.inverse;
        public void ResetWorldToCameraMatrix() => _worldToCamera = null;
        public void ResetCullingMatrix() { }

        /// <summary>Oblique near-plane projection (clipPlane in camera space) — Lengyel's method, as the original.</summary>
        public Matrix4x4 CalculateObliqueMatrix(Vector4 clipPlane)
        {
            var m = projectionMatrix;
            var q = m.inverse * new Vector4(MathF.Sign(clipPlane.x), MathF.Sign(clipPlane.y), 1f, 1f);
            float dot = clipPlane.x * q.x + clipPlane.y * q.y + clipPlane.z * q.z + clipPlane.w * q.w;
            var c = clipPlane * (2f / dot);
            m.m20 = c.x - m.m30; m.m21 = c.y - m.m31; m.m22 = c.z - m.m32; m.m23 = c.w - m.m33;
            return m;
        }

        public Vector3 WorldToViewportPoint(Vector3 position)
        {
            var clip = projectionMatrix * (worldToCameraMatrix * new Vector4(position.x, position.y, position.z, 1f));
            float w = MathF.Abs(clip.w) < 1e-12f ? 1e-12f : clip.w;
            var cam = worldToCameraMatrix.MultiplyPoint3x4(position);
            return new Vector3(clip.x / w * 0.5f + 0.5f, clip.y / w * 0.5f + 0.5f, -cam.z);
        }

        public Vector3 WorldToScreenPoint(Vector3 position)
        {
            var v = WorldToViewportPoint(position);
            var r = pixelRect;
            return new Vector3(r.x + v.x * r.width, r.y + v.y * r.height, v.z);
        }

        public Vector3 ViewportToWorldPoint(Vector3 position)
        {
            float z = position.z;
            var ndc = new Vector4(position.x * 2f - 1f, position.y * 2f - 1f, 0f, 1f);
            var inv = (projectionMatrix * worldToCameraMatrix).inverse;
            // A point on the ray through the viewport position, then scaled to the requested depth.
            var near = inv * new Vector4(ndc.x, ndc.y, -1f, 1f);
            var far = inv * new Vector4(ndc.x, ndc.y, 1f, 1f);
            var n = new Vector3(near.x, near.y, near.z) / near.w;
            var f = new Vector3(far.x, far.y, far.z) / far.w;
            var dir = (f - n).normalized;
            var fwd = transform.forward;
            float along = Vector3.Dot(dir, fwd);
            if (orthographic)
                return n + fwd * (z - nearClipPlane);
            var origin = transform.position;
            return origin + dir * (MathF.Abs(along) < 1e-6f ? z : z / along);
        }

        public Vector3 ScreenToWorldPoint(Vector3 position)
        {
            var r = pixelRect;
            return ViewportToWorldPoint(new Vector3((position.x - r.x) / r.width, (position.y - r.y) / r.height, position.z));
        }

        public Vector3 ScreenToViewportPoint(Vector3 position)
        {
            var r = pixelRect;
            return new Vector3((position.x - r.x) / r.width, (position.y - r.y) / r.height, position.z);
        }

        public Vector3 ViewportToScreenPoint(Vector3 position)
        {
            var r = pixelRect;
            return new Vector3(r.x + position.x * r.width, r.y + position.y * r.height, position.z);
        }

        public Ray ViewportPointToRay(Vector3 position)
        {
            var p = ViewportToWorldPoint(new Vector3(position.x, position.y, nearClipPlane));
            var q = ViewportToWorldPoint(new Vector3(position.x, position.y, farClipPlane));
            return new Ray(p, q - p);
        }

        public Ray ScreenPointToRay(Vector3 position)
            => ViewportPointToRay(ScreenToViewportPoint(position));

        public Ray ScreenPointToRay(Vector2 position) => ScreenPointToRay((Vector3)position);

        public void CalculateFrustumCorners(Rect viewport, float z, MonoOrStereoscopicEye eye, Vector3[] outCorners)
        {
            outCorners[0] = transform.InverseTransformPoint(ViewportToWorldPoint(new Vector3(viewport.xMin, viewport.yMin, z)));
            outCorners[1] = transform.InverseTransformPoint(ViewportToWorldPoint(new Vector3(viewport.xMin, viewport.yMax, z)));
            outCorners[2] = transform.InverseTransformPoint(ViewportToWorldPoint(new Vector3(viewport.xMax, viewport.yMax, z)));
            outCorners[3] = transform.InverseTransformPoint(ViewportToWorldPoint(new Vector3(viewport.xMax, viewport.yMin, z)));
        }

        public static float FieldOfViewToFocalLength(float fieldOfView, float sensorSize) => sensorSize * 0.5f / MathF.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
        public static float FocalLengthToFieldOfView(float focalLength, float sensorSize) => 2f * MathF.Atan(sensorSize * 0.5f / focalLength) * Mathf.Rad2Deg;
        public static float HorizontalToVerticalFieldOfView(float horizontalFieldOfView, float aspectRatio)
            => 2f * MathF.Atan(MathF.Tan(horizontalFieldOfView * Mathf.Deg2Rad * 0.5f) / aspectRatio) * Mathf.Rad2Deg;
        public static float VerticalToHorizontalFieldOfView(float verticalFieldOfView, float aspectRatio)
            => 2f * MathF.Atan(MathF.Tan(verticalFieldOfView * Mathf.Deg2Rad * 0.5f) * aspectRatio) * Mathf.Rad2Deg;

        /// <summary>Draw this camera now (into <see cref="targetTexture"/> when set) through the renderer hook.</summary>
        public void Render()
        {
            current = this;
            onPreCull?.Invoke(this);
            onPreRender?.Invoke(this);
            RenderRequested?.Invoke(this);
            onPostRender?.Invoke(this);
            current = null;
        }

        public void RenderWithShader(Shader shader, string replacementTag) => Render();
        public void SetReplacementShader(Shader shader, string replacementTag) { }
        public void ResetReplacementShader() { }

        public void CopyFrom(Camera other)
        {
            if (other == null) return;
            fieldOfView = other.fieldOfView; nearClipPlane = other.nearClipPlane; farClipPlane = other.farClipPlane;
            orthographic = other.orthographic; orthographicSize = other.orthographicSize; depth = other.depth;
            cullingMask = other.cullingMask; clearFlags = other.clearFlags; backgroundColor = other.backgroundColor;
            rect = other.rect; allowHDR = other.allowHDR; allowMSAA = other.allowMSAA; targetTexture = other.targetTexture;
            transform.position = other.transform.position; transform.rotation = other.transform.rotation;
        }

        public enum MonoOrStereoscopicEye { Left = 0, Right = 1, Mono = 2 }
    }
}
