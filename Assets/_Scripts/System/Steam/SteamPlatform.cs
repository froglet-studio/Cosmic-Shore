// The platforms the Steam client runs on. Steamworks.NET also compiles for Android, which has no Steam client.
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
#define COSMIC_STEAM
#endif
using System;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
#if COSMIC_STEAM
using Steamworks;
#endif

namespace CosmicShore.Core
{
    /// <summary>
    /// The game's one owner of the Steam API (Steamworks.NET): starts it, pumps its callbacks, issues the Web API
    /// ticket UGS signs in with, and shuts it down on quit. Fair play, Threat Board T1.
    ///
    /// <para>
    /// On every platform without a Steam client (phones, consoles, the Amoebius port) this compiles to a shell that
    /// always answers "not running", so callers never need their own platform guard.
    /// </para>
    /// </summary>
    public static class SteamPlatform
    {
        /// <summary>True once the Steam API started and until it shuts down.</summary>
        public static bool IsRunning { get; private set; }

#if COSMIC_STEAM
        static bool s_startFailed;
        static Callback<GetTicketForWebApiResponse_t> s_ticketCallback;
        static TaskCompletionSource<string> s_pendingTicket;
        static HAuthTicket s_pendingHandle;

        // Domain reload is disabled in this project, so statics survive play-mode exit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_startFailed = false;
            s_pendingTicket = null;
        }
#endif

        /// <summary>
        /// Starts the Steam API once. False when Steam is switched off (<see cref="SteamConfigSO.AppId"/> 0), the
        /// platform has no Steam client, or the client is not running - the caller signs in without Steam.
        /// </summary>
        public static bool TryStart(SteamConfigSO config)
        {
#if COSMIC_STEAM
            if (IsRunning) return true;
            if (s_startFailed || !config || config.AppId == 0) return false;

            // Launched by Steam, the client sets this. Launched from the Editor or a local build, it tells the API
            // which app to start as (the documented alternative to a steam_appid.txt beside the executable).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamAppId")))
                Environment.SetEnvironmentVariable("SteamAppId", config.AppId.ToString());

            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    s_startFailed = true;
                    CSDebug.LogWarning("[Steam] The Steamworks native library does not match this build; signing in without Steam.");
                    return false;
                }

                var result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    s_startFailed = true;
                    CSDebug.LogWarning($"[Steam] Steam is not available ({result}: {error}); signing in without Steam.");
                    return false;
                }
            }
            catch (DllNotFoundException e)
            {
                s_startFailed = true;
                CSDebug.LogWarning($"[Steam] The Steamworks native library is missing ({e.Message}); signing in without Steam.");
                return false;
            }

            IsRunning = true;
            s_ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicket);
            SteamCallbackPump.Create();
            Application.quitting += Shutdown;
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// A Steam Web API session ticket for <paramref name="identity"/>, hex-encoded the way UGS expects it, or
        /// null when Steam is not running, refuses, or does not answer within <paramref name="timeoutSeconds"/>.
        /// </summary>
        public static Task<string> GetWebApiTicketAsync(string identity, float timeoutSeconds)
        {
#if COSMIC_STEAM
            return IsRunning ? RequestTicketAsync(identity, timeoutSeconds) : Task.FromResult<string>(null);
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static void Shutdown()
        {
#if COSMIC_STEAM
            Application.quitting -= Shutdown;
            if (!IsRunning) return;

            IsRunning = false;
            s_pendingTicket?.TrySetResult(null);
            s_pendingTicket = null;
            s_ticketCallback?.Dispose();
            s_ticketCallback = null;
            SteamAPI.Shutdown();
#endif
        }

#if COSMIC_STEAM
        static async Task<string> RequestTicketAsync(string identity, float timeoutSeconds)
        {
            s_pendingTicket?.TrySetResult(null); // one ticket request at a time; an older one is abandoned
            var pending = new TaskCompletionSource<string>();
            s_pendingTicket = pending;
            s_pendingHandle = SteamUser.GetAuthTicketForWebApi(identity);
            if (s_pendingHandle == HAuthTicket.Invalid)
            {
                s_pendingTicket = null;
                return null;
            }

            var finished = await Task.WhenAny(pending.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            if (finished != pending.Task)
                CSDebug.LogWarning("[Steam] No Web API ticket within the timeout; signing in without Steam.");
            pending.TrySetResult(null);
            if (s_pendingTicket == pending) s_pendingTicket = null;
            return await pending.Task;
        }

        // Runs on the main thread, inside SteamAPI.RunCallbacks.
        static void OnTicket(GetTicketForWebApiResponse_t response)
        {
            var pending = s_pendingTicket;
            if (pending == null || response.m_hAuthTicket != s_pendingHandle)
                return;

            s_pendingTicket = null;
            if (response.m_eResult != EResult.k_EResultOK || response.m_cubTicket <= 0)
            {
                CSDebug.LogWarning($"[Steam] Steam refused a Web API ticket ({response.m_eResult}); signing in without Steam.");
                pending.TrySetResult(null);
                return;
            }

            pending.TrySetResult(BitConverter.ToString(response.m_rgubTicket, 0, response.m_cubTicket).Replace("-", string.Empty));
        }
#endif
    }
}
