using System;
using System.Reflection;
using CosmicShore.Utility.ClassExtensions;
using CosmicShore.Utility.Singleton;
using QFSW.QC;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;

namespace CosmicShore.Multiplayer.UnityMultiplayer
{
    public class TestRelay : SingletonPersistent<TestRelay>
    {
        // Start is called before the first frame update
        private string _joinCode;
        async void Start()
        {
            await UnityServices.InitializeAsync();

            AuthenticationService.Instance.SignedIn += () =>
            {
                this.LogWithClassMethod(MethodBase.GetCurrentMethod()?.ToString(),
                    $"Signed in: {AuthenticationService.Instance.PlayerId}");
            };
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        }

        [Command]
        public async void CreateRelay()
        {
            try
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(3);
                _joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                this.LogWithClassMethod("", $"Join code: {_joinCode}");
            }
            catch (RelayServiceException e)
            {
                this.LogErrorWithClassMethod("", e.Message);
            }
        }

        [Command]
        public async void JoinRelay(string joinCode = null)
        {
            try
            {
                this.LogErrorWithClassMethod("", 
                    $"Joining relay with {joinCode}");
                
                if (!string.IsNullOrEmpty(joinCode))
                {
                    _joinCode = joinCode;
                }
                await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (RelayServiceException e)
            {
                this.LogErrorWithClassMethod("", e.Message);
            }
        }
    }
}
