using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void CreateRootFlares(FilamentRuntime filament)
        {
            CreateRootFlare(filament, 0f, $"{filament.Index:00} Root A");
            CreateRootFlare(filament, 1f, $"{filament.Index:00} Root B");
            CreateRootSpriteOverlay(filament, 0f, filament.Index * 2);
            CreateRootSpriteOverlay(filament, 1f, filament.Index * 2 + 1);
        }

        void CreateRootFlare(FilamentRuntime filament, float axis01, string flareName)
        {
            for (int fork = -5; fork <= 5; fork++)
            {
                float fork01 = Mathf.Abs(fork) / 5f;
                float width = Mathf.Max(0.14f, tubeRadius * 0.0012f) * Mathf.Lerp(1.2f, 0.55f, fork01);
                float length = Mathf.Max(15f, tubeRadius * 0.062f) * Mathf.Lerp(1f, 0.66f, fork01);
                var line = MakeLine($"Filament {flareName} Root Fork {fork}", 6, width, _whiteEnergyMaterial);
                AddRootFlare(filament, line, axis01, fork, false, width, length);

                if (fork % 2 != 0)
                    continue;

                var branch = MakeLine($"Filament {flareName} Side Branch {fork}", 4, width * 0.58f, _whiteEnergyMaterial);
                AddRootFlare(filament, branch, axis01, fork, true, width * 0.58f, length);
            }

            UpdateFilamentRoots(filament);
        }

        void AddRootFlare(
            FilamentRuntime filament,
            LineRenderer line,
            float axis01,
            int fork,
            bool isBranch,
            float width,
            float length)
        {
            var root = new RootFlareRuntime
            {
                Line = line,
                Filament = filament,
                Axis01 = axis01,
                ForkIndex = fork,
                IsBranch = isBranch,
                Width = width,
                Length = length
            };
            filament.RootFlares.Add(root);
        }

        void UpdateFilamentRoots(FilamentRuntime filament)
        {
            if (filament?.RootFlares == null)
                return;

            for (int i = 0; i < filament.RootFlares.Count; i++)
                UpdateRootFlareLine(filament.RootFlares[i]);
        }

        void UpdateRootFlareLine(RootFlareRuntime root)
        {
            if (root?.Line == null || root.Filament == null)
                return;

            Vector3 endpoint = FilamentSurfacePoint(root.Filament, root.Axis01);
            Vector3 outward = root.Axis01 < 0.5f ? -root.Filament.Direction : root.Filament.Direction;
            Vector3 side = root.Filament.Side;
            Vector3 up = root.Filament.Up;
            Vector3 spread = RootSpread(outward, side, up, root.ForkIndex);

            root.Line.startWidth = root.Width * (1f + BeatPulse() * 0.12f);
            root.Line.endWidth = root.Width * 0.36f;

            if (root.IsBranch)
            {
                UpdateRootBranch(root, endpoint, spread, side, up);
                return;
            }

            for (int point = 0; point < root.Line.positionCount; point++)
            {
                float t = point / (float)(root.Line.positionCount - 1);
                Vector3 fork = side * Mathf.Sin(t * Mathf.PI * (2.1f + Mathf.Abs(root.ForkIndex) * 0.12f)) * root.Length * 0.16f;
                Vector3 pulse = up * Mathf.Sin(t * Mathf.PI) * root.Length * 0.22f;
                root.Line.SetPosition(point, endpoint + spread * (t * root.Length) + fork + pulse);
            }
        }

        void UpdateRootBranch(RootFlareRuntime root, Vector3 endpoint, Vector3 spread, Vector3 side, Vector3 up)
        {
            float sideSign = Mathf.Sign(root.ForkIndex == 0 ? 1 : root.ForkIndex);
            Vector3 branchRoot = endpoint + spread * (root.Length * 0.42f);
            Vector3 branchDirection = (spread + side * sideSign * 1.4f + up * 0.35f).normalized;

            for (int point = 0; point < root.Line.positionCount; point++)
            {
                float t = point / (float)(root.Line.positionCount - 1);
                Vector3 pulse = up * Mathf.Sin(t * Mathf.PI) * root.Length * 0.08f;
                root.Line.SetPosition(point, branchRoot + branchDirection * (t * root.Length * 0.46f) + pulse);
            }
        }

        static Vector3 RootSpread(Vector3 outward, Vector3 side, Vector3 up, int fork)
        {
            return (outward * 4.8f + side * fork * 2.45f + up * Mathf.Abs(fork) * 0.92f).normalized;
        }
    }
}
