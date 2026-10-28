using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>Where the camera is, what it looks at, its field of view and roll (degrees).</summary>
    public struct IntroPose
    {
        public Vector3 Position;
        public Vector3 LookAt;
        public float Fov;
        public float Roll;

        public static IntroPose Lerp(in IntroPose a, in IntroPose b, float t) => new()
        {
            Position = Vector3.Lerp(a.Position, b.Position, t),
            LookAt = Vector3.Lerp(a.LookAt, b.LookAt, t),
            Fov = Mathf.Lerp(a.Fov, b.Fov, t),
            Roll = Mathf.Lerp(a.Roll, b.Roll, t),
        };

        public Quaternion Rotation
        {
            get
            {
                var dir = LookAt - Position;
                var look = dir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;
                return Roll != 0f ? look * Quaternion.Euler(0f, 0f, Roll) : look;
            }
        }
    }

    /// <summary>
    /// Evaluates an <see cref="IntroShot"/> at a moment of its run. Pure, so the Intro Cam Lab's
    /// moves can be held to the same numbers in tests (IntroCameraMathTests).
    /// </summary>
    public static class IntroCameraMath
    {
        public const float MinFov = 8f, MaxFov = 125f;

        public static float Ease(IntroEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case IntroEase.Linear: return t;
                case IntroEase.InOut: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case IntroEase.Out: return 1f - Mathf.Pow(1f - t, 3f);
                case IntroEase.In: return t * t * t;
                case IntroEase.ExpoOut: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case IntroEase.ExpoIn: return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case IntroEase.Back:
                    const float c = 1.4f;
                    return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
                default: return t * t * (3f - 2f * t);
            }
        }

        /// <summary>
        /// The field of view that keeps a subject the same size on screen at <paramref name="distance"/>
        /// as it was at <paramref name="startDistance"/> with <paramref name="startFov"/>: the dolly zoom.
        /// </summary>
        public static float VertigoFov(float startDistance, float startFov, float distance)
        {
            if (distance <= 1e-4f) return MaxFov;
            float half = Mathf.Atan(startDistance * Mathf.Tan(startFov * 0.5f * Mathf.Deg2Rad) / distance);
            return Mathf.Clamp(2f * half * Mathf.Rad2Deg, MinFov, MaxFov);
        }

        /// <summary>The direction a cut frames its Nth target from (the artifact's pattern).</summary>
        public static Vector3 CutDirection(int i) =>
            new Vector3(Mathf.Cos(i * 2.1f), 0.3f + (i % 2) * 0.3f, Mathf.Sin(i * 2.1f)).normalized;

        /// <param name="u">Progress through the shot, 0 to 1 (not eased).</param>
        /// <param name="time">Seconds since the intro started; drives shake and lens breathing.</param>
        public static IntroPose Evaluate(IntroShot shot, float u, float time, IntroCameraScene scene)
        {
            float raw = Mathf.Clamp01(u);
            float e = Ease(shot.Ease, raw);
            float k = scene.Scale;
            float dk = shot.DistanceSpace == IntroOffsetSpace.Units || shot.DistanceSpace == IntroOffsetSpace.LocalUnits ? 1f : k;
            var pose = new IntroPose { Fov = Mathf.Lerp(shot.Fov0, shot.Fov1, e), Roll = Mathf.Lerp(shot.Roll0, shot.Roll1, e) };

            switch (shot.Kind)
            {
                case IntroShotKind.Move:
                    pose.Position = Vector3.Lerp(scene.Resolve(shot.From), scene.Resolve(shot.To), e);
                    pose.LookAt = Vector3.Lerp(scene.Resolve(shot.LookFrom), scene.Resolve(shot.LookTo), e);
                    break;

                case IntroShotKind.Hold:
                    pose.Position = scene.Resolve(shot.From);
                    pose.LookAt = scene.Resolve(shot.LookFrom);
                    break;

                case IntroShotKind.Orbit:
                {
                    var c = scene.Resolve(shot.OrbitCenter);
                    float a = Mathf.Lerp(shot.Angle0, shot.Angle1, e) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(shot.Radius0, shot.Radius1, e) * dk;
                    float h = Mathf.Lerp(shot.Height0, shot.Height1, e) * dk;
                    pose.Position = c + new Vector3(Mathf.Sin(a) * r, h, Mathf.Cos(a) * r);
                    pose.LookAt = c;
                    break;
                }

                case IntroShotKind.Path:
                {
                    var path = scene.PathFor(shot.Path, shot.RingRadius, shot.RingHeight);
                    float dir = Mathf.Sign(shot.PathEnd - shot.PathStart);
                    if (dir == 0f) dir = 1f;
                    float at = Mathf.Lerp(shot.PathStart, shot.PathEnd, e);
                    var fwd = path.TangentAt(at) * dir;
                    Frame(fwd, out var side, out var up);
                    var off = shot.PathOffset * k;
                    pose.Position = path.PointAt(at) + side * off.x + up * off.y + fwd * off.z;
                    pose.LookAt = path.PointAt(at + shot.PathAhead * dir) + up * (off.y * shot.PathLookUp) + side * (off.x * 0.6f);
                    if (shot.Bank != 0f)
                    {
                        var next = path.TangentAt(at + 0.012f * dir) * dir;
                        float bankRad = Mathf.Clamp(-Vector3.Dot(side, next) * shot.Bank * 14f, -0.55f, 0.55f);
                        pose.Roll += bankRad * Mathf.Rad2Deg;
                    }
                    break;
                }

                case IntroShotKind.Vertigo:
                {
                    var target = scene.Resolve(shot.VertigoTarget);
                    var dirV = shot.VertigoDirection.sqrMagnitude > 1e-8f ? shot.VertigoDirection.normalized : Vector3.back;
                    float d0 = shot.Distance0 * dk, d = Mathf.Lerp(shot.Distance0, shot.Distance1, e) * dk;
                    pose.Position = target + dirV * d;
                    pose.LookAt = target;
                    pose.Fov = VertigoFov(d0, shot.Fov0, d);
                    break;
                }

                case IntroShotKind.Cuts:
                {
                    int n = Mathf.Max(1, shot.CutCount);
                    float scaled = Mathf.Min(raw, 0.9999f) * n;
                    int i = Mathf.Min(n - 1, Mathf.FloorToInt(scaled));
                    float local = scaled - i;
                    var target = scene.AnchorPosition(shot.CutAnchor, i);
                    var cam = target + CutDirection(i) * (shot.CutDistance * dk);
                    var toward = (target - cam).normalized;
                    pose.Position = cam + toward * (shot.CutPush * dk * Ease(IntroEase.Out, local));
                    pose.LookAt = target;
                    pose.Fov = shot.Fov0 - local * shot.CutZoom;
                    break;
                }
            }

            pose.Roll += shot.Spin * e;
            if (shot.FovWaveAmplitude != 0f)
                pose.Fov += Mathf.Sin(time * shot.FovWaveFrequency) * shot.FovWaveAmplitude;
            pose.Fov = Mathf.Clamp(pose.Fov, MinFov, MaxFov);

            if (shot.Shake > 0f)
            {
                float amp = shot.Shake * k * (shot.ShakeDecay ? Mathf.Pow(1f - e, 1.5f) : 1f);
                pose.Position += new Vector3(
                    Mathf.Sin(time * 13.1f) * 0.7f + Mathf.Sin(time * 29.3f) * 0.3f,
                    Mathf.Sin(time * 17.3f + 1f) * 0.7f + Mathf.Sin(time * 31.7f) * 0.3f,
                    Mathf.Sin(time * 11.7f + 2f) * 0.6f) * amp;
                pose.LookAt += new Vector3(Mathf.Sin(time * 9.7f + 3f), Mathf.Sin(time * 8.3f + 1f), 0f) * (amp * 0.6f);
            }
            return pose;
        }

        static void Frame(Vector3 forward, out Vector3 side, out Vector3 up)
        {
            side = Vector3.Cross(forward, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            up = Vector3.Cross(side, forward).normalized;
        }
    }
}
