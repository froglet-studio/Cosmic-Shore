using System.Collections;
using CosmicShore.Game;
using UnityEngine;

public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
{
    static readonly int Opacity = Shader.PropertyToID("_opacity");

    [Header("Scene Refs")]
    [SerializeField] private Crystal crystalPrefab;

    Crystal _ghostCrystal;
    Coroutine _followRoutine;
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
        if (Time.time - _lastUseTime < so.Cooldown)
        {
            float remaining = so.Cooldown - (Time.time - _lastUseTime);
            Debug.Log($"[DeployTeamCrystal] Cooldown – {remaining:F1}s left");
            return;
        }
        if (_ghostCrystal != null || crystalPrefab == null) return;

        Vector3 pos = GetSpawnPoint(so, ship, status);
        Quaternion rot = Quaternion.LookRotation(ship.Transform.forward, ship.Transform.up);

        _ghostCrystal = Instantiate(crystalPrefab, pos, rot);
        PrepareGhost(_ghostCrystal, so);
        _followRoutine = StartCoroutine(FollowShip(so, ship));
    }

    public void Commit(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_ghostCrystal == null) return;

        if (_followRoutine != null) { StopCoroutine(_followRoutine); _followRoutine = null; }

        ActivateCrystal(_ghostCrystal, status);
        _ghostCrystal = null;

        _lastUseTime = Time.time;
        Debug.Log($"[DeployTeamCrystal] Crystal deployed. Cooldown started ({so.Cooldown}s)");
    }

    Vector3 GetSpawnPoint(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        Vector3 pos = ship.Transform.position + ship.Transform.forward * so.ForwardOffset;

        if (so.RayMask.value != 0 &&
            Physics.Raycast(ship.Transform.position, ship.Transform.forward,
                out RaycastHit hit, so.ForwardOffset, so.RayMask, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
        }
        return pos;
    }

    IEnumerator FollowShip(DeployTeamCrystalActionSO so, IVessel ship)
    {
        while (_ghostCrystal != null)
        {
            _ghostCrystal.transform.SetPositionAndRotation(
                GetSpawnPoint(so, ship, _status),
                Quaternion.LookRotation(ship.Transform.forward, ship.Transform.up));
            yield return null;
        }
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
            if (r != null && r.material != null) r.material.SetFloat(Opacity, so.FadeValue);
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