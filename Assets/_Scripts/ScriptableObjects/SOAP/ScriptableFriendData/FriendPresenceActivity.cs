using System.Runtime.Serialization;
using UnityEngine.Scripting;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Custom activity payload for the UGS Friends presence system.
    /// Transmitted alongside the Availability enum to provide rich status info.
    /// Must use DataContract/DataMember attributes for UGS serialization.
    /// </summary>
    [Preserve]
    [DataContract]
    public class FriendPresenceActivity
    {
        [Preserve]
        [DataMember(Name = "status", IsRequired = true, EmitDefaultValue = true)]
        public string Status { get; set; }

        [Preserve]
        [DataMember(Name = "scene", IsRequired = false, EmitDefaultValue = true)]
        public string Scene { get; set; }

        [Preserve]
        [DataMember(Name = "vesselClass", IsRequired = false, EmitDefaultValue = true)]
        public string VesselClass { get; set; }

        [Preserve]
        [DataMember(Name = "partySessionId", IsRequired = false, EmitDefaultValue = true)]
        public string PartySessionId { get; set; }

        [Preserve]
        [DataMember(Name = "partyMemberCount", IsRequired = false, EmitDefaultValue = true)]
        public int PartyMemberCount { get; set; }

        [Preserve]
        [DataMember(Name = "partyMaxSlots", IsRequired = false, EmitDefaultValue = true)]
        public int PartyMaxSlots { get; set; }

        [Preserve]
        [DataMember(Name = "matchName", IsRequired = false, EmitDefaultValue = true)]
        public string MatchName { get; set; }

        public FriendPresenceActivity()
        {
            Status = "Online";
            Scene = "";
            VesselClass = "";
            PartySessionId = "";
            PartyMemberCount = 0;
            PartyMaxSlots = 0;
            MatchName = "";
        }

        public FriendPresenceActivity(
            string status,
            string scene = "",
            string vesselClass = "",
            string partySessionId = "",
            int partyMemberCount = 0,
            int partyMaxSlots = 0,
            string matchName = "")
        {
            Status = status;
            Scene = scene;
            VesselClass = vesselClass;
            PartySessionId = partySessionId;
            PartyMemberCount = partyMemberCount;
            PartyMaxSlots = partyMaxSlots;
            MatchName = matchName;
        }
    }
}
