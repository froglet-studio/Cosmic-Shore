using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.UI;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Three engine contracts the player's first arcade launch depended on:
//   • NetworkVariable/NetworkList are serialized INLINE, so two clones of one prefab
//     never share a variable (the host Player and an AI Player cloned from the same
//     prefab were writing each other's name, vessel id and AI flag);
//   • destroying a spawned NetworkObject despawns it first (a scene unload must reach
//     OnNetworkDespawn, where the in-scene spawners unsubscribe);
//   • the serializer never leaves a serialized list/array/string/plain class null, and
//     a Graphic with no authored material reads the shared default UI material.
public class SerializerContractTests
{
    [Serializable]
    public class Payload { public int X; public List<int> Items; }

    public class NetState : NetworkBehaviour
    {
        public NetworkVariable<int> Counter = new(0);
        public NetworkList<int> History = new();
        public int Despawns;
        public override void OnNetworkDespawn() => Despawns++;
    }

    public class DefaultsHolder : MonoBehaviour
    {
        public List<string> Names;
        [SerializeField] int[] numbers;
        [SerializeField] string label;
        public Payload Payload;
        [NonSerialized] public List<int> Runtime;
        List<int> _private; // not serialized
        public int[] Numbers => numbers;
        public string Label => label;
        public List<int> Private => _private;
    }

    [Fact]
    public void ClonesOfOnePrefab_DoNotShareNetworkVariables()
    {
        using var loop = new GameLoop();
        var template = new GameObject("Template");
        template.SetActive(false);
        var state = template.AddComponent<NetState>();
        state.Counter.Value = 3;

        var a = Object.Instantiate(template).GetComponent<NetState>();
        var b = Object.Instantiate(template).GetComponent<NetState>();

        Assert.NotSame(a.Counter, b.Counter);
        Assert.NotSame(a.History, b.History);
        Assert.Equal(3, a.Counter.Value); // the value is inlined by copy

        int bChanges = 0;
        b.Counter.OnValueChanged += (_, _) => bChanges++;
        a.Counter.Value = 9;
        a.History.Add(1);
        Assert.Equal(3, b.Counter.Value);
        Assert.Empty(b.History);
        Assert.Equal(0, bChanges);
    }

    [Fact]
    public void Instantiate_KeepsTheTemplatesDisabledBehaviours()
    {
        using var loop = new GameLoop();
        var template = new GameObject("Row", typeof(RectTransform));
        template.SetActive(false);
        var image = template.AddComponent<Image>();
        image.enabled = false; // the AbilityControlRow's root Image is authored m_Enabled: 0
        var clone = Object.Instantiate(template);
        Assert.False(clone.GetComponent<Image>().enabled);
    }

    [Fact]
    public void DestroyingASpawnedNetworkObject_DespawnsItFirst()
    {
        using var loop = new GameLoop();
        var go = new GameObject("Net");
        var no = go.AddComponent<NetworkObject>();
        var state = go.AddComponent<NetState>();
        no.Spawn();
        Assert.True(state.IsSpawned);

        Object.DestroyImmediate(go);
        Assert.Equal(1, state.Despawns);
        Assert.False(state.IsSpawned);
    }

    [Fact]
    public void AddComponent_FillsNullSerializedFields_LikeTheSerializer()
    {
        using var loop = new GameLoop();
        var h = new GameObject("H").AddComponent<DefaultsHolder>();
        Assert.NotNull(h.Names);
        Assert.Empty(h.Names);
        Assert.NotNull(h.Numbers);
        Assert.Equal(string.Empty, h.Label);
        Assert.NotNull(h.Payload);
        Assert.NotNull(h.Payload.Items); // nested plain class filled recursively
        Assert.Null(h.Runtime);          // [NonSerialized] untouched
        Assert.Null(h.Private);          // private without [SerializeField] untouched
    }

    [Fact]
    public void GraphicWithoutMaterial_ReadsTheDefaultUIMaterial()
    {
        using var loop = new GameLoop();
        var go = new GameObject("Img", typeof(RectTransform));
        var image = go.AddComponent<Image>();
        Assert.NotNull(image.material);
        Assert.Same(Graphic.defaultGraphicMaterial, image.material);
        var copy = new Material(image.material); // the VolumeUI.Awake pattern
        Assert.NotNull(copy);
    }
}
