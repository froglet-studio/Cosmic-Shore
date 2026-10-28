using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage's one line to the party (Docs/Voyages/SLICE.md §4 step 5): the host sends its course (embark, each
    /// leg, the end) as a Netcode named message, and every friend's <see cref="VoyageRun"/> follows it. A named message
    /// needs no networked object in the scene, so freestyle's session carries it as it is. Only the host's message is
    /// read; the party size is whatever the session holds (no player count is assumed here).
    /// </summary>
    public static class VoyageCourseLink
    {
        const string MessageName = "cosmicshore.voyage.course";

        static NetworkManager s_registeredOn;
        static Action<VoyageCourse> s_onReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_registeredOn = null;
            s_onReceived = null;
        }

        /// <summary>True on the machine whose voyage the party follows: the host, or a solo session with no network.</summary>
        public static bool IsLeader
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening || nm.IsServer;
            }
        }

        /// <summary>Listen for the host's course (call every so often: the session can start, end and restart).</summary>
        public static void Listen(Action<VoyageCourse> onReceived)
        {
            s_onReceived = onReceived;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null || s_registeredOn == nm) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            s_registeredOn = nm;
        }

        /// <summary>Stop listening (the toy is gone).</summary>
        public static void Stop(Action<VoyageCourse> onReceived)
        {
            if (s_onReceived == onReceived) s_onReceived = null;
            var nm = s_registeredOn;
            if (nm != null && nm.CustomMessagingManager != null)
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            s_registeredOn = null;
        }

        /// <summary>The host tells the party. Does nothing solo or off the host.</summary>
        public static void Send(VoyageCourse course)
        {
            var nm = NetworkManager.Singleton;
            if (course == null || nm == null || !nm.IsListening || !nm.IsServer || nm.CustomMessagingManager == null) return;
            if (nm.ConnectedClientsIds.Count <= 1) return; // nobody to tell
            string json = JsonUtility.ToJson(course);
            var writer = new FastBufferWriter(json.Length * 2 + 64, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(json);
                nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        static void OnMessage(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            VoyageCourse course;
            try
            {
                course = JsonUtility.FromJson<VoyageCourse>(json);
            }
            catch (ArgumentException e)
            {
                CSDebug.LogWarning($"[Voyage] The host's course would not read ({e.Message}) - not followed.");
                return;
            }
            if (course != null) s_onReceived?.Invoke(course);
        }
    }
}
