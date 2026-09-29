using System;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary><c>--dump-ui NAME[:DEPTH]</c>: the subtree under every object named NAME — world rect, activity, components.</summary>
    public static class UiDump
    {
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
