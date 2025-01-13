using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utilities
{
    /// <summary>
    /// ScriptableObject that stores a GUID for unique identification. The population of this field is implemented
    /// inside an Editor script.
    /// NOTE - Any scriptable object class that wants to have a unique identifier same for every instance of this project (clients for multiplayer)
    /// must have atleast one member variable. This is because the OnValidate method is called only when a member variable is changed.
    /// eg: Check TagSO for reference.
    /// </summary>
    [Serializable]
    public abstract class GuidSO : ScriptableObject
    {
        [SerializeField]
        private string m_InstanceName;

        [SerializeField]
        byte[] m_Guid;

        public Guid Guid => new Guid(m_Guid);

        protected virtual void OnValidate()
        {
            m_InstanceName = name;

            if (m_Guid == null || m_Guid.Length == 0)
            {
                if (!string.IsNullOrEmpty(m_InstanceName))
                {
                    m_Guid = GenerateGuidFromName(m_InstanceName).ToByteArray();
                }
                else
                {
                    m_Guid = Guid.NewGuid().ToByteArray();
                }
            }
        }

        private static Guid GenerateGuidFromName(string name)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
                return new Guid(hash);
            }
        }
    }
}
