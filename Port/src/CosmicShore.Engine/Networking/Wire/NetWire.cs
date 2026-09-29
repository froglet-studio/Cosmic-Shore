using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CosmicShore.Engine.Collections;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Binary value codec for everything that crosses the wire: RPC arguments, NetworkVariable
    /// values, NetworkList operations. Covers what the game sends - primitives, enums, strings,
    /// the engine's vector/colour structs, FixedString (unmanaged by layout), arrays and lists of
    /// those, <see cref="INetworkSerializable"/> structs (through a <see cref="BufferSerializer{T}"/>
    /// backed by this codec, the same contract the original's serializer gives them), object and
    /// behaviour references (by network id), and any other unmanaged struct by a raw copy
    /// (Netcode's INetworkSerializeByMemcpy path). Both peers run the same build, so the layout
    /// of an unmanaged struct is identical at both ends.
    /// </summary>
    public static class NetWire
    {
        delegate void Writer(BinaryWriter w, object value);
        delegate object Reader(BinaryReader r);

        static readonly ConcurrentDictionary<Type, (Writer write, Reader read)> s_codecs = new();

        public static void Write(BinaryWriter w, Type type, object value) => Codec(type).write(w, value);
        public static object Read(BinaryReader r, Type type) => Codec(type).read(r);

        public static byte[] ToBytes(Type type, object value)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            Write(w, type, value);
            w.Flush();
            return ms.ToArray();
        }

        public static object FromBytes(Type type, byte[] bytes)
        {
            using var r = new BinaryReader(new MemoryStream(bytes));
            return Read(r, type);
        }

        static (Writer write, Reader read) Codec(Type t) => s_codecs.GetOrAdd(t, Build);

        static (Writer, Reader) Build(Type t)
        {
            if (t == typeof(bool)) return ((w, v) => w.Write((bool)v), r => r.ReadBoolean());
            if (t == typeof(byte)) return ((w, v) => w.Write((byte)v), r => r.ReadByte());
            if (t == typeof(sbyte)) return ((w, v) => w.Write((sbyte)v), r => r.ReadSByte());
            if (t == typeof(short)) return ((w, v) => w.Write((short)v), r => r.ReadInt16());
            if (t == typeof(ushort)) return ((w, v) => w.Write((ushort)v), r => r.ReadUInt16());
            if (t == typeof(int)) return ((w, v) => w.Write((int)v), r => r.ReadInt32());
            if (t == typeof(uint)) return ((w, v) => w.Write((uint)v), r => r.ReadUInt32());
            if (t == typeof(long)) return ((w, v) => w.Write((long)v), r => r.ReadInt64());
            if (t == typeof(ulong)) return ((w, v) => w.Write((ulong)v), r => r.ReadUInt64());
            if (t == typeof(float)) return ((w, v) => w.Write((float)v), r => r.ReadSingle());
            if (t == typeof(double)) return ((w, v) => w.Write((double)v), r => r.ReadDouble());
            if (t == typeof(char)) return ((w, v) => w.Write((ushort)(char)v), r => (char)r.ReadUInt16());
            if (t == typeof(string))
                return ((w, v) =>
                {
                    if (v == null) { w.Write(-1); return; }
                    var b = Encoding.UTF8.GetBytes((string)v);
                    w.Write(b.Length); w.Write(b);
                }, r =>
                {
                    int n = r.ReadInt32();
                    return n < 0 ? null : Encoding.UTF8.GetString(r.ReadBytes(n));
                });
            if (t.IsEnum)
            {
                var under = Enum.GetUnderlyingType(t);
                var (uw, ur) = Codec(under);
                return ((w, v) => uw(w, Convert.ChangeType(v, under)), r => Enum.ToObject(t, ur(r)));
            }
            if (t == typeof(NetworkObjectReference))
                return ((w, v) => w.Write(((NetworkObjectReference)v).NetworkObjectId),
                        r => NetworkObjectReference.FromId(r.ReadUInt64()));
            if (t == typeof(NetworkBehaviourReference))
                return ((w, v) =>
                {
                    var b = (NetworkBehaviourReference)v;
                    b.TryGet(out NetworkBehaviour nb);
                    w.Write(nb != null ? nb.NetworkObjectId : 0UL);
                    w.Write(nb != null ? NetObjects.BehaviourIndex(nb) : (ushort)0);
                }, r =>
                {
                    ulong id = r.ReadUInt64();
                    ushort idx = r.ReadUInt16();
                    var nb = NetObjects.Behaviour(id, idx);
                    return nb != null ? new NetworkBehaviourReference(nb) : default;
                });
            if (t.IsArray)
            {
                var et = t.GetElementType();
                return ((w, v) =>
                {
                    if (v is not Array a) { w.Write(-1); return; }
                    w.Write(a.Length);
                    foreach (var e in a) Write(w, et, e);
                }, r =>
                {
                    int n = r.ReadInt32();
                    if (n < 0) return null;
                    var a = Array.CreateInstance(et, n);
                    for (int i = 0; i < n; i++) a.SetValue(Read(r, et), i);
                    return a;
                });
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var et = t.GetGenericArguments()[0];
                return ((w, v) =>
                {
                    if (v is not System.Collections.IList l) { w.Write(-1); return; }
                    w.Write(l.Count);
                    foreach (var e in l) Write(w, et, e);
                }, r =>
                {
                    int n = r.ReadInt32();
                    if (n < 0) return null;
                    var l = (System.Collections.IList)Activator.CreateInstance(t);
                    for (int i = 0; i < n; i++) l.Add(Read(r, et));
                    return l;
                });
            }
            if (typeof(INetworkSerializable).IsAssignableFrom(t))
                return ((w, v) =>
                {
                    bool isNull = v == null;
                    if (!t.IsValueType) { w.Write(!isNull); if (isNull) return; }
                    var s = (INetworkSerializable)v;
                    s.NetworkSerialize(new BufferSerializer<WireRW>(new WireRW(w)));
                }, r =>
                {
                    if (!t.IsValueType && !r.ReadBoolean()) return null;
                    var s = (INetworkSerializable)(t.IsValueType ? Activator.CreateInstance(t) : RuntimeHelpers.GetUninitializedObject(t));
                    s.NetworkSerialize(new BufferSerializer<WireRW>(new WireRW(r)));
                    return s;
                });
            if (t.IsValueType && IsUnmanaged(t)) // unmanaged: raw bytes
            {
                var m = typeof(NetWire).GetMethod(nameof(RawCodec), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(t);
                return ((Writer, Reader))m.Invoke(null, null);
            }
            if (t.IsValueType)
                return FieldwiseCodec(t);
            throw new NotSupportedException($"NetWire: no codec for {t.FullName}");
        }

        static bool IsUnmanaged(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t.IsPointer) return true;
            if (!t.IsValueType) return false;
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!IsUnmanaged(f.FieldType)) return false;
            return true;
        }

        static (Writer, Reader) RawCodec<T>() where T : unmanaged
            => ((w, v) => { T x = (T)v; w.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(ref x))); },
                r =>
                {
                    T x = default;
                    var span = MemoryMarshal.AsBytes(new Span<T>(ref x));
                    int got = r.Read(span);
                    if (got != span.Length) throw new EndOfStreamException();
                    return x;
                });

        /// <summary>A managed struct (holds a string or array): its public and serialized fields in order.</summary>
        static (Writer, Reader) FieldwiseCodec(Type t)
        {
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return ((w, v) => { foreach (var f in fields) Write(w, f.FieldType, f.GetValue(v)); },
                r =>
                {
                    object o = Activator.CreateInstance(t);
                    foreach (var f in fields) f.SetValue(o, Read(r, f.FieldType));
                    return o;
                });
        }

        /// <summary>The <see cref="IReaderWriter"/> an <see cref="INetworkSerializable"/> sees: raw unmanaged values.</summary>
        public readonly struct WireRW : IReaderWriter
        {
            readonly BinaryWriter _w;
            readonly BinaryReader _r;
            public WireRW(BinaryWriter w) { _w = w; _r = null; }
            public WireRW(BinaryReader r) { _w = null; _r = r; }
            public bool IsReader => _r != null;
            public bool IsWriter => _w != null;

            public void SerializeValue<T>(ref T value) where T : unmanaged
            {
                if (_w != null) _w.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(ref value)));
                else
                {
                    var span = MemoryMarshal.AsBytes(new Span<T>(ref value));
                    if (_r.Read(span) != span.Length) throw new EndOfStreamException();
                }
            }
        }
    }
}
