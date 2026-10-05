using System;
using System.IO;
using System.IO.Compression;

namespace CosmicShore.Client
{
    /// <summary>Minimal PNG encoder (RGBA8) for headless screenshot verification.</summary>
    public static class MiniPng
    {
        public static void Write(string path, byte[] rgba, int width, int height, bool flipY = true)
        {
            using var file = File.Create(path);
            Span<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            file.Write(signature);

            // IHDR
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, width);
            WriteBE(ihdr, 4, height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // color type RGBA
            WriteChunk(file, "IHDR", ihdr);

            // IDAT: zlib stream of filter-prefixed rows
            using (var idat = new MemoryStream())
            {
                using (var z = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
                {
                    int stride = width * 4;
                    for (int y = 0; y < height; y++)
                    {
                        int row = flipY ? height - 1 - y : y;
                        z.WriteByte(0); // filter: none
                        z.Write(rgba, row * stride, stride);
                    }
                }
                WriteChunk(file, "IDAT", idat.ToArray());
            }

            WriteChunk(file, "IEND", Array.Empty<byte>());
        }

        static void WriteBE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        static void WriteChunk(Stream stream, string type, byte[] data)
        {
            Span<byte> lengthBytes = stackalloc byte[4];
            lengthBytes[0] = (byte)(data.Length >> 24);
            lengthBytes[1] = (byte)(data.Length >> 16);
            lengthBytes[2] = (byte)(data.Length >> 8);
            lengthBytes[3] = (byte)data.Length;
            stream.Write(lengthBytes);

            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);

            uint crc = Crc32(typeBytes, 0xFFFFFFFF);
            crc = Crc32(data, crc);
            crc ^= 0xFFFFFFFF;
            Span<byte> crcBytes = stackalloc byte[4];
            crcBytes[0] = (byte)(crc >> 24);
            crcBytes[1] = (byte)(crc >> 16);
            crcBytes[2] = (byte)(crc >> 8);
            crcBytes[3] = (byte)crc;
            stream.Write(crcBytes);
        }

        static readonly uint[] CrcTable = BuildCrcTable();

        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        static uint Crc32(byte[] data, uint crc)
        {
            foreach (byte b in data)
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
