using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        enum BulkSpriteAnchorKind { Filament, LatchRing, Tether, FollowTransform, Root }

        Color ContrastingFilamentParticleColor(int filamentIndex)
        {
            if (filamentIndex == _currentFilamentIndex)
                return new Color(1f, 0.28f, 0.82f, 0.64f);
            if (filamentIndex == _currentFilamentIndex + 1)
                return new Color(1f, 0.68f, 0.18f, 0.58f);
            return new Color(0.62f, 0.58f, 1f, 0.46f);
        }

        Vector3 SampleLineRenderer(LineRenderer line, float t)
        {
            int last = line.positionCount - 1;
            if (last <= 0)
                return line.transform.position;

            float scaled = Mathf.Clamp01(t) * last;
            int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, last - 1);
            return Vector3.Lerp(line.GetPosition(index), line.GetPosition(index + 1), scaled - index);
        }

        void FaceCamera(Transform target)
        {
            if (!_mainCamera)
                return;

            Vector3 toCamera = _mainCamera.transform.position - target.position;
            if (toCamera.sqrMagnitude < 0.01f)
                return;

            target.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }

        sealed class BulkSpriteOverlayRuntime
        {
            public Transform Transform;
            public Renderer Renderer;
            public MaterialPropertyBlock Block;
            public BulkSpriteAnchorKind Anchor;
            public FilamentRuntime Filament;
            public Transform Follow;
            public Vector3 RootPosition;
            public Vector3 RootOutward;
            public Vector3 RootUp;
            public float RootAxis01;
            public float Distance;
            public float OrbitAngleRadians;
            public int RingIndex;
            public int TetherIndex;
            public float Ring01;
            public float Tether01;
            public Vector2 BaseScale;
            public Color Tint;
            public float Alpha;
            public float Glow;
            public float FrameRate;
            public float Phase;
        }
    }
}
