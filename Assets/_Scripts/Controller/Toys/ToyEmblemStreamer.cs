using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fills every toy's <see cref="ToyEmblem"/> at <b>one slot per frame</b>, toybox-wide.
    ///
    /// The toybox is placed on the <c>OnClientReady</c> frame - on EVERY entry to Menu_Main, not
    /// just boot - and that frame is already the most expensive one in the menu. So emblems build
    /// nothing there: they register here, and this pump spreads the work across the following
    /// frames, comfortably inside the toys' own 1.2s bloom-in.
    ///
    /// <b>Round-robin, breadth-first:</b> every emblem's core lands before any satellite does, so
    /// the toybox becomes identifiable as early as possible instead of one toy at a time.
    /// A slot that reports itself <c>heavy</c> gets a clear frame after it, so two expensive
    /// builds never land back to back (the <see cref="CellSelectorToy"/> streaming idiom).
    ///
    /// The pump exits once a full pass builds nothing, so an idle toybox costs zero per frame.
    /// </summary>
    public sealed class ToyEmblemStreamer : MonoBehaviour
    {
        readonly List<ToyEmblem> _emblems = new();
        CancellationTokenSource _cts;
        bool _running;

        public void Register(ToyEmblem emblem)
        {
            if (!emblem || _emblems.Contains(emblem)) return;
            _emblems.Add(emblem);
            RequestRefresh();
        }

        /// <summary>Restart the pump (a registration, or an emblem that re-queued its slots).</summary>
        public void RequestRefresh()
        {
            if (_running) return;
            _cts ??= CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            Pump(_cts.Token).Forget();
        }

        async UniTaskVoid Pump(CancellationToken ct)
        {
            _running = true;
            try
            {
                // Yield BEFORE the first build. An `async UniTaskVoid` body runs synchronously up
                // to its first suspension, so without this the pump's first slot is built on the
                // stack of whoever started it - which is exactly the frame this class exists to
                // keep free (ToyboxController.PlaceToys, on every entry to Menu_Main) and, for a
                // live-key rebuild, inside ToyEmblem.Update. One frame of latency on a ~0.3s
                // stream, in exchange for "nothing builds on the caller's frame" being true.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                while (!ct.IsCancellationRequested)
                {
                    bool builtAny = false;

                    for (int i = 0; i < _emblems.Count; i++)
                    {
                        var emblem = _emblems[i];
                        if (!emblem || !emblem.HasPending) continue;
                        if (!emblem.TryBuildNextSlot(out bool heavy)) continue;

                        builtAny = true;
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        if (heavy) await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    if (!builtAny) break;
                }
            }
            finally
            {
                _running = false;
            }
        }

        void OnDestroy()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
