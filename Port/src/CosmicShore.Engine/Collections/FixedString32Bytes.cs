using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace CosmicShore.Engine.Collections
{
    /// <summary>
    /// Fixed-capacity string for replicated state, preserving the original type's
    /// contract AND layout: a 2-byte length plus 30 bytes of inline UTF-8 storage
    /// (32 bytes total, 29 usable — the original reserves the rest for its length
    /// and terminator). Being unmanaged is load-bearing: it is what lets
    /// <c>BufferSerializer.SerializeValue&lt;T&gt;() where T : unmanaged</c> carry it
    /// in a network struct. Longer input is truncated at a code-point boundary.
    /// Implicitly converts to/from <see cref="string"/>.
    /// </summary>
    public struct FixedString32Bytes : IEquatable<FixedString32Bytes>
    {
        public const int Capacity = 29;

        [InlineArray(30)]
        struct Storage { byte _e0; }

        ushort _length;
        Storage _bytes;

        public FixedString32Bytes(string value) { this = default; Assign(value); }

        public string Value
        {
            get
            {
                if (_length == 0) return string.Empty;
                ReadOnlySpan<byte> span = _bytes;
                return Encoding.UTF8.GetString(span.Slice(0, _length));
            }
        }

        public bool IsEmpty => _length == 0;
        public int Length => Value.Length;
        public int Utf8LengthInBytes => _length;

        void Assign(string s)
        {
            _length = 0;
            if (string.IsNullOrEmpty(s)) return;
            Span<byte> dest = _bytes;
            int byteCount = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                int runeBytes = rune.Utf8SequenceLength;
                if (byteCount + runeBytes > Capacity) break;
                rune.EncodeToUtf8(dest.Slice(byteCount));
                byteCount += runeBytes;
            }
            _length = (ushort)byteCount;
        }

        public static implicit operator FixedString32Bytes(string s) => new(s);
        public static implicit operator string(FixedString32Bytes f) => f.Value;

        public bool Equals(FixedString32Bytes other)
        {
            if (_length != other._length) return false;
            ReadOnlySpan<byte> a = _bytes, b = other._bytes;
            return a.Slice(0, _length).SequenceEqual(b.Slice(0, _length));
        }
        public override bool Equals(object obj) => obj is FixedString32Bytes other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(FixedString32Bytes a, FixedString32Bytes b) => a.Equals(b);
        public static bool operator !=(FixedString32Bytes a, FixedString32Bytes b) => !a.Equals(b);
    }
}
