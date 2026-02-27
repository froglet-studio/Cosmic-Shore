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

        public FriendPresenceActivity()
        {
            Status = "Online";
            Scene = "";
            VesselClass = "";
            PartySessionId = "";
        }

        public FriendPresenceActivity(string status, string scene = "", string vesselClass = "", string partySessionId = "")
        {
            Status = status;
            Scene = scene;
            VesselClass = vesselClass;
            PartySessionId = partySessionId;
        }
    }
}
