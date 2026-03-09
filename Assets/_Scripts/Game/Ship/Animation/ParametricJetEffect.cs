using UnityEngine;

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

    // Cached shader property IDs
    private static readonly int JetPowerID = Shader.PropertyToID("_JetPower");
    private static readonly int AfterburnerIntensityID = Shader.PropertyToID("_AfterburnerIntensity");
    private static readonly int AfterburnerColorID = Shader.PropertyToID("_AfterburnerColor");
    private static readonly int MachDiamondFrequencyID = Shader.PropertyToID("_MachDiamondFrequency");
    private static readonly int MachDiamondIntensityID = Shader.PropertyToID("_MachDiamondIntensity");
    private static readonly int HeatDistortionID = Shader.PropertyToID("_HeatDistortion");

    // Cached gradient to avoid per-frame allocation
    private Gradient _cachedGradient;

    // Track previous values to skip redundant updates
    private float _prevJetPower;
    private float _prevJetWidth;
    private float _prevJetLength;
    private bool _prevAfterburnerActive;
    private float _prevAfterburnerIntensity;
    private bool _prevMachDiamondsEnabled;
    private float _prevMachDiamondFrequency;
    private float _prevMachDiamondIntensity;
    private float _prevHeatDistortionIntensity;

    void Start()
    {
        jetParticles = GetComponent<ParticleSystem>();
        jetMain = jetParticles.main;
        jetEmission = jetParticles.emission;
        jetMaterial = GetComponent<Renderer>().material;
        _cachedGradient = new Gradient();

        UpdateJetProperties();
        CachePreviousValues();
    }

    void Update()
    {
        if (HasChanged())
        {
            UpdateJetProperties();
            CachePreviousValues();
        }
    }

    bool HasChanged()
    {
        return !Mathf.Approximately(jetPower, _prevJetPower)
            || !Mathf.Approximately(jetWidth, _prevJetWidth)
            || !Mathf.Approximately(jetLength, _prevJetLength)
            || afterburnerActive != _prevAfterburnerActive
            || !Mathf.Approximately(afterburnerIntensity, _prevAfterburnerIntensity)
            || machDiamondsEnabled != _prevMachDiamondsEnabled
            || !Mathf.Approximately(machDiamondFrequency, _prevMachDiamondFrequency)
            || !Mathf.Approximately(machDiamondIntensity, _prevMachDiamondIntensity)
            || !Mathf.Approximately(heatDistortionIntensity, _prevHeatDistortionIntensity);
    }

    void CachePreviousValues()
    {
        _prevJetPower = jetPower;
        _prevJetWidth = jetWidth;
        _prevJetLength = jetLength;
        _prevAfterburnerActive = afterburnerActive;
        _prevAfterburnerIntensity = afterburnerIntensity;
        _prevMachDiamondsEnabled = machDiamondsEnabled;
        _prevMachDiamondFrequency = machDiamondFrequency;
        _prevMachDiamondIntensity = machDiamondIntensity;
        _prevHeatDistortionIntensity = heatDistortionIntensity;
    }

    void UpdateJetProperties()
    {
        // Update particle system
        jetMain.startLifetime = jetLength / jetMain.startSpeed.constant;
        jetMain.startSize = jetWidth;
        jetEmission.rateOverTime = jetPower * 100f;

        // Update material properties using cached IDs
        jetMaterial.SetFloat(JetPowerID, jetPower);
        jetMaterial.SetFloat(AfterburnerIntensityID, afterburnerActive ? afterburnerIntensity : 0f);
        jetMaterial.SetColor(AfterburnerColorID, afterburnerColor);
        jetMaterial.SetFloat(MachDiamondFrequencyID, machDiamondsEnabled ? machDiamondFrequency : 0f);
        jetMaterial.SetFloat(MachDiamondIntensityID, machDiamondIntensity);
        jetMaterial.SetFloat(HeatDistortionID, heatDistortionIntensity);

        // Reuse cached gradient instead of allocating new one each frame
        _cachedGradient.SetKeys(jetColorGradient.colorKeys, jetColorGradient.alphaKeys);
        jetMain.startColor = _cachedGradient;
    }

    public void SetJetPower(float power)
    {
        jetPower = power;
        UpdateJetProperties();
        _prevJetPower = power;
    }

    public void ToggleAfterburner(bool active)
    {
        afterburnerActive = active;
        UpdateJetProperties();
        _prevAfterburnerActive = active;
    }
}
