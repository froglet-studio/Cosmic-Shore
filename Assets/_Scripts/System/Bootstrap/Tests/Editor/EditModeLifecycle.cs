#if UNITY_EDITOR
using System;
using System.Reflection;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Helpers for Edit-Mode tests that need to drive Unity lifecycle code.
    ///
    /// Unity does NOT call <c>Awake</c>/<c>OnEnable</c>/<c>Start</c> outside Play Mode for a
    /// MonoBehaviour that isn't <c>[ExecuteAlways]</c>. Neither <c>AddComponent</c> nor a
    /// <c>SetActive(false) → SetActive(true)</c> round trip changes that. A test that assumes
    /// otherwise reads whatever ambient state the editor happens to be in, which is how these
    /// suites ended up with failures AND with a false green
    /// (<c>ConfigurePlatform_WithConfig_AppliesTargetFrameRate</c> passed only because this
    /// machine's editor <c>targetFrameRate</c> happened to match the asserted value).
    ///
    /// The green suites in this folder already work this way — see
    /// <c>ApplicationLifecycleManagerTests</c>, which invokes every private method by reflection.
    /// These helpers put that in one place so the reason is stated once.
    ///
    /// Invoking <c>Awake</c> is only safe when the body is side-effect-free outside Play Mode.
    /// <see cref="SceneTransitionManager"/> qualifies (it builds child GameObjects).
    /// <see cref="AppManager"/> does NOT: its <c>Awake</c> runs <c>TryResolveManagersEarly</c>,
    /// which sweeps the open scene with <c>FindAnyObjectByType</c> and adds a
    /// <c>DontDestroyOnLoad</c> component to anything it finds — dirtying whatever scene the
    /// developer has open. Call the specific private method instead.
    /// </summary>
    static class EditModeLifecycle
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>
        /// Invokes the component's private <c>Awake</c>, which Unity never calls in Edit Mode.
        /// Only use on components whose Awake is safe outside Play Mode.
        /// </summary>
        public static void InvokeAwake(Component component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            InvokePrivate(component, "Awake");
        }

        /// <summary>
        /// Invokes a private instance method by name and returns its result.
        /// Throws if the method no longer exists, so a rename fails the test loudly rather than
        /// silently degrading it into a no-op (the failure mode that made three tests in
        /// <c>AppManagerBootstrapTests</c> assert against fields that had been deleted).
        /// </summary>
        public static object InvokePrivate(object target, string methodName, params object[] args)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var method = target.GetType().GetMethod(methodName, Instance);
            if (method == null)
                throw new MissingMethodException(target.GetType().Name, methodName);

            return method.Invoke(target, args);
        }

        /// <summary>
        /// Writes a private instance field by name. Throws if it no longer exists.
        /// </summary>
        public static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var field = target.GetType().GetField(fieldName, Instance);
            if (field == null)
                throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }

        /// <summary>
        /// Captures the editor's main thread into <see cref="MainThreadDispatcher"/>.
        ///
        /// Its own <c>Init</c> is <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>, which
        /// never runs outside Play Mode, so <c>_mainThreadId</c> stays 0 and
        /// <c>IsOnMainThread</c> reports FALSE for every Edit-Mode test. Any code that guards on
        /// it — <see cref="SceneTransitionManager.SetFadeImmediate"/> does — then bails with a
        /// <c>Debug.LogError</c>, which the Test Framework counts as a failure on top of whatever
        /// the assert would have said.
        ///
        /// Idempotent, and harmless in Play Mode: it captures the same thread Unity's own Init
        /// would, and entering Play Mode re-runs that Init anyway.
        /// </summary>
        public static void EnsureMainThreadDispatcherInitialized()
        {
            if (MainThreadDispatcher.IsOnMainThread)
                return;

            var init = typeof(MainThreadDispatcher).GetMethod("Init", Static);
            if (init == null)
                throw new MissingMethodException(nameof(MainThreadDispatcher), "Init");

            init.Invoke(null, null);
        }
    }
}
#endif
