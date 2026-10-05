using System;

namespace Cysharp.Threading.Tasks
{
    /// <summary>
    /// Original contract: the sink for exceptions nothing can observe — faults escaping an
    /// <c>async UniTaskVoid</c> method or a <c>Forget()</c>-ed task. With no subscriber they are
    /// logged through the engine's <c>Debug.LogException</c>; they are never swallowed silently.
    /// <see cref="OperationCanceledException"/> is dropped unless
    /// <see cref="PropagateOperationCanceledException"/> is set (upstream default: false).
    /// </summary>
    public static class UniTaskScheduler
    {
        public static event Action<Exception> UnobservedTaskException;

        public static bool PropagateOperationCanceledException = false;

        internal static void PublishUnobservedTaskException(Exception exception)
        {
            if (exception == null) return;
            if (exception is AggregateException { InnerExceptions.Count: 1 } agg) exception = agg.InnerExceptions[0];
            if (exception is OperationCanceledException && !PropagateOperationCanceledException) return;

            var handler = UnobservedTaskException;
            if (handler != null) handler(exception);
            else CosmicShore.Engine.Debug.LogException(exception);
        }
    }
}
