using UnityEngine;

public enum ItemType
{
    None = 0,
    Buff = 1,
    Debuff = 2,
}

public class NodeItem : MonoBehaviour
{
    protected int id;
    public Teams Team = Teams.None;
    public ItemType ItemType = ItemType.Buff;

    public int GetID()
    {
        return id;
    }

    public void SetID(int id)
    {
        this.id = id;
    }

    public void AddSelfToNode()
    {
        if (NodeControlManager.Instance == null)
        {
            Debug.LogErrorFormat("{0} - {1} - {2} is not instantiated yet. Would be good to attach it somewhere?", nameof(NodeItem), nameof(AddSelfToNode), nameof(NodeControlManager));
        }
        NodeControlManager.Instance.AddItem(this);
    }

    public void RemoveSelfFromNode()
    {
        if (NodeControlManager.Instance == null)
        {
            Debug.LogErrorFormat("{0} - {1} - {2} is not instantiated yet. Would be good to attach it somewhere?", nameof(NodeItem), nameof(RemoveSelfFromNode), nameof(NodeControlManager));
        }
        NodeControlManager.Instance.RemoveItem(this);
    }

    public void UpdateSelfWithNode()
    {
        if (NodeControlManager.Instance == null)
        {
            Debug.LogErrorFormat("{0} - {1} - {2} is not instantiated yet. Would be good to attach it somewhere?", nameof(NodeItem), nameof(UpdateSelfWithNode), nameof(NodeControlManager));
        }
        NodeControlManager.Instance.UpdateItem(this);
    }
}