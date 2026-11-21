using System;
using System.IO;
using System.Text;

namespace CosmicShore.Engine
{
    /// <summary>
    /// A text or binary file imported as an asset (UnityEngine.TextAsset): .txt, .json, .bytes, .csv,
    /// .xml, .yaml, .html, .htm and .fnt. <see cref="bytes"/> is the file unchanged; <see cref="text"/>
    /// decodes it, honouring a UTF-8 or UTF-16 byte-order mark and defaulting to UTF-8, as Unity does.
    /// Content loads one wherever a field or Resources.Load asks for a TextAsset.
    /// </summary>
    public class TextAsset : Object
    {
        readonly byte[] _bytes;
        string _text;

        public TextAsset() : this(string.Empty) { }

        /// <summary>A text asset made in code (Unity 2020.1+).</summary>
        public TextAsset(string text)
        {
            _text = text ?? string.Empty;
            _bytes = Encoding.UTF8.GetBytes(_text);
        }

        /// <summary>The port's import path: the file's bytes, exactly as on disk.</summary>
        public TextAsset(byte[] fileBytes) { _bytes = fileBytes ?? Array.Empty<byte>(); }

        /// <summary>The file names the asset the way Unity does: the file name without its extension.</summary>
        public static TextAsset FromFile(string path) => new(File.ReadAllBytes(path)) { name = Path.GetFileNameWithoutExtension(path) };

        public string text => _text ??= Decode(_bytes);
        public byte[] bytes => (byte[])_bytes.Clone();
        public long dataSize => _bytes.LongLength;

        public override string ToString() => text;

        /// <summary>Whether Unity imports a file with this extension as a TextAsset.</summary>
        public static bool IsTextAssetExtension(string extension) => extension?.ToLowerInvariant() is
            ".txt" or ".json" or ".bytes" or ".csv" or ".xml" or ".yaml" or ".html" or ".htm" or ".fnt";

        static string Decode(byte[] data)
        {
            using var reader = new StreamReader(new MemoryStream(data), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }
}
