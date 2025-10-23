using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
{
    static readonly int Opacity = Shader.PropertyToID("_opacity");

    [Header("Scene Refs")]
    [SerializeField] private Crystal crystalPrefab;

    Crystal _ghostCrystal;
    float _lastUseTime = -Mathf.Infinity;

    IVessel _ship;
    IVesselStatus _status;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship = shipStatus.Vessel;
    }

    public void Begin(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_ghostCrystal || !crystalPrefab) return;

        // Parent directly to the ship
        _ghostCrystal = Instantiate(crystalPrefab, ship.Transform);
        _ghostCrystal.transform.localPosition = new Vector3(0f, 0f, so.ForwardOffset);
        _ghostCrystal.transform.localRotation = Quaternion.identity;

        PrepareGhost(_ghostCrystal, so);
    }

    public void Commit(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        if (!_ghostCrystal) return;

        // Detach before activation so it stays in world space
        _ghostCrystal.transform.SetParent(null, true);

        ActivateCrystal(_ghostCrystal, status);
        _ghostCrystal = null;

        _lastUseTime = Time.time;
        Debug.Log($"[DeployTeamCrystal] Crystal deployed. Cooldown started ({so.Cooldown}s)");
    }

    void PrepareGhost(Crystal cr, DeployTeamCrystalActionSO so)
    {
        cr.enabled = false;
        foreach (var col in cr.GetComponentsInChildren<Collider>(true)) col.enabled = false;

        var fadeIns = cr.GetComponentsInChildren<FadeIn>(true);
        foreach (var fade in fadeIns)
        {
            fade.enabled = false;
            var r = fade.gameObject.GetComponent<Renderer>();
            if (r && r.material) r.material.SetFloat(Opacity, so.FadeValue);
        }
    }

    void ActivateCrystal(Crystal cr, IVesselStatus status)
    {
        cr.ownDomain = status.Domain;
        foreach (var col in cr.GetComponentsInChildren<Collider>(true)) col.enabled = true;
        foreach (var fade in cr.GetComponentsInChildren<FadeIn>(true)) fade.enabled = true;
        cr.enabled = true;
        cr.ActivateCrystal();
    }
}
