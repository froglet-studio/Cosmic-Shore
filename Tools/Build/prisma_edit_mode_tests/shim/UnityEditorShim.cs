// Scratch stand-ins for the two UnityEditor APIs the multiplayer tests use, backed by the port's
// real asset reader (ContentRuntime) and by reflection on serialized fields - so a test that reads a
// shipped .asset reads the shipped YAML, not a fixture.
using System;
using System.IO;
using System.Reflection;
using CosmicShore.Content;
using NUnit.Framework;

namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : CosmicShore.Engine.Object
        {
            var rt = ContentRuntime.Current;
            if (rt == null) return null;
            var guid = rt.Db.GuidOf(assetPath);
            if (guid == null) return null;
            var f = rt.Db.Load(guid);
            if (f == null) return null;
            long fileId = 0;
            foreach (var d in f.Documents) if (d.FileId == 11400000 || d.FileId == 2100000) { fileId = d.FileId; break; }
            if (fileId == 0) foreach (var d in f.Documents) if (!d.Stripped) { fileId = d.FileId; break; }
            return fileId == 0 ? null : rt.Assets.Load(new ObjRef(fileId, guid, 2), typeof(T)) as T;
        }
    }

    public class SerializedObject
    {
        readonly object _target;
        public SerializedObject(CosmicShore.Engine.Object target) { _target = target; }
        public SerializedProperty FindProperty(string name)
        {
            for (var t = _target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return new SerializedProperty(_target, f);
            }
            return null;
        }
        public bool ApplyModifiedPropertiesWithoutUndo() => true; // writes are immediate
        public bool ApplyModifiedProperties() => true;
        public void Update() { }
    }

    public class SerializedProperty
    {
        readonly object _o; readonly FieldInfo _f;
        internal SerializedProperty(object o, FieldInfo f) { _o = o; _f = f; }
        public int intValue { get => Convert.ToInt32(_f.GetValue(_o)); set => _f.SetValue(_o, _f.FieldType.IsEnum ? Enum.ToObject(_f.FieldType, value) : Convert.ChangeType(value, _f.FieldType)); }
        public float floatValue { get => Convert.ToSingle(_f.GetValue(_o)); set => _f.SetValue(_o, Convert.ChangeType(value, _f.FieldType)); }
        public bool boolValue { get => (bool)_f.GetValue(_o); set => _f.SetValue(_o, value); }
        public string stringValue { get => (string)_f.GetValue(_o); set => _f.SetValue(_o, value); }
        public CosmicShore.Engine.Object objectReferenceValue { get => _f.GetValue(_o) as CosmicShore.Engine.Object; set => _f.SetValue(_o, value); }
        public int enumValueIndex { get => Convert.ToInt32(_f.GetValue(_o)); set => _f.SetValue(_o, Enum.ToObject(_f.FieldType, value)); }
    }
}

// Global namespace: NUnit applies a SetUpFixture only to tests in its own namespace.
    /// <summary>Installs the port's content runtime once, as Unity's editor has its asset database.</summary>
    [SetUpFixture]
    public sealed class ContentBoot
    {
        [OneTimeSetUp]
        public void Boot()
        {
            var root = System.Environment.GetEnvironmentVariable("PORTTESTS_PROJECT_ROOT") ?? CosmicShore.Content.AssetDatabase.FindProjectRoot();
            Assert.IsNotNull(root, "Unity project root not found");
            // Unity's editor: Application.dataPath is the project's Assets folder.
            CosmicShore.Engine.Application.dataPath = System.IO.Path.Combine(root, "Assets");
            // ...and the working directory is the project root (tests read "Assets/..." relative to it).
            System.IO.Directory.SetCurrentDirectory(root);
            new ContentRuntime(root, new[] { typeof(CosmicShore.Engine.GameObject).Assembly,
                typeof(CosmicShore.Utility.UgsRequestPolicy).Assembly }).Install();
            // An edit-mode run has a live editor loop: objects can be created at any time.
            _loop = new CosmicShore.Engine.GameLoop("PortTests");
        }

        CosmicShore.Engine.GameLoop _loop;

        [OneTimeTearDown]
        public void Shutdown() => _loop?.Dispose();
    }

