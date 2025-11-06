using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Game;
using UnityEngine;

public class ActionExecutorRegistry : MonoBehaviour
{
    [SerializeField] List<ShipActionExecutorBase> _executors = new();
    readonly Dictionary<Type, ShipActionExecutorBase> _byType = new();

    public IVesselStatus VesselStatus { get; private set; }   // <—

    public void InitializeAll(IVesselStatus status)
    {
        VesselStatus = status;                                // <—
        _byType.Clear();
        foreach (var exec in _executors.Where(e => e))
        {
            exec.Initialize(status);
            _byType[exec.GetType()] = exec;
        }
    }

    public T Get<T>() where T : ShipActionExecutorBase
    {
        if (_byType.TryGetValue(typeof(T), out var e)) return (T)e;
        return GetComponentInChildren<T>(true);
    }
}