using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// Payload carried on the <c>Event_BootStatusRequest</c> SOAP channel.
    /// Any system that wants to drive the boot/loading status surface raises
    /// this; <c>BootStatusPanel</c> is the sole subscriber.
    /// </summary>
    [Serializable]
    public struct BootStatusRequest
    {
        public BootStatusMode Mode;
        public string Text;

        public BootStatusRequest(BootStatusMode mode, string text = null)
        {
            Mode = mode;
            Text = text;
        }
    }
}
