using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage's lines to the party (Docs/Voyages/SLICE.md §4 step 5, Docs/Voyages/PARTY.md), all Netcode named
    /// messages, so no networked object is added to the scene:
    /// <list type="bullet">
    /// <item><b>The course</b> (host to every friend, reliable): embark, each leg, the escort, the party's crystal count,
    /// the end. Every friend's <see cref="VoyageRun"/> or <see cref="ArkEscortRun"/> follows it.</item>
    /// <item><b>The Ark's pose</b> (host to every friend, unreliable, a few times a second): each friend's own Ark is kept
    /// where the host's is (<see cref="VoyageArkSync"/>).</item>
    /// <item><b>Reports</b> (a friend to the host, reliable): the seat they picked in the lobby, and each crystal and creature
    /// they took in their own world.</item>
    /// </list>
    /// Only the host's course and pose are read, and only friends' reports. The party size is whatever the session holds.
    /// </summary>
    public static class VoyageCourseLink
    {
        const string CourseMessage = "cosmicshore.voyage.course";
        const string PoseMessage = "cosmicshore.voyage.arkpose";
        const string ReportMessage = "cosmicshore.voyage.report";

        static CustomMessagingManager s_registeredOn;
        static Action<VoyageCourse> s_onReceived;

        /// <summary>The host's Ark pose arrived (a friend's machine).</summary>
        public static event Action<VoyageArkPose> ArkPoseReceived;

        /// <summary>A friend's report arrived (the host), with the friend's client id.</summary>
        public static event Action<ulong, VoyagePartyReport> ReportReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_registeredOn = null;
            s_onReceived = null;
            ArkPoseReceived = null;
            ReportReceived = null;
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

        /// <summary>True while a session with someone else in it is up and this machine hosts it.</summary>
        public static bool HostWithParty
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && nm.IsServer && nm.ConnectedClientsIds.Count > 1;
            }
        }

        /// <summary>True on a friend's machine in the host's session.</summary>
        public static bool IsFollower
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && !nm.IsServer;
            }
        }

        /// <summary>This machine's client id in the session, or -1 with none.</summary>
        public static long LocalClientId
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening ? (long)nm.LocalClientId : -1;
            }
        }

        /// <summary>The network's clock (seconds, the same on every peer), else this machine's.</summary>
        public static double ServerTimeNow
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.unscaledTimeAsDouble;
            }
        }

        /// <summary>Listen for the host's course (call every so often: the session can start, end and restart).</summary>
        public static void Listen(Action<VoyageCourse> onReceived)
        {
            s_onReceived = onReceived;
            EnsureListening();
        }

        /// <summary>
        /// Register the handlers on the live session if they are not (call every so often: a session that stops and starts
        /// again has a new messaging manager, so the check is on that, not on the NetworkManager).
        /// </summary>
        public static void EnsureListening()
        {
            var nm = NetworkManager.Singleton;
            var messaging = nm != null && nm.IsListening ? nm.CustomMessagingManager : null;
            if (messaging == null || ReferenceEquals(s_registeredOn, messaging)) return;
            messaging.RegisterNamedMessageHandler(CourseMessage, OnCourse);
            messaging.RegisterNamedMessageHandler(PoseMessage, OnPose);
            messaging.RegisterNamedMessageHandler(ReportMessage, OnReport);
            s_registeredOn = messaging;
        }

        /// <summary>Stop following the course (the harbour is gone). The pose and report handlers stay with the session.</summary>
        public static void Stop(Action<VoyageCourse> onReceived)
        {
            if (s_onReceived == onReceived) s_onReceived = null;
        }

        /// <summary>The host tells the party. Does nothing solo or off the host.</summary>
        public static void Send(VoyageCourse course)
        {
            var nm = NetworkManager.Singleton;
            if (course == null || !HostWithParty || nm.CustomMessagingManager == null) return;
            string json = JsonUtility.ToJson(course);
            var writer = new FastBufferWriter(json.Length * 2 + 64, Allocator.Temp, int.MaxValue);
            try
            {
                writer.WriteValueSafe(json);
                nm.CustomMessagingManager.SendNamedMessageToAll(CourseMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>The host sends its Ark's pose (unreliable: the next one replaces a lost one).</summary>
        public static void SendArkPose(in VoyageArkPose pose)
        {
            var nm = NetworkManager.Singleton;
            if (!HostWithParty || nm.CustomMessagingManager == null) return;
            var writer = new FastBufferWriter(VoyageArkPose.Bytes, Allocator.Temp);
            try
            {
                pose.Write(writer);
                nm.CustomMessagingManager.SendNamedMessageToAll(PoseMessage, writer, NetworkDelivery.UnreliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>A friend tells the host. Does nothing on the host or solo.</summary>
        public static void Report(VoyagePartyReport report)
        {
            var nm = NetworkManager.Singleton;
            if (report == null || !IsFollower || nm.CustomMessagingManager == null) return;
            string json = JsonUtility.ToJson(report);
            var writer = new FastBufferWriter(json.Length * 2 + 64, Allocator.Temp, int.MaxValue);
            try
            {
                writer.WriteValueSafe(json);
                nm.CustomMessagingManager.SendNamedMessage(ReportMessage, NetworkManager.ServerClientId, writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        static void OnCourse(ulong sender, FastBufferReader reader)
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

        static void OnPose(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || sender != NetworkManager.ServerClientId) return;
            if (!VoyageArkPose.TryRead(reader, out var pose)) return;
            ArkPoseReceived?.Invoke(pose);
        }

        static void OnReport(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || sender == nm.LocalClientId) return;
            reader.ReadValueSafe(out string json);
            VoyagePartyReport report;
            try
            {
                report = JsonUtility.FromJson<VoyagePartyReport>(json);
            }
            catch (ArgumentException e)
            {
                CSDebug.LogWarning($"[Voyage] A friend's report would not read ({e.Message}) - dropped.");
                return;
            }
            if (report == null) return;
            if (report.Kind == VoyagePartyReport.ReportKind.Seat) VoyagePartySeats.Store(sender, report);
            ReportReceived?.Invoke(sender, report);
        }
    }

    /// <summary>The host's Ark at one moment, on the network's clock: where it is, which way it faces, how it moves.</summary>
    public readonly struct VoyageArkPose
    {
        public const int Bytes = sizeof(int) + sizeof(double) + sizeof(float) * 11 + 8;

        /// <summary>The host's leg (a job voyage's legs count from 1; an escort is one leg).</summary>
        public readonly int LegSerial;
        /// <summary>The network's server time when the pose was taken.</summary>
        public readonly double ServerTime;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Velocity;
        /// <summary>Seconds left in the host Ark's anchor window at a Crystal Run cell (0: under way).</summary>
        public readonly float AnchorLeft;

        public VoyageArkPose(int legSerial, double serverTime, Vector3 position, Quaternion rotation, Vector3 velocity,
            float anchorLeft = 0f)
        {
            LegSerial = legSerial;
            ServerTime = serverTime;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AnchorLeft = anchorLeft;
        }

        public void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(LegSerial);
            writer.WriteValueSafe(ServerTime);
            writer.WriteValueSafe(Position);
            writer.WriteValueSafe(Rotation);
            writer.WriteValueSafe(Velocity);
            writer.WriteValueSafe(AnchorLeft);
        }

        public static bool TryRead(FastBufferReader reader, out VoyageArkPose pose)
        {
            pose = default;
            try
            {
                reader.ReadValueSafe(out int leg);
                reader.ReadValueSafe(out double time);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out Quaternion rotation);
                reader.ReadValueSafe(out Vector3 velocity);
                reader.ReadValueSafe(out float anchorLeft);
                pose = new VoyageArkPose(leg, time, position, rotation, velocity, anchorLeft);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
