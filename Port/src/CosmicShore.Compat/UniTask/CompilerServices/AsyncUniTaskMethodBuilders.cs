using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cysharp.Threading.Tasks.CompilerServices
{
    // The three async method builders. Shared shape: a method that completes without ever
    // suspending allocates nothing; the first real suspension boxes the state machine
    // (StateMachineBox) and creates the promise the returned UniTask wraps. The box copy of a
    // struct state machine carries this builder with the promise already set, so every later
    // MoveNext reports into the same promise. ExecutionContext is not flowed (upstream parity).

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AsyncUniTaskMethodBuilder
    {
        UniTaskSource _source;
        Action _moveNext;
        Exception _syncException;

        public static AsyncUniTaskMethodBuilder Create() => default;

        public UniTask Task
        {
            get
            {
                if (_source != null) return new UniTask(_source);
                if (_syncException != null) return UniTask.FromException(_syncException);
                return UniTask.CompletedTask;
            }
        }

        public void SetResult() => _source?.TrySetResult();

        public void SetException(Exception exception)
        {
            if (_source == null) _syncException = exception;
            else _source.TrySetException(exception);
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.OnCompleted(_moveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.UnsafeOnCompleted(_moveNext);
        }

        void EnsureBoxed<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_moveNext != null) return;
            var box = new StateMachineBox<TStateMachine>();
            _source = new UniTaskSource();
            _moveNext = box.MoveNextAction;
            box.StateMachine = stateMachine; // copy AFTER the fields are set: the copy's builder carries them
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AsyncUniTaskMethodBuilder<T>
    {
        UniTaskSource<T> _source;
        Action _moveNext;
        Exception _syncException;
        T _result;

        public static AsyncUniTaskMethodBuilder<T> Create() => default;

        public UniTask<T> Task
        {
            get
            {
                if (_source != null) return new UniTask<T>(_source);
                if (_syncException != null) return UniTask.FromException<T>(_syncException);
                return new UniTask<T>(_result);
            }
        }

        public void SetResult(T result)
        {
            if (_source == null) _result = result;
            else _source.TrySetResult(result);
        }

        public void SetException(Exception exception)
        {
            if (_source == null) _syncException = exception;
            else _source.TrySetException(exception);
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.OnCompleted(_moveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.UnsafeOnCompleted(_moveNext);
        }

        void EnsureBoxed<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_moveNext != null) return;
            var box = new StateMachineBox<TStateMachine>();
            _source = new UniTaskSource<T>();
            _moveNext = box.MoveNextAction;
            box.StateMachine = stateMachine;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AsyncUniTaskVoidMethodBuilder
    {
        Action _moveNext;

        public static AsyncUniTaskVoidMethodBuilder Create() => default;

        public UniTaskVoid Task => default;

        public void SetResult() { }

        /// <summary>Nobody can observe an UniTaskVoid — publish instead of storing (cancellation is ignored).</summary>
        public void SetException(Exception exception) => UniTaskScheduler.PublishUnobservedTaskException(exception);

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.OnCompleted(_moveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureBoxed(ref stateMachine);
            awaiter.UnsafeOnCompleted(_moveNext);
        }

        void EnsureBoxed<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_moveNext != null) return;
            var box = new StateMachineBox<TStateMachine>();
            _moveNext = box.MoveNextAction;
            box.StateMachine = stateMachine;
        }
    }
}
