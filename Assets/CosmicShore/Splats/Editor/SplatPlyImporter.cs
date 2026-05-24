using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Tools.SplatImport
{
    // Header-driven binary PLY parser for 3D Gaussian Splat ply files.
    // Returns the raw (still-encoded) per-vertex values. Decoding into Unity-space
    // transforms/colors happens in SplatDecimator so the math is testable in isolation.
    public static class SplatPlyImporter
    {
        public enum PlyPropertyType
        {
            Char = 0, UChar, Short, UShort, Int, UInt, Float, Double
        }

        public struct PlyProperty
        {
            public string name;
            public PlyPropertyType type;
            public int offset;
            public int size;
        }

        public class PlyHeader
        {
            public int vertexCount;
            public List<PlyProperty> properties = new List<PlyProperty>();
            public Dictionary<string, int> propertyIndexByName = new Dictionary<string, int>();
            public int strideBytes;

            public bool TryGet(string name, out PlyProperty prop)
            {
                if (propertyIndexByName.TryGetValue(name, out int idx))
                {
                    prop = properties[idx];
                    return true;
                }
                prop = default;
                return false;
            }
        }

        public struct RawSplat
        {
            public Vector3 position;
            public Vector3 scaleRaw;   // log-encoded
            public Vector4 rotRaw;     // (w, x, y, z) raw, unnormalized
            public Vector3 fDc;        // SH DC (R, G, B)
            public float opacityRaw;   // logit
            public bool hasRotation;
            public bool hasColor;
            public bool hasOpacity;
            public bool hasScale;
        }

        public class ImportResult
        {
            public PlyHeader header;
            public RawSplat[] splats;
            public string warnings;
        }

        public static ImportResult Load(string plyPath)
        {
            if (!File.Exists(plyPath))
                throw new FileNotFoundException($"PLY file not found: {plyPath}");

            using (var fs = File.OpenRead(plyPath))
            {
                var header = ParseHeader(fs, out string format);
                if (format != "binary_little_endian")
                    throw new NotSupportedException($"Unsupported PLY format '{format}'. Only binary_little_endian is supported.");

                var splats = ReadBody(fs, header);
                return new ImportResult
                {
                    header = header,
                    splats = splats,
                };
            }
        }

        private static PlyHeader ParseHeader(Stream stream, out string format)
        {
            format = null;
            var header = new PlyHeader();
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
            {
                string line = reader.ReadLine();
                if (line == null || line.Trim() != "ply")
                    throw new InvalidDataException("Not a PLY file (missing 'ply' magic).");

                bool inVertexElement = false;
                int offset = 0;
                while (true)
                {
                    line = reader.ReadLine();
                    if (line == null) throw new InvalidDataException("Unexpected EOF in PLY header.");
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    if (line.StartsWith("format"))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 2) format = parts[1];
                        continue;
                    }
                    if (line.StartsWith("comment")) continue;
                    if (line.StartsWith("element"))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 3 && parts[1] == "vertex")
                        {
                            inVertexElement = true;
                            header.vertexCount = int.Parse(parts[2]);
                        }
                        else
                        {
                            inVertexElement = false;
                        }
                        continue;
                    }
                    if (line.StartsWith("property"))
                    {
                        if (!inVertexElement) continue; // ignore properties on other elements
                        var parts = line.Split(' ');
                        if (parts.Length < 3) throw new InvalidDataException($"Malformed property line: {line}");
                        if (parts[1] == "list")
                            throw new NotSupportedException("List properties are not supported in this importer.");

                        var type = ParseType(parts[1]);
                        int size = SizeOf(type);
                        var name = parts[2];
                        var prop = new PlyProperty { name = name, type = type, offset = offset, size = size };
                        header.propertyIndexByName[name] = header.properties.Count;
                        header.properties.Add(prop);
                        offset += size;
                        continue;
                    }
                    if (line == "end_header") break;
                }

                header.strideBytes = offset;
            }

            // StreamReader buffered ahead — find the true binary start by scanning for end_header.
            // Re-position: walk a fresh byte stream from the start to the byte after "end_header\n".
            long binaryStart = FindEndHeaderOffset(stream);
            stream.Position = binaryStart;
            return header;
        }

        private static long FindEndHeaderOffset(Stream stream)
        {
            long savedPos = stream.Position;
            stream.Position = 0;
            // Read first ~64KB; PLY headers are nearly always far smaller than this.
            int probe = (int)Math.Min(stream.Length, 1 << 16);
            var buffer = new byte[probe];
            int read = stream.Read(buffer, 0, probe);
            // Find "end_header" then advance past the following newline (\n or \r\n).
            byte[] needle = Encoding.ASCII.GetBytes("end_header");
            int idx = IndexOf(buffer, needle, 0, read);
            if (idx < 0)
                throw new InvalidDataException("Could not locate end_header in PLY header.");
            int cursor = idx + needle.Length;
            // Skip trailing whitespace/newline characters.
            while (cursor < read && (buffer[cursor] == '\r' || buffer[cursor] == '\n' || buffer[cursor] == ' ' || buffer[cursor] == '\t'))
                cursor++;
            stream.Position = savedPos;
            return cursor;
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int start, int length)
        {
            int end = Math.Min(haystack.Length, start + length) - needle.Length;
            for (int i = start; i <= end; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static PlyPropertyType ParseType(string t)
        {
            switch (t)
            {
                case "char":
                case "int8":   return PlyPropertyType.Char;
                case "uchar":
                case "uint8":  return PlyPropertyType.UChar;
                case "short":
                case "int16":  return PlyPropertyType.Short;
                case "ushort":
                case "uint16": return PlyPropertyType.UShort;
                case "int":
                case "int32":  return PlyPropertyType.Int;
                case "uint":
                case "uint32": return PlyPropertyType.UInt;
                case "float":
                case "float32": return PlyPropertyType.Float;
                case "double":
                case "float64": return PlyPropertyType.Double;
                default: throw new NotSupportedException($"Unknown PLY type '{t}'");
            }
        }

        private static int SizeOf(PlyPropertyType t)
        {
            switch (t)
            {
                case PlyPropertyType.Char: return 1;
                case PlyPropertyType.UChar: return 1;
                case PlyPropertyType.Short: return 2;
                case PlyPropertyType.UShort: return 2;
                case PlyPropertyType.Int: return 4;
                case PlyPropertyType.UInt: return 4;
                case PlyPropertyType.Float: return 4;
                case PlyPropertyType.Double: return 8;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static RawSplat[] ReadBody(Stream stream, PlyHeader header)
        {
            int n = header.vertexCount;
            int stride = header.strideBytes;
            var splats = new RawSplat[n];

            header.TryGet("x", out var px);
            header.TryGet("y", out var py);
            header.TryGet("z", out var pz);

            bool hasScale = header.TryGet("scale_0", out var s0) & header.TryGet("scale_1", out var s1) & header.TryGet("scale_2", out var s2);
            bool hasRot = header.TryGet("rot_0", out var r0) & header.TryGet("rot_1", out var r1) & header.TryGet("rot_2", out var r2) & header.TryGet("rot_3", out var r3);
            bool hasColor = header.TryGet("f_dc_0", out var fdc0) & header.TryGet("f_dc_1", out var fdc1) & header.TryGet("f_dc_2", out var fdc2);
            bool hasOpacity = header.TryGet("opacity", out var op);

            var row = new byte[stride];
            for (int i = 0; i < n; i++)
            {
                int readTotal = 0;
                while (readTotal < stride)
                {
                    int got = stream.Read(row, readTotal, stride - readTotal);
                    if (got <= 0) throw new EndOfStreamException($"PLY body ended early at vertex {i}.");
                    readTotal += got;
                }

                var sp = new RawSplat
                {
                    position = new Vector3(ReadFloat(row, px), ReadFloat(row, py), ReadFloat(row, pz)),
                    hasScale = hasScale,
                    hasRotation = hasRot,
                    hasColor = hasColor,
                    hasOpacity = hasOpacity,
                };
                if (hasScale)
                    sp.scaleRaw = new Vector3(ReadFloat(row, s0), ReadFloat(row, s1), ReadFloat(row, s2));
                if (hasRot)
                    sp.rotRaw = new Vector4(ReadFloat(row, r0), ReadFloat(row, r1), ReadFloat(row, r2), ReadFloat(row, r3));
                if (hasColor)
                    sp.fDc = new Vector3(ReadFloat(row, fdc0), ReadFloat(row, fdc1), ReadFloat(row, fdc2));
                if (hasOpacity)
                    sp.opacityRaw = ReadFloat(row, op);

                splats[i] = sp;
            }

            return splats;
        }

        private static float ReadFloat(byte[] row, PlyProperty prop)
        {
            switch (prop.type)
            {
                case PlyPropertyType.Float:
                    return BitConverter.ToSingle(row, prop.offset);
                case PlyPropertyType.Double:
                    return (float)BitConverter.ToDouble(row, prop.offset);
                case PlyPropertyType.Char:
                    return (sbyte)row[prop.offset];
                case PlyPropertyType.UChar:
                    return row[prop.offset];
                case PlyPropertyType.Short:
                    return BitConverter.ToInt16(row, prop.offset);
                case PlyPropertyType.UShort:
                    return BitConverter.ToUInt16(row, prop.offset);
                case PlyPropertyType.Int:
                    return BitConverter.ToInt32(row, prop.offset);
                case PlyPropertyType.UInt:
                    return BitConverter.ToUInt32(row, prop.offset);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
