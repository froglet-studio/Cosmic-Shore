using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tools.SplatImport.Tests
{
    // Builds a tiny in-memory binary PLY (3DGS-shaped) and round-trips it
    // through SplatPlyImporter, so the header parse + binary read are
    // verifiable without depending on an external .ply on disk.
    [TestFixture]
    public class SplatPlyImporterTests
    {
        [Test]
        public void Load_ParsesHeaderAndBody()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"splat_test_{Guid.NewGuid():N}.ply");
            try
            {
                WriteSyntheticPly(tempPath, vertexCount: 3);
                var result = SplatPlyImporter.Load(tempPath);
                Assert.IsNotNull(result);
                Assert.AreEqual(3, result.header.vertexCount);
                Assert.AreEqual(3, result.splats.Length);

                Assert.IsTrue(result.splats[0].hasScale);
                Assert.IsTrue(result.splats[0].hasRotation);
                Assert.IsTrue(result.splats[0].hasColor);
                Assert.IsTrue(result.splats[0].hasOpacity);

                // Vertex 0 was written with position (1, 2, 3).
                Assert.AreEqual(1f, result.splats[0].position.x, 1e-5f);
                Assert.AreEqual(2f, result.splats[0].position.y, 1e-5f);
                Assert.AreEqual(3f, result.splats[0].position.z, 1e-5f);

                // Vertex 1 has scale_raw = (ln 0.5, ln 0.1, ln 0.02).
                Assert.AreEqual(Mathf.Log(0.5f), result.splats[1].scaleRaw.x, 1e-4f);
                Assert.AreEqual(Mathf.Log(0.1f), result.splats[1].scaleRaw.y, 1e-4f);
                Assert.AreEqual(Mathf.Log(0.02f), result.splats[1].scaleRaw.z, 1e-4f);

                // Vertex 2 carries a known quaternion in (w, x, y, z) order.
                Assert.AreEqual(0.5f, result.splats[2].rotRaw.x, 1e-5f); // w
                Assert.AreEqual(0.1f, result.splats[2].rotRaw.y, 1e-5f); // x
                Assert.AreEqual(0.2f, result.splats[2].rotRaw.z, 1e-5f); // y
                Assert.AreEqual(0.3f, result.splats[2].rotRaw.w, 1e-5f); // z
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Test]
        public void Load_ThrowsOnMissingFile()
        {
            Assert.Throws<FileNotFoundException>(() => SplatPlyImporter.Load("/this/path/does/not/exist.ply"));
        }

        [Test]
        public void Load_ThrowsOnAsciiFormat()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"splat_ascii_{Guid.NewGuid():N}.ply");
            try
            {
                File.WriteAllText(tempPath,
                    "ply\n" +
                    "format ascii 1.0\n" +
                    "element vertex 1\n" +
                    "property float x\n" +
                    "property float y\n" +
                    "property float z\n" +
                    "end_header\n" +
                    "0 0 0\n");
                Assert.Throws<NotSupportedException>(() => SplatPlyImporter.Load(tempPath));
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Test]
        public void Load_HandlesPositionOnlyPly()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"splat_posonly_{Guid.NewGuid():N}.ply");
            try
            {
                using (var fs = File.Create(tempPath))
                {
                    var hdr =
                        "ply\n" +
                        "format binary_little_endian 1.0\n" +
                        "element vertex 2\n" +
                        "property float x\n" +
                        "property float y\n" +
                        "property float z\n" +
                        "end_header\n";
                    var hdrBytes = Encoding.ASCII.GetBytes(hdr);
                    fs.Write(hdrBytes, 0, hdrBytes.Length);
                    using (var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true))
                    {
                        bw.Write(1f); bw.Write(2f); bw.Write(3f);
                        bw.Write(4f); bw.Write(5f); bw.Write(6f);
                    }
                }

                var result = SplatPlyImporter.Load(tempPath);
                Assert.AreEqual(2, result.splats.Length);
                Assert.IsFalse(result.splats[0].hasScale);
                Assert.IsFalse(result.splats[0].hasColor);
                Assert.IsFalse(result.splats[0].hasOpacity);
                Assert.IsFalse(result.splats[0].hasRotation);
                Assert.AreEqual(new Vector3(1, 2, 3), result.splats[0].position);
                Assert.AreEqual(new Vector3(4, 5, 6), result.splats[1].position);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void WriteSyntheticPly(string path, int vertexCount)
        {
            using (var fs = File.Create(path))
            {
                var hdr = new StringBuilder();
                hdr.Append("ply\n");
                hdr.Append("format binary_little_endian 1.0\n");
                hdr.Append("comment synthetic splat test\n");
                hdr.Append($"element vertex {vertexCount}\n");
                hdr.Append("property float x\n");
                hdr.Append("property float y\n");
                hdr.Append("property float z\n");
                hdr.Append("property float scale_0\n");
                hdr.Append("property float scale_1\n");
                hdr.Append("property float scale_2\n");
                hdr.Append("property float rot_0\n");
                hdr.Append("property float rot_1\n");
                hdr.Append("property float rot_2\n");
                hdr.Append("property float rot_3\n");
                hdr.Append("property float f_dc_0\n");
                hdr.Append("property float f_dc_1\n");
                hdr.Append("property float f_dc_2\n");
                hdr.Append("property float opacity\n");
                hdr.Append("end_header\n");
                var hdrBytes = Encoding.ASCII.GetBytes(hdr.ToString());
                fs.Write(hdrBytes, 0, hdrBytes.Length);

                using (var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true))
                {
                    // Vertex 0: pos = (1, 2, 3), all other fields zero.
                    bw.Write(1f); bw.Write(2f); bw.Write(3f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f);

                    // Vertex 1: scale_raw = (ln 0.5, ln 0.1, ln 0.02).
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(Mathf.Log(0.5f)); bw.Write(Mathf.Log(0.1f)); bw.Write(Mathf.Log(0.02f));
                    bw.Write(1f); bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f);

                    // Vertex 2: rot stored (w, x, y, z) = (0.5, 0.1, 0.2, 0.3).
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0.5f); bw.Write(0.1f); bw.Write(0.2f); bw.Write(0.3f);
                    bw.Write(0f); bw.Write(0f); bw.Write(0f);
                    bw.Write(0f);
                }
            }
        }
    }
}
