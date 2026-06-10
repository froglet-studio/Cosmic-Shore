using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Shared-state container — the port's replacement for the SOAP ScriptableVariable.
    /// Single-writer / multi-reader contract is preserved: writers set <see cref="Value"/>,
    /// readers subscribe to <see cref="OnValueChanged"/> or poll. Listeners are invoked
    /// inline on the caller's thread (same contract as the original — keep raises on the
    /// main thread).
    /// </summary>
    public abstract class ScriptableVariableBase : ScriptableObject { }

    public class ScriptableVariable<T> : ScriptableVariableBase
    {
        T _value;
        T _initialValue;
        bool _initialCaptured;

        public event Action<T> OnValueChanged;

        public T PreviousValue { get; private set; }

        public virtual T Value
        {
            get => _value;
            set
            {
                if (!_initialCaptured)
                {
                    _initialValue = _value;
                    _initialCaptured = true;
                }
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                PreviousValue = _value;
                _value = value;
                OnValueChanged?.Invoke(value);
            }
        }

        /// <summary>Set the starting value without firing change events (asset initialization).</summary>
        public void SetInitialValue(T value)
        {
            _value = value;
            _initialValue = value;
            _initialCaptured = true;
            PreviousValue = value;
        }

        public void ResetToInitialValue() => Value = _initialValue;

        /// <summary>Force-raise the change event with the current value (re-sync late subscribers).</summary>
        public void ForceNotify() => OnValueChanged?.Invoke(_value);

        public override string ToString() => $"{name}: {_value}";
    }

    // Common concrete variable types, mirroring the original asset menu set.
    public class BoolVariable : ScriptableVariable<bool> { }
    public class IntVariable : ScriptableVariable<int> { }
    public class FloatVariable : ScriptableVariable<float> { }
    public class StringVariable : ScriptableVariable<string> { }
    public class Vector2Variable : ScriptableVariable<Vector2> { }
    public class Vector3Variable : ScriptableVariable<Vector3> { }
    public class QuaternionVariable : ScriptableVariable<Quaternion> { }
    public class ColorVariable : ScriptableVariable<Color> { }
}
