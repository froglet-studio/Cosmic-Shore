using UnityEngine;

public class DependencySpawner : MonoBehaviour
{
    [SerializeField] GameObject[] _items;

    void Awake()
    {
        if (_items == null) return;
        foreach (var item in _items)
            Instantiate(item);
    }
}