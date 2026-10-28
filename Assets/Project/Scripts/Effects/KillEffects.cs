using UnityEngine;

// 처치 연출 (B4) — GameplayBootstrap이 생성, EnemyBase.OnAnyEnemyDeath 구독 (처치 원인 무관)
//   코드 파티클 (이펙트 에셋을 구하면 교체): 피 튐(방울이 위로 튀었다 떨어짐) + 피 안개(짧은 연무) + 바닥 핏자국(서서히 사라짐)
//   엘리트는 eliteScale배, 봄버는 폭발로 사라지므로 제외. 게임 시간 기준 (엘리트 히트 스톱 중 함께 느려짐)
//   템플릿을 한 번 만들어 두고(비활성) 처치마다 복제 → splatLifetime 뒤 제거
//   재질은 Sprites/Default + 코드로 만든 텍스처 (빌드에 항상 포함되는 셰이더)
public class KillEffects : MonoBehaviour
{
    public static KillEffects Instance { get; private set; }

    public Color bloodColor = new Color(0.55f, 0.03f, 0.03f);
    public Color bloodDarkColor = new Color(0.22f, 0f, 0f);
    public int dropletCount = 22;
    public float splatLifetime = 6f;
    public float eliteScale = 1.5f;

    private GameObject template;
    private Texture2D dotTexture;
    private Texture2D blobTexture;
    private Material dotMaterial;
    private Material blobMaterial;

    public static KillEffects EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("KillEffects");
        return go.AddComponent<KillEffects>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
        if (template != null) Destroy(template);
        if (dotMaterial != null) Destroy(dotMaterial);
        if (blobMaterial != null) Destroy(blobMaterial);
        if (dotTexture != null) Destroy(dotTexture);
        if (blobTexture != null) Destroy(blobTexture);
        Instance = null;
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        if (enemy == null || enemy.Type == EnemyType.Bomber) return;
        Spawn(enemy.transform.position, enemy.IsElite ? eliteScale : 1f);
    }

    public void Spawn(Vector3 position, float scale)
    {
        if (template == null) BuildTemplate();

        Vector3 ground = position;
        ground.y = 0f;
        GameObject effect = Instantiate(template, ground, Quaternion.identity);
        effect.transform.localScale = Vector3.one * scale;
        effect.SetActive(true);
        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        Destroy(effect, splatLifetime + 0.5f);
    }

    #region 템플릿

    private void BuildTemplate()
    {
        dotTexture = CreateDotTexture();
        blobTexture = CreateBlobTexture();
        Shader shader = Shader.Find("Sprites/Default");
        dotMaterial = new Material(shader) { name = "KillDot", mainTexture = dotTexture, hideFlags = HideFlags.DontSave };
        blobMaterial = new Material(shader) { name = "KillBlob", mainTexture = blobTexture, hideFlags = HideFlags.DontSave };

        template = new GameObject("KillEffectTemplate");
        template.SetActive(false);
        template.transform.SetParent(transform, false); // 이 오브젝트와 함께 씬에서 정리

        BuildSpray(template.transform);
        BuildMist(template.transform);
        BuildSplat(template.transform);
    }

    // 피 튐: 몸통 높이에서 위쪽 원뿔로 튀어 중력으로 떨어짐
    private void BuildSpray(Transform parent)
    {
        ParticleSystem ps = CreateSystem("Spray", parent, new Vector3(0f, 1.1f, 0f), dotMaterial);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        main.gravityModifier = 2.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(bloodColor, bloodDarkColor);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)dropletCount) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 위쪽으로

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
    }

    // 피 안개: 짧게 부풀며 사라지는 붉은 연무
    private void BuildMist(Transform parent)
    {
        ParticleSystem ps = CreateSystem("Mist", parent, new Vector3(0f, 1.1f, 0f), dotMaterial);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        Color mist = bloodColor;
        mist.a = 0.45f;
        main.startColor = mist;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.7f));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FadeGradient(0f);
    }

    // 바닥 핏자국: 바닥에 누운 얼룩, 마지막 30% 동안 서서히 사라짐
    private void BuildSplat(Transform parent)
    {
        ParticleSystem ps = CreateSystem("Splat", parent, new Vector3(0f, 0.03f, 0f), blobMaterial);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = splatLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 1.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Color splat = bloodDarkColor;
        splat.a = 0.85f;
        main.startColor = splat;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 1) });

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.04f, 1f), new Keyframe(1f, 1f));
        size.size = new ParticleSystem.MinMaxCurve(1f, grow);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FadeGradient(0.7f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sortingFudge = 10f; // 피 튐·연무가 핏자국 위로 그려지게
    }

    private static ParticleSystem CreateSystem(string objName, Transform parent, Vector3 localPosition, Material material)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; // 엘리트 배율
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // fadeStart 이후 알파 1 → 0
    private static ParticleSystem.MinMaxGradient FadeGradient(float fadeStart)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    // 부드러운 원
    private static Texture2D CreateDotTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KillDot", wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                byte a = (byte)(Mathf.Clamp01((1f - d) * 2.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 불규칙한 얼룩: 가운데 큰 원 + 둘레의 작은 원 여러 개 (고정 시드라 매번 같은 모양, 회전·크기로 변화)
    private static Texture2D CreateBlobTexture()
    {
        const int size = 64;
        System.Random random = new System.Random(7);
        Vector3[] circles = new Vector3[10]; // x, y, 반지름 (0~1 좌표)
        circles[0] = new Vector3(0.5f, 0.5f, 0.26f);
        for (int i = 1; i < circles.Length; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float dist = 0.18f + (float)random.NextDouble() * 0.2f;
            float radius = 0.04f + (float)random.NextDouble() * 0.09f;
            circles[i] = new Vector3(0.5f + Mathf.Cos(angle) * dist, 0.5f + Mathf.Sin(angle) * dist, radius);
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KillBlob", wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float coverage = 0f;
                foreach (Vector3 c in circles)
                {
                    float d = Vector2.Distance(p, new Vector2(c.x, c.y));
                    coverage = Mathf.Max(coverage, Mathf.Clamp01((c.z - d) / 0.02f)); // 가장자리 2% 부드럽게
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
