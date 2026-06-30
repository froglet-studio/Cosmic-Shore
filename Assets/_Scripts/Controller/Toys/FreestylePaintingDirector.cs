using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the "fly by numbers" painting toy in freestyle. Revives the glue role of the
    /// removed <c>SinglePlayerFreestyleController</c> (deleted in commit 2009ae54): it listens
    /// for a painting toy's shape selection (<see cref="ShapeSignEvents.OnShapeSelected"/>) and
    /// runs the existing <see cref="ShapeDrawingManager"/> preview → draw flow.
    ///
    /// Toy-faithful: no Ready button, no countdown, no score gate. After the preview cinematic
    /// the drawing simply begins; when the player exits the shape the painting toy re-arms on
    /// its own. Requires a wired <see cref="ShapeDrawingManager"/> (with its crystal manager /
    /// cell data) in the scene — the heavy shape infrastructure recoverable from the removed
    /// MinigameFreestyle scene.
    /// </summary>
    public class FreestylePaintingDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("The shape-drawing engine. Required — wire its crystal manager / " +
                                 "cell data on the manager itself.")]
        ShapeDrawingManager shapeDrawingManager;

        [SerializeField, Tooltip("Seconds to wait after the preview cinematic before the drawing begins.")]
        float autoBeginDelaySeconds = 0.5f;

        bool _busy;

        void OnEnable()
        {
            ShapeSignEvents.OnShapeSelected += HandleShapeSelected;
            if (shapeDrawingManager)
            {
                shapeDrawingManager.OnPreviewComplete.AddListener(HandlePreviewComplete);
                shapeDrawingManager.OnFreestyleResumed.AddListener(HandleResumed);
            }
        }

        void OnDisable()
        {
            ShapeSignEvents.OnShapeSelected -= HandleShapeSelected;
            if (shapeDrawingManager)
            {
                shapeDrawingManager.OnPreviewComplete.RemoveListener(HandlePreviewComplete);
                shapeDrawingManager.OnFreestyleResumed.RemoveListener(HandleResumed);
            }
        }

        void HandleShapeSelected(ShapeDefinition def, Vector3 origin, Domains _)
        {
            if (_busy || def == null || !shapeDrawingManager) return;
            _busy = true;
            shapeDrawingManager.StartShapeSequence(def, origin);
        }

        void HandlePreviewComplete()
        {
            AutoBegin(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid AutoBegin(CancellationToken ct)
        {
            if (autoBeginDelaySeconds > 0f)
                await UniTask.Delay((int)(autoBeginDelaySeconds * 1000f), cancellationToken: ct);
            if (shapeDrawingManager) shapeDrawingManager.BeginDrawing();
        }

        void HandleResumed() => _busy = false;
    }
}
