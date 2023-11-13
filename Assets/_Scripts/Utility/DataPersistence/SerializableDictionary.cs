using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This Class provides a way to serialize a Dictionary in Json by converting Key value pairs into Lists.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    private List<TKey> keys = new List<TKey>();
    private List<TValue> values = new List<TValue>();

    public void OnAfterDeserialize()
    {
        this.Clear();

        if(keys.Count != values.Count)
        {
            Debug.LogError("Failed to deserialize a SerializableDictionary. Keys : " + keys.Count + " Values : " 
                + values.Count + " did not match.");
        }

        for (int i = 0; i < keys.Count; i++)
        {
            this.Add(keys[i], values[i]);
        }
    }

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }
}
