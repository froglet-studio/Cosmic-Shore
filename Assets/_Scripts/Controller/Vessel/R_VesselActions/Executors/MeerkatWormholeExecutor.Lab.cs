using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lab-only hooks for the Lavalamp side-by-side look test (<c>Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity</c>,
    /// <see cref="MeerkatTubeLook"/>): an executor with NO hull (never <see cref="Initialize"/>d, so its own Update does
    /// nothing and it is not in <see cref="Executors"/>) that forms real tunnels and grows real omnicrystal networks through
    /// the game's own code, and holds them open, so every view and the lens draw them exactly as in a match. Nothing in the
    /// game calls these.
    /// </summary>
    public sealed partial class MeerkatWormholeExecutor
    {
        /// <summary>Gives a hull-less lab executor its config (the field is serialized and private).</summary>
        internal void LabBind(MeerkatWormholeConfigSO cfg) => config = cfg;

        /// <summary>Forms a tunnel at <paramref name="a"/> heading <paramref name="t0"/>, shaped as the sticks would shape it
        /// (<paramref name="lq"/> LT 0..1, <paramref name="sx"/>/<paramref name="sy"/> the bend stick). It ripples out of its
        /// centre over the config's form time and then stays open (no rider ever comes out) while <see cref="LabStep"/> runs.</summary>
        internal Tunnel LabFormTunnel(Vector3 a, Vector3 t0, Vector3 side, float lq, float sx, float sy)
        {
            if (!config) return null;
            var sh = MeerkatWormholeMath.ShapeFor(config.Shape(), lq, sx, sy);
            var t = AddTunnel(a, t0.normalized, side.normalized, sh, Mathf.Max(0.05f, config.FormSeconds), ++_seq);
            _mine = null;   // a lab tunnel is never "the last one" a press collapses
            return t;
        }

        /// <summary>Grows an omnicrystal's wormhole network from <paramref name="a"/>, <paramref name="share"/> of the cell wide.</summary>
        internal MeerkatRootNetwork LabGrowNetwork(uint seed, float share, Vector3 a, Vector3 t0, Vector3 right, Vector3 up) =>
            config ? MeerkatRootNetwork.Grow(config, this, ++_rootSeq, seed, Mathf.Max(0.05f, share), a, t0, right, up) : null;

        /// <summary>Forms and holds this executor's tunnels and networks open (stay forever: no rider comes out).</summary>
        internal void LabStep(float dt)
        {
            if (!config) return;
            for (int i = 0; i < s_tunnels.Count; i++)
            {
                var t = s_tunnels[i];
                if (t.Maker == this) MeerkatWormholeMath.Step(ref t.W, t.L, t.FormT, float.PositiveInfinity, config.CollapseSeconds, dt);
            }
            var nets = MeerkatRootNetwork.Live;
            for (int i = 0; i < nets.Count; i++)
                if (nets[i].Maker == this) nets[i].Step(double.PositiveInfinity, config.CollapseSeconds, dt);
        }

        /// <summary>Removes every tunnel and network this executor made, at once.</summary>
        internal void LabClear()
        {
            for (int i = s_tunnels.Count - 1; i >= 0; i--)
                if (s_tunnels[i].Maker == this) s_tunnels.RemoveAt(i);
            var nets = MeerkatRootNetwork.Live;
            for (int i = nets.Count - 1; i >= 0; i--)
                if (nets[i].Maker == this) nets[i].Remove();
            _seq = 0;
        }
    }
}
