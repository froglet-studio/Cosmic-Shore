using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>The wire codec every RPC argument and NetworkVariable value crosses the network through.</summary>
    public class NetWireTests
    {
        struct Impact : INetworkSerializable
        {
            public Vector3 Point;
            public int Domain;
            public FixedString64Bytes Name;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref Point);
                s.SerializeValue(ref Domain);
                s.SerializeValue(ref Name);
            }
        }

        struct Managed { public string Label; public int[] Values; }

        enum Team : byte { Jade = 1, Ruby = 2, Gold = 4 }

        static T RoundTrip<T>(T value) => (T)NetWire.FromBytes(typeof(T), NetWire.ToBytes(typeof(T), value));

        [Fact]
        public void Primitives_Strings_Enums_And_EngineStructs_RoundTrip()
        {
            Assert.Equal(42, RoundTrip(42));
            Assert.Equal(ulong.MaxValue, RoundTrip(ulong.MaxValue));
            Assert.Equal(1.5f, RoundTrip(1.5f));
            Assert.True(RoundTrip(true));
            Assert.Equal("Orion", RoundTrip("Orion"));
            Assert.Null(RoundTrip<string>(null));
            Assert.Equal(Team.Gold, RoundTrip(Team.Gold));
            Assert.Equal(new Vector3(1, -2, 3), RoundTrip(new Vector3(1, -2, 3)));
            Assert.Equal(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f), RoundTrip(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)));
            Assert.Equal("Nova", RoundTrip(new FixedString32Bytes("Nova")).ToString());
        }

        [Fact]
        public void Arrays_Lists_Serializables_And_ManagedStructs_RoundTrip()
        {
            Assert.Equal(new[] { 3f, 1f, 4f }, RoundTrip(new[] { 3f, 1f, 4f }));
            Assert.Equal(new List<int> { 5, 6 }, RoundTrip(new List<int> { 5, 6 }));
            var impact = RoundTrip(new Impact { Point = new Vector3(4, 5, 6), Domain = 2, Name = new FixedString64Bytes("Squirrel") });
            Assert.Equal(new Vector3(4, 5, 6), impact.Point);
            Assert.Equal(2, impact.Domain);
            Assert.Equal("Squirrel", impact.Name.ToString());
            var m = RoundTrip(new Managed { Label = "x", Values = new[] { 7, 8 } });
            Assert.Equal("x", m.Label);
            Assert.Equal(new[] { 7, 8 }, m.Values);
        }

        [Fact]
        public void NetworkVariable_OutsideASession_BehavesAsSingleProcess()
        {
            var v = new NetworkVariable<int>(1);
            int fired = 0;
            v.OnValueChanged += (a, b) => fired++;
            v.Value = 2;
            v.Value = 2;
            Assert.Equal(2, v.Value);
            Assert.Equal(1, fired);
        }
    }
}
