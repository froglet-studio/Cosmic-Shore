using System;
using System.Text;

namespace CosmicShore.Engine.Collections
{
    /// <summary>
    /// Fixed-capacity string for replicated state, preserving the original type's
    /// contract: holds up to 61 bytes of UTF-8 (the wire format reserves 3 bytes of
    /// the 64 for length); longer input is truncated at a code-point boundary.
    /// Implicitly converts to/from <see cref="string"/>.
    /// </summary>
    public struct FixedString64Bytes : IEquatable<FixedString64Bytes>
    {
        public const int Capacity = 61;

        string _value;

        public FixedString64Bytes(string value) { _value = Truncate(value); }

        public string Value => _value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(_value);
        public int Length => Value.Length;

        static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(s) <= Capacity) return s;

            int byteCount = 0;
            var sb = new StringBuilder();
            foreach (var rune in s.EnumerateRunes())
            {
                int runeBytes = rune.Utf8SequenceLength;
                if (byteCount + runeBytes > Capacity) break;
                byteCount += runeBytes;
                sb.Append(rune.ToString());
            }
            return sb.ToString();
        }

        public static implicit operator FixedString64Bytes(string s) => new(s);
        public static implicit operator string(FixedString64Bytes f) => f.Value;

        public bool Equals(FixedString64Bytes other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FixedString64Bytes other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(FixedString64Bytes a, FixedString64Bytes b) => a.Equals(b);
        public static bool operator !=(FixedString64Bytes a, FixedString64Bytes b) => !a.Equals(b);
    }
}
