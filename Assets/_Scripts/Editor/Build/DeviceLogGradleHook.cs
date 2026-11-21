using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Adds <see cref="CosmicShore.Utility.PerformanceBenchmark.DeviceLogCapture"/>'s Android half
    /// (<c>Assets/_Scripts/Utility/PerformanceBenchmark/DeviceLogAndroid~</c>) to a DEVELOPMENT Android build's Gradle
    /// project, and to nothing else. Its service and notification permission are written here at build time rather than kept
    /// in an <c>.androidlib</c>, because a library module's manifest and code reach every build, release included, and a
    /// release must not carry them. Docs/perf/DEVICE_LOGS.md.
    /// </summary>
    public sealed class DeviceLogGradleHook : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        const string SourceFolder = "_Scripts/Utility/PerformanceBenchmark/DeviceLogAndroid~";
        const string JavaPackagePath = "com/froglet/cosmicshore/devicelogs";
        const string JobClass = "com.froglet.cosmicshore.devicelogs.DeviceLogJob";
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        static bool s_development;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) =>
            s_development = report.summary.options.HasFlag(BuildOptions.Development);

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!s_development) return;

            string source = Path.Combine(Application.dataPath, SourceFolder);
            string[] files = Directory.Exists(source) ? Directory.GetFiles(source, "*.java") : Array.Empty<string>();
            if (files.Length == 0)
                throw new BuildFailedException($"[DeviceLog] no Java sources in {source}.");

            string destination = Path.Combine(path, "src", "main", "java", JavaPackagePath);
            Directory.CreateDirectory(destination);
            foreach (string file in files)
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

            AddToManifest(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
        }

        static void AddToManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            if (manifest?.SelectSingleNode("application") is not XmlElement application)
                throw new BuildFailedException($"[DeviceLog] no <application> in {manifestPath}.");

            AddPermission(doc, manifest, "android.permission.POST_NOTIFICATIONS");
            AddPermission(doc, manifest, "android.permission.ACCESS_NETWORK_STATE");
            AddPermission(doc, manifest, "android.permission.INTERNET");

            XmlElement service = doc.CreateElement("service");
            SetAndroidAttribute(doc, service, "name", JobClass);
            SetAndroidAttribute(doc, service, "permission", "android.permission.BIND_JOB_SERVICE");
            SetAndroidAttribute(doc, service, "exported", "false");
            SetAndroidAttribute(doc, service, "process", ":reporter");
            application.AppendChild(service);

            doc.Save(manifestPath);
        }

        static void AddPermission(XmlDocument doc, XmlElement manifest, string name)
        {
            foreach (XmlElement existing in manifest.SelectNodes("uses-permission"))
                if (existing.GetAttribute("name", AndroidNs) == name) return;
            XmlElement permission = doc.CreateElement("uses-permission");
            SetAndroidAttribute(doc, permission, "name", name);
            manifest.PrependChild(permission);
        }

        static void SetAndroidAttribute(XmlDocument doc, XmlElement element, string name, string value)
        {
            XmlAttribute attribute = doc.CreateAttribute("android", name, AndroidNs);
            attribute.Value = value;
            element.Attributes.Append(attribute);
        }
    }
}
