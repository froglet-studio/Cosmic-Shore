using System;
using System.Text;

namespace CosmicShore.Engine.Collections
{
    /// <summary>
    /// Fixed-capacity string for replicated state (E12-family sibling of
    /// <see cref="FixedString64Bytes"/>), preserving the original type's contract:
    /// holds up to 125 bytes of UTF-8 (the wire format reserves 3 bytes of the 128
    /// for length); longer input is truncated at a code-point boundary.
    /// Implicitly converts to/from <see cref="string"/>.
    /// </summary>
    public struct FixedString128Bytes : IEquatable<FixedString128Bytes>
    {
        public const int Capacity = 125;

        string _value;

        public FixedString128Bytes(string value) { _value = Truncate(value); }

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

        public static implicit operator FixedString128Bytes(string s) => new(s);
        public static implicit operator string(FixedString128Bytes f) => f.Value;

        public bool Equals(FixedString128Bytes other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FixedString128Bytes other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(FixedString128Bytes a, FixedString128Bytes b) => a.Equals(b);
        public static bool operator !=(FixedString128Bytes a, FixedString128Bytes b) => !a.Equals(b);
    }
}
