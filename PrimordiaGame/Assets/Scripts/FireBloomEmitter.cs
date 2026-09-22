using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystemRenderer))]
public sealed class FireBloomEmitter : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    [SerializeField, Min(1f)]
    [Tooltip("HDR brightness applied only to these fire particles so they cross the bloom threshold.")]
    private float bloomBrightness = 3.25f;

    [SerializeField]
    [Tooltip("Warm tint for the fire's HDR bloom source.")]
    private Color bloomTint = new(1f, 0.85f, 0.55f, 1f);

    private ParticleSystemRenderer particleRenderer;

    private void OnEnable()
    {
        ApplyBloomBrightness();
    }

    private void OnValidate()
    {
        bloomBrightness = Mathf.Max(1f, bloomBrightness);

        if (isActiveAndEnabled)
            ApplyBloomBrightness();
    }

    private void OnDisable()
    {
        GetParticleRenderer().SetPropertyBlock(null);
    }

    private void ApplyBloomBrightness()
    {
        ParticleSystemRenderer renderer = GetParticleRenderer();
        Material material = renderer.sharedMaterial;

        if (material == null)
            return;

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);

        BoostColor(material, properties, BaseColorId);
        BoostColor(material, properties, ColorId);
        BoostColor(material, properties, TintColorId);

        renderer.SetPropertyBlock(properties);
    }

    private void BoostColor(Material material, MaterialPropertyBlock properties, int propertyId)
    {
        if (!material.HasProperty(propertyId))
            return;

        Color source = material.GetColor(propertyId);
        source.r *= bloomBrightness * bloomTint.r;
        source.g *= bloomBrightness * bloomTint.g;
        source.b *= bloomBrightness * bloomTint.b;
        properties.SetColor(propertyId, source);
    }

    private ParticleSystemRenderer GetParticleRenderer()
    {
        if (particleRenderer == null)
            particleRenderer = GetComponent<ParticleSystemRenderer>();

        return particleRenderer;
    }
}
