using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--report-render</c>: what the live scene asks the 3D pass to draw — every active
    /// renderer, whether its mesh and materials resolved, and which shaders those materials
    /// name. The measurement a renderer is built against.
    /// </summary>
    public static class RenderInventory
    {
        public static void Print()
        {
            var renderers = CosmicShore.Engine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var cams = CosmicShore.Engine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Console.WriteLine($"[render] {cams.Length} cameras, {renderers.Length} active renderers");
            foreach (var c in cams)
                Console.WriteLine($"  camera '{c.name}' enabled={c.enabled} main={(Camera.main == c)} depth={c.depth} fov={c.fieldOfView} clear={c.clearFlags} bg={c.backgroundColor} mask={c.cullingMask} target={(c.targetTexture != null ? c.targetTexture.name : "screen")} pos={c.transform.position}");

            var byKind = new Dictionary<string, int>();
            var byShader = new Dictionary<string, int>();
            var meshNames = new Dictionary<string, int>();
            foreach (var r in renderers)
            {
                if (!r.enabled) { Bump(byKind, r.GetType().Name + " (disabled)"); continue; }
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                Bump(byKind, $"{r.GetType().Name} mesh={(mesh != null ? "ok" : "MISSING")}");
                if (mesh != null) Bump(meshNames, mesh.name);
                var mats = r.sharedMaterials ?? Array.Empty<Material>();
                if (mats.Length == 0) Bump(byShader, "(no materials)");
                foreach (var m in mats) Bump(byShader, m == null ? "(null material)" : m.shader?.name ?? "(no shader)");
            }
            Console.WriteLine("  renderers:");
            foreach (var (k, n) in byKind.OrderByDescending(kv => kv.Value)) Console.WriteLine($"    {n,6}  {k}");
            Console.WriteLine("  shaders:");
            foreach (var (k, n) in byShader.OrderByDescending(kv => kv.Value)) Console.WriteLine($"    {n,6}  {k}");
            Console.WriteLine("  meshes (top 25):");
            foreach (var (k, n) in meshNames.OrderByDescending(kv => kv.Value).Take(25)) Console.WriteLine($"    {n,6}  {k}");
        }

        public static void PrintNetwork()
        {
            var nm = CosmicShore.Engine.Networking.NetworkManager.Singleton;
            if (nm == null) { Console.WriteLine("[net] no NetworkManager"); return; }
            Console.WriteLine($"[net] listening={nm.IsListening} server={nm.IsServer} client={nm.IsClient} clients={nm.ConnectedClientsIds.Count} spawned={nm.SpawnManager?.SpawnedObjectsList.Count}");
            foreach (var kv in nm.ConnectedClients)
                Console.WriteLine($"  client {kv.Key}: playerObject={(kv.Value.PlayerObject is null ? "null-ref" : kv.Value.PlayerObject.name + " destroyed=" + kv.Value.PlayerObject.IsDestroyed + " spawned=" + kv.Value.PlayerObject.IsSpawned + " #" + kv.Value.PlayerObject.GetInstanceID())}");
            foreach (var o in nm.SpawnManager?.SpawnedObjectsList ?? new System.Collections.Generic.HashSet<CosmicShore.Engine.Networking.NetworkObject>())
                Console.WriteLine($"  spawned #{o.NetworkObjectId} inst{o.GetInstanceID()} '{o.name}' owner={o.OwnerClientId} player={o.IsPlayerObject} scene={o.IsSceneObject}");
        }

        static void Bump(Dictionary<string, int> d, string k) => d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
    }
}
