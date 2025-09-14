using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Game;
using UnityEngine;

public class ActionExecutorRegistry : MonoBehaviour
{
    [Tooltip("List the executors this ship actually uses (one component per action type).")]
    [SerializeField] List<ShipActionExecutorBase> _executors = new();

    readonly Dictionary<Type, ShipActionExecutorBase> _byType = new();

    public void InitializeAll(IShipStatus status)
    {
        _byType.Clear();
        foreach (var exec in _executors.Where(exec => exec))
        {
            exec.Initialize(status);
            _byType[exec.GetType()] = exec;
        }
    }

    // Get by concrete type (fast & simple)
    public T Get<T>() where T : ShipActionExecutorBase
    {
        if (_byType.TryGetValue(typeof(T), out var e)) return (T)e;
        return GetComponentInChildren<T>(true);
    }
}