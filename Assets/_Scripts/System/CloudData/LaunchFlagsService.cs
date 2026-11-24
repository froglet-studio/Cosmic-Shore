using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Fetches the live flags (<see cref="LaunchFlags"/>, Docs/LAUNCH/LIVE_FLAGS.md) from the <c>launch-flags</c> Cloud
    /// Code script once per run, when UGS sign-in completes, through the shared <see cref="UgsRequestPolicy"/> (its
    /// back-off and retry budget). A good answer is put in force and saved on the device with its time; a failure
    /// keeps whatever is in force (the fresh-enough saved answer, else the safe default), so a server that cannot be
    /// reached never stops anyone playing. Registered by <c>AppManager</c> and resolved at bootstrap so its sign-in
    /// subscription exists from the start; the browser Toybox never signs in and so never asks.
    /// </summary>
    public sealed class LaunchFlagsService
    {
        /// <summary>The Cloud Code script's name (its file name, Tools/UGS/CloudCode/launch-flags.js).</summary>
        public const string Endpoint = "launch-flags";

        readonly AuthenticationDataVariable _auth;
        readonly UgsRequestPolicy _policy;
        bool _fetched;
        bool _inFlight;

        public LaunchFlagsService(AuthenticationDataVariable auth, UgsRequestPolicy policy)
        {
            _auth = auth;
            _policy = policy ?? UgsRequestPolicy.CreateDefault();

            var data = _auth != null ? _auth.Value : null;
            if (data?.OnSignedIn == null)
            {
                CSDebug.LogWarning("[LaunchFlags] No authentication data wired; the live flags will not be fetched this run.");
                return;
            }
            data.OnSignedIn.OnRaised += HandleSignedIn;
            // A lazy singleton may be built after sign-in already happened (AnalyticsServiceFacade's lesson).
            if (data.IsSignedIn) HandleSignedIn();
        }

        void HandleSignedIn() => FetchAsync().Forget();

        async UniTaskVoid FetchAsync()
        {
            if (_fetched || _inFlight) return;
            _inFlight = true;
            string json = null;
            try
            {
                // The generic call with a JObject: the document is kept as JSON (it is what the device saves).
                var answer = await _policy.ExecuteAsync("cloudcode:" + Endpoint, () =>
                    CloudCodeService.Instance.CallEndpointAsync<JObject>(Endpoint, new Dictionary<string, object>()).AsMainThread());
                json = answer?.ToString(Formatting.None);
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[LaunchFlags] The flags could not be fetched ({e.Message}); keeping the {LaunchFlags.Source} flags.");
            }
            finally
            {
                _inFlight = false;
            }

            if (!LaunchFlagsDocument.TryParse(json, out _))
            {
                if (json != null) CSDebug.LogWarning("[LaunchFlags] The server's answer is not a flags document; keeping the flags in force.");
                return;
            }

            _fetched = true;
            long now = LaunchFlags.NowUnix();
            LaunchFlags.WriteCache(json, now);
            var (doc, source) = LaunchFlags.Resolve(json, null, 0, now, LaunchFlagsConfigSO.LoadOrDefault().CacheMaxAgeHours);
            LaunchFlags.Apply(doc, source);
            CSDebug.LogVerbose(CSLogChannel.CloudData,
                $"[LaunchFlags] In force: min {doc.MinimumVersion}, recommended {doc.RecommendedVersion}, " +
                $"killed [{string.Join(",", doc.KilledSwitches)}], disabled modes {doc.DisabledModes.Count}.");
        }
    }
}
