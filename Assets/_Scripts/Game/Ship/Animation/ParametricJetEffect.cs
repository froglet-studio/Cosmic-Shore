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

    void Start()
    {
        jetParticles = GetComponent<ParticleSystem>();
        jetMain = jetParticles.main;
        jetEmission = jetParticles.emission;
        jetMaterial = GetComponent<Renderer>().material;

        UpdateJetProperties();
    }

    void Update()
    {
        UpdateJetProperties();
    }

    void UpdateJetProperties()
    {
        // Update particle system
        jetMain.startLifetime = jetLength / jetMain.startSpeed.constant;
        jetMain.startSize = jetWidth;
        jetEmission.rateOverTime = jetPower * 100f;

        // Update material properties
        jetMaterial.SetFloat("_JetPower", jetPower);
        jetMaterial.SetFloat("_AfterburnerIntensity", afterburnerActive ? afterburnerIntensity : 0f);
        jetMaterial.SetColor("_AfterburnerColor", afterburnerColor);
        jetMaterial.SetFloat("_MachDiamondFrequency", machDiamondsEnabled ? machDiamondFrequency : 0f);
        jetMaterial.SetFloat("_MachDiamondIntensity", machDiamondIntensity);
        jetMaterial.SetFloat("_HeatDistortion", heatDistortionIntensity);

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