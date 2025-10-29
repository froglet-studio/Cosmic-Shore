using Obvious.Soap;
using CosmicShore.Game;
using UnityEngine;

public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
{
    static readonly int Opacity = Shader.PropertyToID("_opacity");

    [Header("Scene Refs")]
    [SerializeField] private Crystal crystalPrefab;

    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

    Crystal _ghostCrystal;

    IVessel _ship;
    IVesselStatus _status;

    void OnEnable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship   = shipStatus.Vessel;
    }

    public void Begin(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        if (_ghostCrystal || !crystalPrefab) return;

        _ghostCrystal = Instantiate(crystalPrefab, ship.Transform);
        _ghostCrystal.transform.localPosition = new Vector3(0f, 0f, so.ForwardOffset);
        _ghostCrystal.transform.localRotation = Quaternion.identity;

        PrepareGhost(_ghostCrystal, so);
    }

    public void Commit(DeployTeamCrystalActionSO so, IVessel ship, IVesselStatus status)
    {
        if (!_ghostCrystal) return;

        _ghostCrystal.transform.SetParent(null, true);

        ActivateCrystal(_ghostCrystal, status);
        _ghostCrystal = null;
    }

    void End()
    {
        if (!_ghostCrystal) return;
        Destroy(_ghostCrystal.gameObject);
        _ghostCrystal = null;
    }

    void OnTurnEndOfMiniGame() => End();

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
