using System;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary><c>--dump-ui NAME[:DEPTH]</c>: the subtree under every object named NAME — world rect, activity, components.</summary>
    public static class UiDump
    {
        /// <summary>
        /// <c>--dump-ui-at X,Y</c>: every active, visible UI graphic covering that pixel (top-left
        /// origin, like a screenshot), with its path, sprite/texture, colour and effective alpha -
        /// the port's "Report On-Screen UI" for "what is that thing on screen?".
        /// </summary>
        public static void PrintAt(string spec)
        {
            var xy = spec.Split(',');
            float x = float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture);
            float y = Screen.height - float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine($"[dump-ui-at] ({spec}) on {Screen.width}x{Screen.height}");
            foreach (var g in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Engine.UI.Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!g.isActiveAndEnabled || g.transform is not RectTransform rt) continue;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                if (x < c[0].x || x > c[2].x || y < c[0].y || y > c[2].y) continue;
                float alpha = g.color.a * g.canvasRenderer.GetAlpha();
                for (var t = g.transform; t != null; t = t.parent)
                    foreach (var cg in t.gameObject.GetComponents<CosmicShore.Engine.CanvasGroup>()) alpha *= cg.alpha;
                if (alpha < 0.01f) continue;
                string tex = g is CosmicShore.Engine.UI.Image img ? (img.sprite != null ? "sprite=" + img.sprite.name : "sprite=NULL")
                    : g is CosmicShore.Engine.UI.RawImage raw ? (raw.texture != null ? "texture=" + raw.texture.name : "texture=NULL") : g.GetType().Name;
                Console.WriteLine($"  {Path(g.transform)}  [{g.GetType().Name}] {tex} color={g.color} alpha={alpha:0.00} rect=({c[0].x:0},{c[0].y:0})-({c[2].x:0},{c[2].y:0})");
            }
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        public static void Print(string spec)
        {
            int depth = 4;
            var parts = spec.Split(':');
            if (parts.Length > 1) int.TryParse(parts[1], out depth);
            var roots = CosmicShore.Engine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == parts[0]).ToList();
            Console.WriteLine($"[dump-ui] '{parts[0]}': {roots.Count} match(es)");
            foreach (var r in roots) Walk(r, 0, depth);
        }

        static void Walk(Transform t, int level, int max)
        {
            var go = t.gameObject;
            string rect = "";
            if (t is RectTransform rt)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                rect = $" rect=({c[0].x:0},{c[0].y:0})-({c[2].x:0},{c[2].y:0}) aMin={rt.anchorMin} aMax={rt.anchorMax} pivot={rt.pivot} size={rt.sizeDelta} pos={rt.anchoredPosition} scale={rt.localScale}";
            }
            else rect = $" world={t.position} fwd={t.forward} scale={t.lossyScale}";
            var comps = string.Join(",", go.GetComponents<Component>().Where(c => c is not Transform).Select(c => c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : "")));
            Console.WriteLine($"{new string(' ', level * 2)}- {t.name} active={go.activeSelf}/{go.activeInHierarchy}{rect} [{comps}]");
            foreach (var img in go.GetComponents<CosmicShore.Engine.UI.Image>())
                Console.WriteLine($"{new string(' ', level * 2)}    image sprite={(img.sprite != null ? img.sprite.name + " tex=" + (img.sprite.texture != null ? img.sprite.texture.name : "null") + " border=" + img.sprite.border : "null")} type={img.type} color={img.color} fill={img.fillAmount} ppum={img.pixelsPerUnitMultiplier} raycast={img.raycastTarget}");
            foreach (var m in go.GetComponents<CosmicShore.Engine.UI.Mask>())
                Console.WriteLine($"{new string(' ', level * 2)}    mask showGraphic={m.showMaskGraphic} enabled={m.enabled}");
            foreach (var g in go.GetComponents<CosmicShore.Engine.CanvasGroup>())
                Console.WriteLine($"{new string(' ', level * 2)}    group alpha={g.alpha} blocks={g.blocksRaycasts}");
            if (level >= max) return;
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), level + 1, max);
        }
    }
}
