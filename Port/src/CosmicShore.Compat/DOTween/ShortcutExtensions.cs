using System;
using CosmicShore.Engine;

namespace DG.Tweening
{
    /// <summary>
    /// Shortcut tweens on engine types (DOTween's ShortcutExtensions): each creates a
    /// playing tween whose <c>target</c> is the receiver, so <c>DOKill()</c> /
    /// <c>DOTween.Kill(receiver)</c> find it.
    /// </summary>
    public static class ShortcutExtensions
    {
        static T Target<T>(T t, object target) where T : Tween
        {
            t.target = target;
            return t;
        }

        static TweenerCore<Vector3> Snap(TweenerCore<Vector3> t, bool snapping) { t.snapping = snapping; return t; }

        // ── Transform: position ──

        public static TweenerCore<Vector3> DOMove(this Transform target, Vector3 endValue, float duration, bool snapping = false)
            => Target(Snap(DOTween.To(() => target.position, v => target.position = v, endValue, duration), snapping), target);

        public static TweenerCore<float> DOMoveX(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 0, false);
        public static TweenerCore<float> DOMoveY(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 1, false);
        public static TweenerCore<float> DOMoveZ(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 2, false);

        public static TweenerCore<Vector3> DOLocalMove(this Transform target, Vector3 endValue, float duration, bool snapping = false)
            => Target(Snap(DOTween.To(() => target.localPosition, v => target.localPosition = v, endValue, duration), snapping), target);

        public static TweenerCore<float> DOLocalMoveX(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 0, true);
        public static TweenerCore<float> DOLocalMoveY(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 1, true);
        public static TweenerCore<float> DOLocalMoveZ(this Transform target, float endValue, float duration, bool snapping = false)
            => Axis(target, endValue, duration, snapping, 2, true);

        static TweenerCore<float> Axis(Transform target, float endValue, float duration, bool snapping, int axis, bool local)
        {
            DOGetter<float> get = () =>
            {
                var p = local ? target.localPosition : target.position;
                return axis == 0 ? p.x : axis == 1 ? p.y : p.z;
            };
            DOSetter<float> set = v =>
            {
                var p = local ? target.localPosition : target.position;
                if (axis == 0) p.x = v; else if (axis == 1) p.y = v; else p.z = v;
                if (local) target.localPosition = p; else target.position = p;
            };
            var t = DOTween.To(get, set, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        // ── Transform: rotation ──

        public static TweenerCore<Vector3> DORotate(this Transform target, Vector3 endValue, float duration, RotateMode mode = RotateMode.Fast)
            => Rotate(target, endValue, duration, mode, false);

        public static TweenerCore<Vector3> DOLocalRotate(this Transform target, Vector3 endValue, float duration, RotateMode mode = RotateMode.Fast)
            => Rotate(target, endValue, duration, mode, true);

        static TweenerCore<Vector3> Rotate(Transform target, Vector3 endValue, float duration, RotateMode mode, bool local)
        {
            if (mode == RotateMode.Fast || mode == RotateMode.FastBeyond360)
            {
                var plugin = new EulerPlugin(mode == RotateMode.Fast);
                var t = DOTween.Make(
                    () => local ? target.localEulerAngles : target.eulerAngles,
                    v => { if (local) target.localRotation = Quaternion.Euler(v); else target.rotation = Quaternion.Euler(v); },
                    endValue, duration, plugin);
                return Target(t, target);
            }

            // Additive modes: tween an accumulated euler offset applied on top of the rotation
            // captured at startup (local axes → post-multiply, world axes → pre-multiply).
            Quaternion baseRotation = Quaternion.identity;
            var add = DOTween.Make(
                () => new Vector3(0f, 0f, 0f),
                v =>
                {
                    var r = mode == RotateMode.LocalAxisAdd
                        ? baseRotation * Quaternion.Euler(v)
                        : Quaternion.Euler(v) * baseRotation;
                    if (local) target.localRotation = r; else target.rotation = r;
                },
                endValue, duration, Plugins.Vector3);
            add.onStartupInternal = _ => baseRotation = local ? target.localRotation : target.rotation;
            return Target(add, target);
        }

        public static Tweener DORotateQuaternion(this Transform target, Quaternion endValue, float duration)
            => Target(QuaternionTween(() => target.rotation, q => target.rotation = q, endValue, duration), target);

        public static Tweener DOLocalRotateQuaternion(this Transform target, Quaternion endValue, float duration)
            => Target(QuaternionTween(() => target.localRotation, q => target.localRotation = q, endValue, duration), target);

        static TweenerCore<Quaternion> QuaternionTween(DOGetter<Quaternion> get, DOSetter<Quaternion> set, Quaternion end, float duration)
            => DOTween.Register(new TweenerCore<Quaternion>(get, set, end, duration, QuaternionSlerpPlugin.Instance));

        // ── Transform: scale ──

        public static TweenerCore<Vector3> DOScale(this Transform target, Vector3 endValue, float duration)
            => Target(DOTween.To(() => target.localScale, v => target.localScale = v, endValue, duration), target);

        public static TweenerCore<Vector3> DOScale(this Transform target, float endValue, float duration)
            => target.DOScale(new Vector3(endValue, endValue, endValue), duration);

        public static TweenerCore<float> DOScaleX(this Transform target, float endValue, float duration)
            => ScaleAxis(target, endValue, duration, 0);
        public static TweenerCore<float> DOScaleY(this Transform target, float endValue, float duration)
            => ScaleAxis(target, endValue, duration, 1);
        public static TweenerCore<float> DOScaleZ(this Transform target, float endValue, float duration)
            => ScaleAxis(target, endValue, duration, 2);

        static TweenerCore<float> ScaleAxis(Transform target, float endValue, float duration, int axis)
        {
            var t = DOTween.To(
                () => { var s = target.localScale; return axis == 0 ? s.x : axis == 1 ? s.y : s.z; },
                v => { var s = target.localScale; if (axis == 0) s.x = v; else if (axis == 1) s.y = v; else s.z = v; target.localScale = s; },
                endValue, duration);
            return Target(t, target);
        }

        // ── Transform: punch / shake ──

        public static WaypointTweener DOPunchPosition(this Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f, bool snapping = false)
        {
            var t = DOTween.Punch(() => target.localPosition, v => target.localPosition = v, punch, duration, vibrato, elasticity);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static WaypointTweener DOPunchScale(this Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
            => Target(DOTween.Punch(() => target.localScale, v => target.localScale = v, punch, duration, vibrato, elasticity), target);

        public static WaypointTweener DOPunchRotation(this Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
            => Target(DOTween.Punch(() => target.localEulerAngles, v => target.localRotation = Quaternion.Euler(v), punch, duration, vibrato, elasticity), target);

        public static WaypointTweener DOShakePosition(this Transform target, float duration, float strength = 1f, int vibrato = 10,
            float randomness = 90f, bool snapping = false, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => ShakePosition(target, duration, new Vector3(strength, strength, strength), vibrato, randomness, snapping, fadeOut, randomnessMode);

        public static WaypointTweener DOShakePosition(this Transform target, float duration, Vector3 strength, int vibrato = 10,
            float randomness = 90f, bool snapping = false, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => ShakePosition(target, duration, strength, vibrato, randomness, snapping, fadeOut, randomnessMode);

        static WaypointTweener ShakePosition(Transform target, float duration, Vector3 strength, int vibrato, float randomness,
                                             bool snapping, bool fadeOut, ShakeRandomnessMode mode)
        {
            var t = DOTween.Shake(() => target.localPosition, v => target.localPosition = v, duration, strength, vibrato, randomness, fadeOut, mode, false);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static WaypointTweener DOShakeRotation(this Transform target, float duration, float strength = 90f, int vibrato = 10,
            float randomness = 90f, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => target.DOShakeRotation(duration, new Vector3(strength, strength, strength), vibrato, randomness, fadeOut, randomnessMode);

        public static WaypointTweener DOShakeRotation(this Transform target, float duration, Vector3 strength, int vibrato = 10,
            float randomness = 90f, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => Target(DOTween.Shake(() => target.localEulerAngles, v => target.localRotation = Quaternion.Euler(v),
                                    duration, strength, vibrato, randomness, fadeOut, randomnessMode, false), target);

        public static WaypointTweener DOShakeScale(this Transform target, float duration, float strength = 1f, int vibrato = 10,
            float randomness = 90f, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => target.DOShakeScale(duration, new Vector3(strength, strength, strength), vibrato, randomness, fadeOut, randomnessMode);

        public static WaypointTweener DOShakeScale(this Transform target, float duration, Vector3 strength, int vibrato = 10,
            float randomness = 90f, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => Target(DOTween.Shake(() => target.localScale, v => target.localScale = v,
                                    duration, strength, vibrato, randomness, fadeOut, randomnessMode, false), target);

        // ── Material ──

        public static TweenerCore<Color> DOColor(this Material target, Color endValue, float duration)
            => Target(DOTween.To(() => target.color, c => target.color = c, endValue, duration), target);

        public static TweenerCore<Color> DOColor(this Material target, Color endValue, string property, float duration)
            => Target(DOTween.To(() => target.GetColor(property), c => target.SetColor(property, c), endValue, duration), target);

        public static TweenerCore<Color> DOColor(this Material target, Color endValue, int propertyID, float duration)
            => Target(DOTween.To(() => target.GetColor(propertyID), c => target.SetColor(propertyID, c), endValue, duration), target);

        public static TweenerCore<float> DOFade(this Material target, float endValue, float duration)
            => Target(DOTween.AlphaTween(() => target.color, c => target.color = c, endValue, duration), target);

        public static TweenerCore<float> DOFade(this Material target, float endValue, string property, float duration)
            => Target(DOTween.AlphaTween(() => target.GetColor(property), c => target.SetColor(property, c), endValue, duration), target);

        public static TweenerCore<float> DOFade(this Material target, float endValue, int propertyID, float duration)
            => Target(DOTween.AlphaTween(() => target.GetColor(propertyID), c => target.SetColor(propertyID, c), endValue, duration), target);

        public static TweenerCore<float> DOFloat(this Material target, float endValue, string property, float duration)
            => Target(DOTween.To(() => target.GetFloat(property), v => target.SetFloat(property, v), endValue, duration), target);

        public static TweenerCore<float> DOFloat(this Material target, float endValue, int propertyID, float duration)
            => Target(DOTween.To(() => target.GetFloat(propertyID), v => target.SetFloat(propertyID, v), endValue, duration), target);

        // ── Camera / Light / Audio ──

        public static TweenerCore<float> DOFieldOfView(this Camera target, float endValue, float duration)
            => Target(DOTween.To(() => target.fieldOfView, v => target.fieldOfView = v, endValue, duration), target);

        public static TweenerCore<float> DOOrthoSize(this Camera target, float endValue, float duration)
            => Target(DOTween.To(() => target.orthographicSize, v => target.orthographicSize = v, endValue, duration), target);

        public static TweenerCore<Color> DOColor(this Camera target, Color endValue, float duration)
            => Target(DOTween.To(() => target.backgroundColor, c => target.backgroundColor = c, endValue, duration), target);

        public static TweenerCore<Color> DOColor(this Light target, Color endValue, float duration)
            => Target(DOTween.To(() => target.color, c => target.color = c, endValue, duration), target);

        public static TweenerCore<float> DOIntensity(this Light target, float endValue, float duration)
            => Target(DOTween.To(() => target.intensity, v => target.intensity = v, endValue, duration), target);

        public static TweenerCore<float> DOFade(this AudioSource target, float endValue, float duration)
        {
            if (endValue < 0f) endValue = 0f; else if (endValue > 1f) endValue = 1f;
            return Target(DOTween.To(() => target.volume, v => target.volume = v, endValue, duration), target);
        }

        // ── Target-based control on components / materials ──

        public static int DOKill(this Component target, bool complete = false) => DOTween.Kill(target, complete);
        public static int DOKill(this Material target, bool complete = false) => DOTween.Kill(target, complete);
        public static int DOComplete(this Component target, bool withCallbacks = false) => DOTween.Complete(target, withCallbacks);
        public static int DOComplete(this Material target, bool withCallbacks = false) => DOTween.Complete(target, withCallbacks);
        public static int DOPause(this Component target) => DOTween.Pause(target);
        public static int DOPause(this Material target) => DOTween.Pause(target);
        public static int DOPlay(this Component target) => DOTween.Play(target);
        public static int DOPlay(this Material target) => DOTween.Play(target);
        public static int DOPlayForward(this Component target) => DOTween.PlayForward(target);
        public static int DOPlayBackwards(this Component target) => DOTween.PlayBackwards(target);
        public static int DORestart(this Component target, bool includeDelay = true) => DOTween.Restart(target, includeDelay);
        public static int DORestart(this Material target, bool includeDelay = true) => DOTween.Restart(target, includeDelay);
        public static int DORewind(this Component target, bool includeDelay = true) => DOTween.Rewind(target, includeDelay);
        public static int DORewind(this Material target, bool includeDelay = true) => DOTween.Rewind(target, includeDelay);
        public static int DOTogglePause(this Component target) => DOTween.TogglePause(target);
        public static int DOGoto(this Component target, float to, bool andPlay = false) => DOTween.Goto(target, to, andPlay);
        public static int DOFlip(this Component target) => DOTween.Flip(target);
    }

    /// <summary>Quaternion tween: spherical interpolation from the captured start to the end rotation.</summary>
    internal sealed class QuaternionSlerpPlugin : ValuePlugin<Quaternion>
    {
        public static readonly QuaternionSlerpPlugin Instance = new();
        public override Quaternion Add(Quaternion a, Quaternion b) => a * b;
        public override Quaternion Sub(Quaternion a, Quaternion b) => Quaternion.Inverse(b) * a;
        public override Quaternion Scale(Quaternion a, float s) => Quaternion.SlerpUnclamped(Quaternion.identity, a, s);
        public override Quaternion Change(Quaternion start, Quaternion end) => end; // "change" holds the end rotation
        public override Quaternion Evaluate(Quaternion start, Quaternion change, float eased) => Quaternion.SlerpUnclamped(start, change, eased);
        public override float Distance(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);
    }
}
