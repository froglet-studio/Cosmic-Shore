using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using CosmicShore.Editor;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pre-upload App Bundle check reads what Play reads. The two manifests below are REAL
    /// aapt2 output, not hand-written protobuf, so the parser is held to the format a Unity build
    /// actually produces.
    ///
    /// <para>Regenerate them: write the manifest XML (good: package com.FrogletGames.CosmicShore,
    /// versionCode 409000, versionName 0.2.0, min SDK 28, target 36, INTERNET + VIBRATE +
    /// ACCESS_NETWORK_STATE, application appCategory="game" debuggable="false"; bad: target 34,
    /// BILLING in place of VIBRATE, appCategory="image", debuggable="true"), then
    /// <c>aapt2 link --proto-format -I android-36/android.jar --manifest AndroidManifest.xml -o x.apk</c>
    /// (aapt2 8.13.2) and base64 the <c>AndroidManifest.xml</c> inside <c>x.apk</c>.</para>
    /// </summary>
    public class AndroidBundleCheckTests
    {
        const string Package = "com.FrogletGames.CosmicShore";

        const string GoodManifestBase64 =
            "CpESCjkKB2FuZHJvaWQSKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBoCCAIaCG1hbmlmZXN0" +
            "IicSB3BhY2thZ2UaHGNvbS5Gcm9nbGV0R2FtZXMuQ29zbWljU2hvcmUiXAoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20v" +
            "YXBrL3Jlcy9hbmRyb2lkEg9pbnN0YWxsTG9jYXRpb24aDnByZWZlckV4dGVybmFsIgIIAii3hYQIMgQ6AjACIlIKKmh0dHA6" +
            "Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILdmVyc2lvbkNvZGUaBjQwOTAwMCICCAIom4SECDIGOgQw" +
            "qPsYIkUKKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILdmVyc2lvbk5hbWUaBTAuMi4wKJyE" +
            "hAgiUgoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEhFjb21waWxlU2RrVmVyc2lvbhoCMzYi" +
            "AggCKPKKhAgyBDoCMCQiKBIYcGxhdGZvcm1CdWlsZFZlcnNpb25Db2RlGgIzNiICCAIyBDoCMCQiUAoqaHR0cDovL3NjaGVt" +
            "YXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEhljb21waWxlU2RrVmVyc2lvbkNvZGVuYW1lGgIxNijzioQIIigSGHBs" +
            "YXRmb3JtQnVpbGRWZXJzaW9uTmFtZRoCMTYiAggCMgQ6AjAQKgsSAwogIBoECAYQLSq2AQqtARoIdXNlcy1zZGsiTgoqaHR0" +
            "cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEg1taW5TZGtWZXJzaW9uGgIyOCICCAcojISECDIEOgIw" +
            "HCJRCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSEHRhcmdldFNka1ZlcnNpb24aAjM2IgII" +
            "ByjwhIQIMgQ6AjAkGgQIBxACKgsSAwogIBoECAcQRypvCmcaD3VzZXMtcGVybWlzc2lvbiJUCipodHRwOi8vc2NoZW1hcy5h" +
            "bmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBG5hbWUaG2FuZHJvaWQucGVybWlzc2lvbi5JTlRFUk5FVCiDgIQIGgQICBAC" +
            "KgsSAwogIBoECAgQQCpuCmYaD3VzZXMtcGVybWlzc2lvbiJTCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVz" +
            "L2FuZHJvaWQSBG5hbWUaGmFuZHJvaWQucGVybWlzc2lvbi5WSUJSQVRFKIOAhAgaBAgJEAIqCxIDCiAgGgQICRA/KnsKcxoP" +
            "dXNlcy1wZXJtaXNzaW9uImAKKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBIEbmFtZRonYW5k" +
            "cm9pZC5wZXJtaXNzaW9uLkFDQ0VTU19ORVRXT1JLX1NUQVRFKIOAhAgaBAgKEAIqCxIDCiAgGgQIChBMKm4KZhoMdXNlcy1m" +
            "ZWF0dXJlIlYKKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILZ2xFc1ZlcnNpb24aCjB4MDAw" +
            "MzAwMDAiAggLKIGFhAgyBjoEOICADBoECAsQAioLEgMKICAaBAgLEDMqzQcKxAcaC2FwcGxpY2F0aW9uIk4KKmh0dHA6Ly9z" +
            "Y2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILYXBwQ2F0ZWdvcnkaBGdhbWUiAggMKMWKhAgyBDoCMAAiTgoq" +
            "aHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgpkZWJ1Z2dhYmxlGgVmYWxzZSICCAwoj4CECDIE" +
            "OgJAACJVCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSEWV4dHJhY3ROYXRpdmVMaWJzGgVm" +
            "YWxzZSICCAwo6omECDIEOgJAACJJCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBmlzR2Ft" +
            "ZRoEdHJ1ZSICCAwo9IeECDIEOgJAASJGCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBWxh" +
            "YmVsGgxDb3NtaWMgU2hvcmUogYCECCoOEgUKICAgIBoFCAwQmgEqjQQKhAQaCGFjdGl2aXR5IksKKmh0dHA6Ly9zY2hlbWFz" +
            "LmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBIIZXhwb3J0ZWQaBHRydWUiAggNKJCAhAgyBDoCQAEiYwoqaHR0cDovL3Nj" +
            "aGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgRuYW1lGipjb20udW5pdHkzZC5wbGF5ZXIuVW5pdHlQbGF5ZXJH" +
            "YW1lQWN0aXZpdHkog4CECCoPEgcKICAgICAgGgQIDRBgKqUCCpwCGg1pbnRlbnQtZmlsdGVyKhESCQogICAgICAgIBoECA4Q" +
            "FSplCl0aBmFjdGlvbiJTCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBG5hbWUaGmFuZHJv" +
            "aWQuaW50ZW50LmFjdGlvbi5NQUlOKIOAhAgaBAgPEAgqERIJCiAgICAgICAgGgQIDxA8Km0KZRoIY2F0ZWdvcnkiWQoqaHR0" +
            "cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgRuYW1lGiBhbmRyb2lkLmludGVudC5jYXRlZ29yeS5M" +
            "QVVOQ0hFUiiDgIQIGgQIEBAIKg8SBwogICAgICAaBAgQEEQaBAgOEAYqDRIFCiAgICAaBAgREBYaBAgNEAQqCxIDCiAgGgQI" +
            "EhAPGgQIDBACKgkSAQoaBAgTEBAaAggC";

        const string BadManifestBase64 =
            "CpISCjkKB2FuZHJvaWQSKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBoCCAIaCG1hbmlmZXN0" +
            "IicSB3BhY2thZ2UaHGNvbS5Gcm9nbGV0R2FtZXMuQ29zbWljU2hvcmUiXAoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20v" +
            "YXBrL3Jlcy9hbmRyb2lkEg9pbnN0YWxsTG9jYXRpb24aDnByZWZlckV4dGVybmFsIgIIAii3hYQIMgQ6AjACIlIKKmh0dHA6" +
            "Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILdmVyc2lvbkNvZGUaBjQwOTAwMCICCAIom4SECDIGOgQw" +
            "qPsYIkUKKmh0dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBILdmVyc2lvbk5hbWUaBTAuMi4wKJyE" +
            "hAgiUgoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEhFjb21waWxlU2RrVmVyc2lvbhoCMzYi" +
            "AggCKPKKhAgyBDoCMCQiKBIYcGxhdGZvcm1CdWlsZFZlcnNpb25Db2RlGgIzNiICCAIyBDoCMCQiUAoqaHR0cDovL3NjaGVt" +
            "YXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEhljb21waWxlU2RrVmVyc2lvbkNvZGVuYW1lGgIxNijzioQIIigSGHBs" +
            "YXRmb3JtQnVpbGRWZXJzaW9uTmFtZRoCMTYiAggCMgQ6AjAQKgsSAwogIBoECAYQLSq2AQqtARoIdXNlcy1zZGsiTgoqaHR0" +
            "cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEg1taW5TZGtWZXJzaW9uGgIyOCICCAcojISECDIEOgIw" +
            "HCJRCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSEHRhcmdldFNka1ZlcnNpb24aAjM0IgII" +
            "ByjwhIQIMgQ6AjAiGgQIBxACKgsSAwogIBoECAcQRypvCmcaD3VzZXMtcGVybWlzc2lvbiJUCipodHRwOi8vc2NoZW1hcy5h" +
            "bmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBG5hbWUaG2FuZHJvaWQucGVybWlzc2lvbi5JTlRFUk5FVCiDgIQIGgQICBAC" +
            "KgsSAwogIBoECAgQQCpvCmcaD3VzZXMtcGVybWlzc2lvbiJUCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVz" +
            "L2FuZHJvaWQSBG5hbWUaG2NvbS5hbmRyb2lkLnZlbmRpbmcuQklMTElORyiDgIQIGgQICRACKgsSAwogIBoECAkQQCp7CnMa" +
            "D3VzZXMtcGVybWlzc2lvbiJgCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSBG5hbWUaJ2Fu" +
            "ZHJvaWQucGVybWlzc2lvbi5BQ0NFU1NfTkVUV09SS19TVEFURSiDgIQIGgQIChACKgsSAwogIBoECAoQTCpuCmYaDHVzZXMt" +
            "ZmVhdHVyZSJWCipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSC2dsRXNWZXJzaW9uGgoweDAw" +
            "MDMwMDAwIgIICyiBhYQIMgY6BDiAgAwaBAgLEAIqCxIDCiAgGgQICxAzKs0HCsQHGgthcHBsaWNhdGlvbiJPCipodHRwOi8v" +
            "c2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSC2FwcENhdGVnb3J5GgVpbWFnZSICCAwoxYqECDIEOgIwAyJN" +
            "CipodHRwOi8vc2NoZW1hcy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSCmRlYnVnZ2FibGUaBHRydWUiAggMKI+AhAgy" +
            "BDoCQAEiVQoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEhFleHRyYWN0TmF0aXZlTGlicxoF" +
            "ZmFsc2UiAggMKOqJhAgyBDoCQAAiSQoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgZpc0dh" +
            "bWUaBHRydWUiAggMKPSHhAgyBDoCQAEiRgoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgVs" +
            "YWJlbBoMQ29zbWljIFNob3JlKIGAhAgqDhIFCiAgICAaBQgMEJoBKo0ECoQEGghhY3Rpdml0eSJLCipodHRwOi8vc2NoZW1h" +
            "cy5hbmRyb2lkLmNvbS9hcGsvcmVzL2FuZHJvaWQSCGV4cG9ydGVkGgR0cnVlIgIIDSiQgIQIMgQ6AkABImMKKmh0dHA6Ly9z" +
            "Y2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBIEbmFtZRoqY29tLnVuaXR5M2QucGxheWVyLlVuaXR5UGxheWVy" +
            "R2FtZUFjdGl2aXR5KIOAhAgqDxIHCiAgICAgIBoECA0QYCqlAgqcAhoNaW50ZW50LWZpbHRlcioREgkKICAgICAgICAaBAgO" +
            "EBUqZQpdGgZhY3Rpb24iUwoqaHR0cDovL3NjaGVtYXMuYW5kcm9pZC5jb20vYXBrL3Jlcy9hbmRyb2lkEgRuYW1lGhphbmRy" +
            "b2lkLmludGVudC5hY3Rpb24uTUFJTiiDgIQIGgQIDxAIKhESCQogICAgICAgIBoECA8QPCptCmUaCGNhdGVnb3J5IlkKKmh0" +
            "dHA6Ly9zY2hlbWFzLmFuZHJvaWQuY29tL2Fway9yZXMvYW5kcm9pZBIEbmFtZRogYW5kcm9pZC5pbnRlbnQuY2F0ZWdvcnku" +
            "TEFVTkNIRVIog4CECBoECBAQCCoPEgcKICAgICAgGgQIEBBEGgQIDhAGKg0SBQogICAgGgQIERAWGgQIDRAEKgsSAwogIBoE" +
            "CBIQDxoECAwQAioJEgEKGgQIExAQGgIIAg==";

        static byte[] GoodManifest => Convert.FromBase64String(GoodManifestBase64);
        static byte[] BadManifest => Convert.FromBase64String(BadManifestBase64);

        // ── Manifest ───────────────────────────────────────────────────────────────

        [Test]
        public void ParseManifest_ReadsAapt2Output()
        {
            var m = AndroidBundleCheck.ParseManifest(GoodManifest);

            Assert.AreEqual(Package, m.Package);
            Assert.AreEqual(409000, m.VersionCode);
            Assert.AreEqual("0.2.0", m.VersionName);
            Assert.AreEqual(28, m.MinSdk);
            Assert.AreEqual(36, m.TargetSdk);
            Assert.AreEqual("game", m.AppCategory);
            Assert.IsFalse(m.Debuggable);
            CollectionAssert.AreEqual(new[]
            {
                "android.permission.INTERNET", "android.permission.VIBRATE", "android.permission.ACCESS_NETWORK_STATE"
            }, m.Permissions);
        }

        [Test]
        public void ParseManifest_ReadsEveryFieldTheChecksRejectOn()
        {
            var m = AndroidBundleCheck.ParseManifest(BadManifest);

            Assert.AreEqual(34, m.TargetSdk);
            Assert.AreEqual("image", m.AppCategory);
            Assert.IsTrue(m.Debuggable);
            CollectionAssert.Contains(m.Permissions, "com.android.vending.BILLING");
        }

        [Test]
        public void ParseManifest_RejectsBytesThatAreNotProtobuf()
        {
            // Field 1, length-delimited, claiming 127 bytes that are not there.
            Assert.Throws<FormatException>(() => AndroidBundleCheck.ParseManifest(new byte[] { 0x0A, 0x7F, 0x01 }));
        }

        // ── ELF ────────────────────────────────────────────────────────────────────

        [Test]
        public void MinLoadAlignment_IsTheSmallestLoadSegment()
        {
            Assert.AreEqual(16384, Align(Elf64(0x4000, 0x4000)));
            Assert.AreEqual(4096, Align(Elf64(0x4000, 0x1000)));
        }

        [Test]
        public void MinLoadAlignment_IsZeroForWhatItCannotRead()
        {
            Assert.AreEqual(0, Align(Encoding.ASCII.GetBytes("not an elf at all, just some text padding it out to 64+ bytes....")));
            byte[] elf32 = Elf64(0x4000);
            elf32[4] = 1; // ELFCLASS32
            Assert.AreEqual(0, Align(elf32));
            byte[] truncated = new byte[70];
            Array.Copy(Elf64(0x4000), truncated, truncated.Length);
            Assert.AreEqual(0, Align(truncated));
        }

        static long Align(byte[] elf)
        {
            using var ms = new MemoryStream(elf);
            return AndroidBundleCheck.MinLoadAlignment(ms);
        }

        /// <summary>A minimal 64-bit little-endian ELF: header, then one LOAD header per alignment.</summary>
        static byte[] Elf64(params long[] loadAligns)
        {
            const int ehsize = 64, phentsize = 56;
            var elf = new byte[ehsize + phentsize * (loadAligns.Length + 1)];
            elf[0] = 0x7F; elf[1] = (byte)'E'; elf[2] = (byte)'L'; elf[3] = (byte)'F';
            elf[4] = 2; elf[5] = 1;
            BitConverter.GetBytes((ulong)ehsize).CopyTo(elf, 0x20);
            BitConverter.GetBytes((ushort)phentsize).CopyTo(elf, 0x36);
            BitConverter.GetBytes((ushort)(loadAligns.Length + 1)).CopyTo(elf, 0x38);

            // A non-LOAD header with a tiny alignment comes first: it must be ignored.
            BitConverter.GetBytes(6u).CopyTo(elf, ehsize);
            BitConverter.GetBytes(8UL).CopyTo(elf, ehsize + 48);
            for (int i = 0; i < loadAligns.Length; i++)
            {
                int ph = ehsize + phentsize * (i + 1);
                BitConverter.GetBytes(1u).CopyTo(elf, ph);
                BitConverter.GetBytes((ulong)loadAligns[i]).CopyTo(elf, ph + 48);
            }
            return elf;
        }

        // ── Whole bundle ───────────────────────────────────────────────────────────

        [Test]
        public void Check_PassesASignedArm64BundleWith16KLibraries()
        {
            var r = CheckBundle(GoodManifest, "arm64-v8a", Elf64(0x4000), "CN=Froglet Games");
            Assert.IsTrue(r.Passed, r.Format());
            Assert.IsEmpty(r.Warnings, r.Format());
        }

        [Test]
        public void Check_FailsOnEachThingPlayRejects()
        {
            Assert.IsFalse(CheckBundle(BadManifest, "arm64-v8a", Elf64(0x4000), "CN=Froglet Games").Passed, "target 34 + debuggable");
            Assert.IsFalse(CheckBundle(GoodManifest, "arm64-v8a", Elf64(0x4000), "CN=Android Debug").Passed, "debug key");
            Assert.IsFalse(CheckBundle(GoodManifest, "arm64-v8a", Elf64(0x4000), null).Passed, "unsigned");
            Assert.IsFalse(CheckBundle(GoodManifest, "armeabi-v7a", Elf64(0x4000), "CN=Froglet Games").Passed, "no 64-bit");
            Assert.IsFalse(CheckBundle(null, "arm64-v8a", Elf64(0x4000), "CN=Froglet Games").Passed, "no manifest");

            var wrongPackage = CheckBundle(GoodManifest, "arm64-v8a", Elf64(0x4000), "CN=Froglet Games", "com.other.app");
            Assert.IsFalse(wrongPackage.Passed, "package");
        }

        [Test]
        public void Check_WarnsButPassesOnA4KLibrary()
        {
            var r = CheckBundle(GoodManifest, "arm64-v8a", Elf64(0x1000), "CN=Froglet Games");
            Assert.IsTrue(r.Passed, r.Format());
            Assert.AreEqual(1, r.Warnings.Count, r.Format());
            StringAssert.Contains("16 KB", r.Warnings[0]);
        }

        static AndroidBundleCheck.Report CheckBundle(byte[] manifest, string abi, byte[] library, string signer,
                                                      string expectedPackage = Package)
        {
            string path = Path.Combine(Path.GetTempPath(), $"cs-aab-test-{Guid.NewGuid():N}.aab");
            try
            {
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    if (manifest != null) Write(zip, "base/manifest/AndroidManifest.xml", manifest);
                    Write(zip, $"base/lib/{abi}/libmain.so", library);
                    Write(zip, "base/dex/classes.dex", new byte[256]);
                    // A signature block carries the signer's certificate, subject name in ASCII.
                    if (signer != null) Write(zip, "META-INF/UPLOAD.RSA", Encoding.ASCII.GetBytes($"0\x82..{signer}, O=Test, C=US.."));
                }
                return AndroidBundleCheck.Check(path, expectedPackage);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static void Write(ZipArchive zip, string name, byte[] data)
        {
            using var s = zip.CreateEntry(name).Open();
            s.Write(data, 0, data.Length);
        }

        // ── Version code ───────────────────────────────────────────────────────────

        [Test]
        public void VersionCodeAt_CountsMinutesSince2026AndStaysUnderPlaysLimit()
        {
            Assert.AreEqual(1, AndroidBundleCheck.VersionCodeAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(410400, AndroidBundleCheck.VersionCodeAt(new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(410401, AndroidBundleCheck.VersionCodeAt(new DateTime(2026, 10, 13, 0, 1, 30, DateTimeKind.Utc)));
            Assert.Less(AndroidBundleCheck.VersionCodeAt(new DateTime(5000, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                AndroidBundleCheck.MaxVersionCode);
        }
    }
}
