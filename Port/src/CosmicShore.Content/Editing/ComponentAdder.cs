using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Editing
{
    /// <summary>
    /// "Add Component": resolves a component by name and writes it onto a GameObject the way
    /// the Unity Editor would — a project script from its C# type's serialized defaults
    /// (<see cref="ComponentSerializer"/>), a built-in or package component from its measured
    /// template (<see cref="ComponentTemplates"/>). Honours Unity's component rules:
    /// <c>[RequireComponent]</c> adds what is required first, <c>[DisallowMultipleComponent]</c>
    /// (and every built-in that is one-per-object) refuses a second copy, and a UI component turns
    /// the object's Transform into a RectTransform.
    /// </summary>
    public sealed class ComponentAdder
    {
        readonly UnityAssetEditor _ed;
        readonly ScriptCatalog _scripts;
        readonly ComponentTemplates _templates;

        public ComponentAdder(UnityAssetEditor editor, ScriptCatalog scripts, ComponentTemplates templates)
        { _ed = editor; _scripts = scripts; _templates = templates; }

        /// <summary>What an add did: every component it created (required ones first) and anything worth saying.</summary>
        public sealed class Result
        {
            public readonly List<(long id, string type, string source)> Added = new();
            public readonly List<string> Notes = new();
        }

        // Built-ins Unity allows only one of per GameObject.
        static readonly HashSet<string> OnePerObject = new(StringComparer.Ordinal)
        {
            "Transform", "RectTransform", "MeshFilter", "MeshRenderer", "SkinnedMeshRenderer", "Rigidbody", "Rigidbody2D",
            "Camera", "Canvas", "CanvasRenderer", "CanvasGroup", "Animator", "Animation", "Light", "AudioListener",
            "LineRenderer", "TrailRenderer", "SpriteRenderer", "ParticleSystem", "ParticleSystemRenderer", "LODGroup", "SortingGroup",
        };

        /// <summary>Adds the component named <paramref name="name"/> (class name, full name, or built-in type name).</summary>
        public Result Add(long goId, string name)
        {
            var r = new Result();
            AddInto(goId, name, r, depth: 0);
            return r;
        }

        void AddInto(long goId, string name, Result r, int depth)
        {
            if (depth > 8) throw new ArgumentException("RequireComponent chain too deep");
            // An abstract requirement means "some kind of": Unity supplies its default member.
            name = name switch { "Collider" => "BoxCollider", "Renderer" => "MeshRenderer", "Collider2D" => "BoxCollider2D", _ => name };
            if (name is "Transform" or "RectTransform")
            {
                if (name == "RectTransform" && _ed.EnsureRectTransform(goId)) r.Notes.Add("Transform converted to RectTransform");
                return;
            }

            if (ComponentTemplates.BuiltInClassIds.TryGetValue(name, out int classId))
            {
                if (OnePerObject.Contains(name) && Has(goId, classId, null))
                {
                    if (depth > 0) return; // a requirement already satisfied
                    throw new ArgumentException($"&{goId} already has a {name} (Unity allows one)");
                }
                if (name == "CanvasRenderer") _ed.EnsureRectTransform(goId);
                var t = _templates.ForClass(classId, name)
                        ?? throw new ArgumentException($"no saved {name} in the project to measure a template from");
                long id = _ed.AddComponentDocument(goId, classId, t.TypeName, (YMap)t.Body.Clone());
                r.Added.Add((id, name, t.Source));
                return;
            }

            var matches = _scripts.Find(name).Where(s => s.Type != null).ToList();
            if (matches.Count == 0) throw new ArgumentException($"no component named '{name}' (a script under Assets/, a package script, or a built-in)");
            if (matches.Count > 1)
                throw new ArgumentException($"'{name}' is ambiguous: {string.Join(", ", matches.Select(m => m.Type.FullName))}; use the full name");
            AddScript(goId, matches[0], r, depth);
        }

        // Types whose add is under way: a [RequireComponent] cycle (Prism <-> PrismScaleAnimator)
        // is satisfied by the add already in progress, not by a second one.
        readonly HashSet<Type> _inProgress = new();

        void AddScript(long goId, ScriptInfo s, Result r, int depth)
        {
            var type = s.Type;
            if (_inProgress.Contains(type)) return;
            if (type.IsAbstract || type.IsGenericTypeDefinition) throw new ArgumentException($"{type.Name} is abstract; add a concrete subclass");
            if (!typeof(CosmicShore.Engine.Component).IsAssignableFrom(type))
                throw new ArgumentException($"{type.Name} is not a component (a ScriptableObject is an asset, not a component)");
            if (Has(goId, 114, s.Guid))
            {
                if (depth > 0) return;
                if (Disallows(type)) throw new ArgumentException($"{type.Name} is [DisallowMultipleComponent] and &{goId} already has one");
            }

            // One Graphic per GameObject: a CanvasRenderer draws exactly one.
            if (IsGraphic(type) && _ed.Components(goId).FirstOrDefault(c => c.ClassId == 114
                    && c.Body["m_Script"]?.Str("guid") is { } g && IsGraphic(_scripts.FromGuid(g).Type)) is { } other)
                throw new ArgumentException($"&{goId} already has a Graphic (&{other.FileId}); Unity allows one per GameObject");

            // [RequireComponent] first, as the Editor adds them before the component itself.
            _inProgress.Add(type);
            try
            {
                foreach (var req in Required(type)) EnsureRequirement(goId, type, req, r, depth + 1);
            }
            finally { _inProgress.Remove(type); }

            // The port replaces package and plugin scripts (uGUI, TMP, FMOD, SOAP …) with its own
            // stand-ins, whose fields are not the original's: those are added from a measured
            // template. A project script is its own type, so its C# type IS its layout.
            bool package = s.IsStandIn;
            YMap body;
            string source;
            if (package)
            {
                var t = _templates.ForScript(s.Guid, type.Name)
                        ?? throw new ArgumentException($"no saved {type.Name} in the project to measure a template from");
                body = (YMap)t.Body.Clone();
                body.Set("m_EditorClassIdentifier", new YScalar(s.EditorClassIdentifier));
                source = t.Source;
            }
            else
            {
                if (!ComponentSerializer.HasKnownBaseLayout(type))
                    r.Notes.Add($"{type.Name} derives from a package class whose serialized fields are not modelled; only its own fields were written");
                body = ComponentSerializer.MonoBehaviourBody(type, goId, s.Guid, s.EditorClassIdentifier);
                source = "serialized defaults of " + type.FullName;
            }
            long id = _ed.AddComponentDocument(goId, 114, "MonoBehaviour", body);
            r.Added.Add((id, type.Name, source));
        }

        void EnsureRequirement(long goId, Type owner, Type req, Result r, int depth)
        {
            if (req.IsInterface || !typeof(CosmicShore.Engine.Component).IsAssignableFrom(req))
            {
                r.Notes.Add($"{owner.Name} requires {req.Name}, which is not a component type; Unity ignores that requirement");
                return;
            }
            if (req.Name is "Transform") return;
            if (req.Name is "RectTransform") { AddInto(goId, "RectTransform", r, depth); return; }
            if (Satisfied(goId, req) || _inProgress.Any(t => req.IsAssignableFrom(t))) return;
            if (req.IsAbstract && !ComponentTemplates.BuiltInClassIds.ContainsKey(DefaultFor(req.Name)))
                throw new ArgumentException($"{owner.Name} requires a {req.Name}, which is abstract; add a concrete one first");
            AddInto(goId, RequiredName(req), r, depth);
        }

        static string DefaultFor(string abstractName)
            => abstractName switch { "Collider" => "BoxCollider", "Renderer" => "MeshRenderer", "Collider2D" => "BoxCollider2D", _ => abstractName };

        /// <summary>Does the GameObject already have a component of (a subtype of) <paramref name="req"/>?</summary>
        bool Satisfied(long goId, Type req)
        {
            foreach (var c in _ed.Components(goId))
            {
                Type t = c.ClassId == 114
                    ? (c.Body["m_Script"]?.Str("guid") is { } g ? _scripts.FromGuid(g).Type : null)
                    : EngineType(c.TypeName);
                if (t != null && req.IsAssignableFrom(t)) return true;
            }
            return false;
        }

        static Type EngineType(string name)
            => typeof(CosmicShore.Engine.GameObject).Assembly.GetTypes()
                   .FirstOrDefault(t => t.Name == name && typeof(CosmicShore.Engine.Component).IsAssignableFrom(t));

        bool Has(long goId, int classId, string scriptGuid)
            => _ed.Components(goId).Any(c => c.ClassId == classId
                                              && (scriptGuid == null || c.Body["m_Script"]?.Str("guid") == scriptGuid));

        static bool IsGraphic(Type t)
        {
            for (var b = t; b != null; b = b.BaseType)
                if (b == typeof(CosmicShore.Engine.UI.Graphic)) return true;
            return false;
        }

        static bool Disallows(Type t)
            => t.GetCustomAttributes(true).Any(a => a.GetType().Name == "DisallowMultipleComponent");

        static IEnumerable<Type> Required(Type t)
        {
            foreach (var a in t.GetCustomAttributes(true).Where(a => a.GetType().Name == "RequireComponentAttribute"))
                foreach (var f in new[] { "m_Type0", "m_Type1", "m_Type2" })
                    if (a.GetType().GetField(f)?.GetValue(a) is Type req) yield return req;
        }

        /// <summary>A required type's add-name: a built-in by type name, a script by full name.</summary>
        static string RequiredName(Type t)
            => ComponentTemplates.BuiltInClassIds.ContainsKey(t.Name) || t.Assembly == typeof(CosmicShore.Engine.GameObject).Assembly
                ? t.Name
                : t.FullName;
    }
}
