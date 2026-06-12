using CosmicShore.Data;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// SOAP data model for application-level state.
    /// Written exclusively by <see cref="Core.ApplicationStateMachine"/>.
    ///
    /// Because this is a reference type, property mutations do NOT trigger
    /// <see cref="ScriptableVariable{T}.OnValueChanged"/>. State-change
    /// notifications go through the embedded <see cref="OnStateChanged"/> event.
    /// </summary>
    [System.Serializable]
    public class ApplicationStateData
    {
        public ApplicationState State { get; set; } = ApplicationState.None;
        public ApplicationState PreviousState { get; set; } = ApplicationState.None;

        /// <summary>
        /// Raised by <see cref="Core.ApplicationStateMachine"/> on every valid transition.
        /// Passes the new <see cref="ApplicationState"/>.
        /// </summary>
        public ScriptableEventApplicationState OnStateChanged;
    }
}
