using TMPro;
using UnityEngine;

/// <summary>World-space feedback for accepted damage and harvesting, with a bounded effect pool.</summary>
public sealed class CombatImpactFeedback : MonoBehaviour
{
    const int PoolSize = 16;
    static CombatImpactFeedback instance;
    readonly Impact[] impacts = new Impact[PoolSize];
    readonly AudioClip[] sounds = new AudioClip[6];
    Material particleMaterial;
    Material ringMaterial;
    Texture2D particleTexture;
    Camera viewer;
    int nextImpact;

    sealed class Impact
    {
        public Transform root;
        public ParticleSystem particles;
        public LineRenderer ring;
        public TextMeshPro label;
        public AudioSource audio;
        public Vector3 labelStart;
        public Color color;
        public float startedAt;
        public float duration;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => instance = null;

    public static void EnemyHit(CombatHit hit, float damage, bool killed)
    {
        var color = hit.ImpactKind switch
        {
            CombatImpactKind.Punch => new Color(1f, 0.82f, 0.4f),
            CombatImpactKind.Stab => new Color(1f, 0.38f, 0.3f),
            _ => Color.white,
        };
        Show(hit.Point, hit.Normal, color, new Color(1f, 0.15f, 0.08f),
            Mathf.CeilToInt(damage).ToString() + (killed ? "\nKilled" : ""),
            (int)hit.ImpactKind, killed ? 0.95f : 0.7f, killed ? 22 : 12);
    }

    public static void ButcherHit(Vector3 point, Vector3 normal, int remainingHits)
    {
        bool finished = remainingHits == 0;
        var color = finished ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.5f, 0.32f);
        Show(point, normal, color, color, finished ? "Harvested" : $"Butcher {3 - remainingHits}/3",
            finished ? 5 : 4, finished ? 1.2f : 0.8f, finished ? 30 : 16);
    }

    static void Show(Vector3 point, Vector3 normal, Color color, Color flecks,
        string text, int sound, float duration, int count)
    {
        if (instance == null)
            instance = new GameObject("Combat Impact Feedback").AddComponent<CombatImpactFeedback>();
        instance.Play(point, normal, color, flecks, text, sound, duration, count);
    }

    void Awake()
    {
        viewer = Camera.main;
        var material = Resources.Load<Material>("Combat Impact");
        particleMaterial = new Material(material);
        ringMaterial = new Material(material);
        particleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        particleTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            var distance = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            particleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - distance) * 4f)));
        }
        particleTexture.Apply(false, true);
        particleMaterial.SetTexture("_BaseMap", particleTexture);
        ringMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
        for (int i = 0; i < sounds.Length; i++) sounds[i] = CreateSound(i);
    }

    Impact CreateImpact()
    {
        var effect = new GameObject("Impact").transform;
        effect.SetParent(transform, false);
        var particles = effect.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.065f);
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.02f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;

        var ring = new GameObject("Impact Ring").AddComponent<LineRenderer>();
        ring.transform.SetParent(effect, false);
        ring.sharedMaterial = ringMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 16;
        ring.widthMultiplier = 0.012f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        var label = new GameObject("Hit Confirmation").AddComponent<TextMeshPro>();
        label.transform.SetParent(effect, false);
        label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        label.fontSize = 1.8f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        var audio = effect.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.minDistance = 1f;
        audio.maxDistance = 25f;
        audio.dopplerLevel = 0f;
        audio.volume = 0.45f;
        return new Impact { root = effect, particles = particles, ring = ring, label = label, audio = audio };
    }

    void Play(Vector3 point, Vector3 normal, Color color, Color flecks,
        string text, int sound, float duration, int count)
    {
        var impact = impacts[nextImpact] ??= CreateImpact();
        nextImpact = (nextImpact + 1) % PoolSize;
        impact.root.gameObject.SetActive(true);
        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        impact.root.SetPositionAndRotation(point + normal * 0.03f, Quaternion.LookRotation(normal));
        impact.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = impact.particles.main;
        main.startColor = flecks;
        impact.particles.Play();
        impact.particles.Emit(count);
        impact.label.text = text;
        impact.label.fontSize = sound >= 4 ? 1.2f : 1.8f;
        impact.label.color = color;
        impact.labelStart = point + normal * 0.08f + Vector3.up * 0.12f;
        impact.color = color;
        impact.startedAt = Time.time;
        impact.duration = duration;
        impact.audio.Stop();
        impact.audio.PlayOneShot(sounds[sound]);
        if (viewer == null) viewer = Camera.main;
        impact.label.transform.localScale = Vector3.one * (viewer != null
            ? Mathf.Clamp(Vector3.Distance(viewer.transform.position, point) / 3f, 1f, 4f) : 1f);
        UpdateImpact(impact);
    }

    void LateUpdate()
    {
        foreach (var impact in impacts)
            if (impact != null && impact.root.gameObject.activeSelf) UpdateImpact(impact);
    }

    void UpdateImpact(Impact impact)
    {
        float age = Time.time - impact.startedAt;
        if (age >= impact.duration) { impact.root.gameObject.SetActive(false); return; }
        impact.label.transform.position = impact.labelStart + Vector3.up * (age * 0.18f);
        var textColor = impact.color;
        textColor.a = Mathf.Clamp01((impact.duration - age) / 0.2f);
        impact.label.color = textColor;
        impact.ring.enabled = age < 0.28f;
        if (impact.ring.enabled)
        {
            float radius = Mathf.Lerp(0.025f, 0.12f, age / 0.28f);
            var ringColor = impact.color;
            ringColor.a = 1f - age / 0.28f;
            impact.ring.startColor = impact.ring.endColor = ringColor;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16;
                impact.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }
        if (viewer == null) return;
        var facing = Quaternion.LookRotation(impact.root.position - viewer.transform.position);
        impact.ring.transform.rotation = facing;
        impact.label.transform.rotation = facing;
    }

    static AudioClip CreateSound(int style)
    {
        const int rate = 22050;
        float duration = style == 5 ? 0.22f : 0.1f;
        var samples = new float[Mathf.CeilToInt(rate * duration)];
        var noise = new System.Random(831 + style);
        float filteredNoise = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            filteredNoise = Mathf.Lerp(filteredNoise, (float)noise.NextDouble() * 2f - 1f, 0.3f);
            float tone = Mathf.Sin(2f * Mathf.PI * (style == 5 ? (t < 0.1f ? 660f : 880f) : 95f + style * 35f) * t);
            float envelope = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * (style == 5 ? 12f : 38f));
            samples[i] = (style == 5 ? tone * 0.4f : tone * 0.45f + filteredNoise * 0.55f) * envelope;
        }
        var clip = AudioClip.Create("Combat Impact " + style, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void OnDestroy()
    {
        Destroy(particleMaterial);
        Destroy(ringMaterial);
        Destroy(particleTexture);
        foreach (var clip in sounds) if (clip != null) Destroy(clip);
    }
}
