using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void SpawnCloseCameraCollisionShower(Vector3 impactPosition, Color color, float scale)
        {
            if (!_mainCamera)
                return;

            float close01 = Mathf.InverseLerp(cameraMaxFollowDistance, cameraMinFollowDistance, _cameraFollowDistance);
            if (close01 < 0.58f)
                return;

            Vector3 cameraPosition = _mainCamera.transform.position;
            Vector3 throughCamera = (cameraPosition - impactPosition).normalized;
            if (throughCamera.sqrMagnitude < 0.01f)
                throughCamera = -_mainCamera.transform.forward;

            Vector3 showerPosition = Vector3.Lerp(cameraPosition, impactPosition, 0.38f);
            Color showerColor = Color.Lerp(color, Color.white, 0.32f);
            int count = Mathf.RoundToInt(Mathf.Lerp(28f, 86f, close01) * scale);
            float speed = Mathf.Lerp(20f, 54f, close01) * Mathf.Max(0.8f, scale);
            CreateParticleBurst("Bulk Close Camera Particle Shower", showerPosition, showerColor, count, 0.42f, speed, 0.08f, 0.72f);

            for (int i = 0; i < Mathf.RoundToInt(3f + close01 * 5f); i++)
            {
                Vector3 start = showerPosition + Random.insideUnitSphere * 2.2f;
                Vector3 end = start + throughCamera * Random.Range(12f, 34f) + Random.onUnitSphere * Random.Range(2f, 8f);
                CreateLightningBolt(start, end, 0.16f, 0.7f, false, 0.08f);
            }
        }
    }
}
