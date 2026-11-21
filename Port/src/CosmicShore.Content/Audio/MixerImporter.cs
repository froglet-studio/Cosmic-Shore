using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine.Audio;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Content.Audio
{
    /// <summary>
    /// Imports a <c>.mixer</c> asset: the AudioMixerController (class 241) becomes an
    /// <see cref="AudioMixer"/> whose exposed parameters carry their start-snapshot values,
    /// each AudioMixerGroupController (243) an <see cref="AudioMixerGroup"/> with its path
    /// from the master group, each snapshot (245) an <see cref="AudioMixerSnapshot"/>. A
    /// reference to any of them resolves to the shared imported object.
    /// </summary>
    public sealed class MixerImporter
    {
        const int MixerClass = 241, GroupClass = 243, SnapshotClass = 245;
        readonly AssetDatabase _db;
        readonly Dictionary<string, Dictionary<long, EngineObject>> _byGuid = new(StringComparer.Ordinal);

        public MixerImporter(AssetDatabase db) { _db = db; }

        public static void Register(AssetLoader assets, MixerImporter importer)
        {
            assets.Importers[typeof(AudioMixer)] = importer.Load;
            assets.Importers[typeof(AudioMixerGroup)] = importer.Load;
            assets.Importers[typeof(AudioMixerSnapshot)] = importer.Load;
        }

        public EngineObject Load(ObjRef r)
        {
            var path = _db.PathOf(r.Guid);
            if (path == null || !path.EndsWith(".mixer", StringComparison.OrdinalIgnoreCase)) return null;
            if (!_byGuid.TryGetValue(r.Guid, out var objects))
                _byGuid[r.Guid] = objects = Import(_db.Load(r.Guid));
            return objects.TryGetValue(r.FileId, out var o) ? o : null;
        }

        static Dictionary<long, EngineObject> Import(AssetFile file)
        {
            var result = new Dictionary<long, EngineObject>();
            if (file == null) return result;

            UnityDocument mixerDoc = null;
            foreach (var d in file.Documents) if (d.ClassId == MixerClass) { mixerDoc = d; break; }
            if (mixerDoc == null) return result;

            var mixer = new AudioMixer { name = mixerDoc.Body.Str("m_Name") };
            result[mixerDoc.FileId] = mixer;

            // Start snapshot float values keyed by parameter guid.
            var startValues = new Dictionary<string, float>(StringComparer.Ordinal);
            var start = file.Get(ObjRef.From(mixerDoc.Body["m_StartSnapshot"]).FileId);
            if (start?.Body["m_FloatValues"] is YMap values)
                foreach (var kv in values.Entries)
                    if (float.TryParse(kv.Value.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        startValues[kv.Key] = v;

            foreach (var p in mixerDoc.Body["m_ExposedParameters"]?.Items ?? Array.Empty<YNode>())
            {
                string name = p.Str("name");
                if (string.IsNullOrEmpty(name)) continue;
                // A parameter absent from the snapshot sits at its effect default: 0 dB volume.
                mixer.DeclareExposed(name, startValues.TryGetValue(p.Str("guid") ?? string.Empty, out var v) ? v : 0f);
            }

            // Groups with paths from the master group down.
            long masterId = ObjRef.From(mixerDoc.Body["m_MasterGroup"]).FileId;
            void AddGroup(long id, string parentPath, int depth)
            {
                var doc = file.Get(id);
                if (doc == null || doc.ClassId != GroupClass || depth > 32 || result.ContainsKey(id)) return;
                string name = doc.Body.Str("m_Name") ?? "Group";
                var group = new AudioMixerGroup { name = name, path = parentPath == null ? name : parentPath + "/" + name };
                mixer.AddGroup(group);
                result[id] = group;
                foreach (var child in doc.Body["m_Children"]?.Items ?? Array.Empty<YNode>())
                    AddGroup(ObjRef.From(child).FileId, group.path, depth + 1);
            }
            AddGroup(masterId, null, 0);

            foreach (var d in file.Documents)
                if (d.ClassId == SnapshotClass && !result.ContainsKey(d.FileId))
                {
                    var snap = new AudioMixerSnapshot { name = d.Body.Str("m_Name") };
                    mixer.AddSnapshot(snap);
                    result[d.FileId] = snap;
                }
            return result;
        }
    }
}
