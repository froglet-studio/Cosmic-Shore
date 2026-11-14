using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// Payload carried on the <c>Event_BootStatusRequest</c> SOAP channel.
    /// Any system that wants to drive the boot/loading status surface raises
    /// this; <c>BootStatusPanel</c> owns the retry surface, and the splash bar
    /// (<c>BootStepBar</c>) reads <see cref="Step"/> for its percentage.
    /// </summary>
    [Serializable]
    public struct BootStatusRequest
    {
        public BootStatusMode Mode;
        public string Text;

        /// <summary>The boot milestone this status starts, or <see cref="BootStep.None"/> for a status that is none.</summary>
        public BootStep Step;

        public BootStatusRequest(BootStatusMode mode, string text = null, BootStep step = BootStep.None)
        {
            Mode = mode;
            Text = text;
            Step = step;
        }
    }
}
