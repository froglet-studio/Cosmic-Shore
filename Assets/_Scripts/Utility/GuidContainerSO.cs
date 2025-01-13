using System;
using UnityEngine;

namespace CosmicShore.Utilities
{
    /// <summary>
    /// An abstract scriptable object class for the container of scriptable objects with Guid
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class GuidContainerSO<T> : ScriptableObject where T : GuidSO
    {
        [SerializeField] private T[] m_DataList;
        public T[] DataList => m_DataList;

        /// <summary>
        /// Try to get the data with the given guid
        /// </summary>
        public bool TryGetData(Guid guid, out T data)
        {
            data = Array.Find(m_DataList, d => d.Guid == guid);
            return data != null;
        }

        /// <summary>
        /// Get the data with the given guid
        /// </summary>
        public T GetRandomData()
        {
            if (m_DataList == null || m_DataList.Length == 0)
            {
                return default;
            }

            return m_DataList[UnityEngine.Random.Range(0, m_DataList.Length)];
        }

        /// <summary>
        /// Log the Names and Guids of all the data in the container
        /// </summary>
        public void LogGuid()
        {
            foreach (var data in m_DataList)
            {
                Debug.Log($"{data.name} Guid: {data.Guid}");
            }
        }
    }
}

