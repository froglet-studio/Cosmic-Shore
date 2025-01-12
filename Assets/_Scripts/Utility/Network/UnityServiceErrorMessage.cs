using System;

namespace CosmicShore.Utilities.Network
{
    public struct UnityServiceErrorMessage
    {
        public enum Service
        {
            Authentication,
            Lobby
        }

        public string Title;
        public string Message;
        public Service AffectedService;
        public Exception OriginalException;

        public UnityServiceErrorMessage(string title, string message, Service affectedService, Exception originalException = null)
        {
            Title = title;
            Message = message;
            AffectedService = affectedService;
            OriginalException = originalException;
        }
    }   
}
