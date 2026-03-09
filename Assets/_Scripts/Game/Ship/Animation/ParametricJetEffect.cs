using UnityEngine;
using CosmicShore.Utility;

[RequireComponent(typeof(ParticleSystem))]
public class ParametricJetEffect : MonoBehaviour
{
    [Header("Core Jet Parameters")]
    public float jetPower = 1f;
    public float jetWidth = 1f;
    public float jetLength = 5f;
    public Gradient jetColorGradient;

    [Header("Afterburner Parameters")]
    public bool afterburnerActive = false;
    public float afterburnerIntensity = 1f;
    public Color afterburnerColor = Color.blue;

    [Header("Mach Diamond Parameters")]
    public bool machDiamondsEnabled = true;
    public float machDiamondFrequency = 1f;
    public float machDiamondIntensity = 1f;

    [Header("Heat Distortion")]
    public float heatDistortionIntensity = 0.1f;

    private ParticleSystem jetParticles;
    private ParticleSystem.MainModule jetMain;
    private ParticleSystem.EmissionModule jetEmission;
    private Material jetMaterial;

    // Mobile: cap emission rate and skip per-frame material updates when values haven't changed
    private float _emissionCap = 100f;
    private bool _isMobile;
    private float _lastJetPower = -1f;
    private bool _lastAfterburner;
    private static readonly int JetPowerID = Shader.PropertyToID("_JetPower");
    private static readonly int AfterburnerIntensityID = Shader.PropertyToID("_AfterburnerIntensity");
    private static readonly int AfterburnerColorID = Shader.PropertyToID("_AfterburnerColor");
    private static readonly int MachDiamondFrequencyID = Shader.PropertyToID("_MachDiamondFrequency");
    private static readonly int MachDiamondIntensityID = Shader.PropertyToID("_MachDiamondIntensity");
    private static readonly int HeatDistortionID = Shader.PropertyToID("_HeatDistortion");

    void Start()
    {
        jetParticles = GetComponent<ParticleSystem>();
        jetMain = jetParticles.main;
        jetEmission = jetParticles.emission;
        jetMaterial = GetComponent<Renderer>().material;

        _isMobile = MobilePerformanceManager.IsMobile;
        _emissionCap = _isMobile ? 30f : 100f;

        UpdateJetProperties();
    }

    void Update()
    {
        // On mobile, only update material properties when values actually change
        if (_isMobile && Mathf.Approximately(_lastJetPower, jetPower) && _lastAfterburner == afterburnerActive)
        {
            // Still update particle emission (cheap)
            jetEmission.rateOverTime = jetPower * _emissionCap;
            return;
        }

        UpdateJetProperties();
    }

    void UpdateJetProperties()
    {
        _lastJetPower = jetPower;
        _lastAfterburner = afterburnerActive;

        // Update particle system
        jetMain.startLifetime = jetLength / jetMain.startSpeed.constant;
        jetMain.startSize = jetWidth;
        jetEmission.rateOverTime = jetPower * _emissionCap;

        // Update material properties using cached property IDs
        jetMaterial.SetFloat(JetPowerID, jetPower);
        jetMaterial.SetFloat(AfterburnerIntensityID, afterburnerActive ? afterburnerIntensity : 0f);
        jetMaterial.SetColor(AfterburnerColorID, afterburnerColor);
        jetMaterial.SetFloat(MachDiamondFrequencyID, machDiamondsEnabled ? machDiamondFrequency : 0f);
        jetMaterial.SetFloat(MachDiamondIntensityID, machDiamondIntensity);
        jetMaterial.SetFloat(HeatDistortionID, heatDistortionIntensity);

        // Update color gradient
        Gradient gradientCopy = new Gradient();
        gradientCopy.SetKeys(jetColorGradient.colorKeys, jetColorGradient.alphaKeys);
        jetMain.startColor = gradientCopy;
    }

    public void SetJetPower(float power)
    {
        jetPower = power;
        UpdateJetProperties();
    }

    public void ToggleAfterburner(bool active)
    {
        afterburnerActive = active;
        UpdateJetProperties();
    }

    // Add more methods for gameplay integration and dynamic adjustments
}