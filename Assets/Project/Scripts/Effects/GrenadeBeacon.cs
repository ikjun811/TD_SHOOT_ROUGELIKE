using UnityEngine;

// 수류탄 시인성 (사용자 채택 2026-10-29: B 점멸등 + D 비행 궤적) — 수류탄·대시 폭탄 루트에 부착
//   점멸등: 빨간 포인트 라이트 + 블룸되는 작은 빛 점이 폭발이 가까울수록 빠르게 깜빡임 (fuseTime 기준, 게임 시간)
//   궤적: Rigidbody가 있고 날아가는 동안만 짧은 빛 꼬리 (착지·정지하면 멈춤)
public class GrenadeBeacon : MonoBehaviour
{
    public static Color lightColor = new Color(1f, 0.15f, 0.08f);
    public static Color dotColor = new Color(4f, 0.5f, 0.25f, 1f);   // HDR → 블룸
    public static Color trailColor = new Color(1f, 0.55f, 0.3f, 0.7f);
    public static float lightRange = 3f;
    public static float lightOn = 4f;
    public static float lightOff = 0.4f;
    public static float blinkStart = 3f;    // 초당 깜빡임 (처음)
    public static float blinkEnd = 12f;     // 초당 깜빡임 (폭발 직전)
    public static float dotSize = 0.09f;
    public static float trailTime = 0.25f;
    public static float trailWidth = 0.12f;
    public static float trailMinSpeed = 1.5f;

    private const string DotShader = "TDShoot/HitFlash";
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static Material dotMaterial;
    private static Material trailMaterial;

    private float fuseTime = 1f;
    private float elapsed;
    private float phase;
    private Light beacon;
    private GameObject dot;
    private TrailRenderer trail;
    private Rigidbody body;
    private float dotHeight;

    // dotHeight: 빛 점 위치 (모델 윗면 근처)
    public static GrenadeBeacon Attach(GameObject target, float fuseTime, float dotHeight)
    {
        GrenadeBeacon b = target.AddComponent<GrenadeBeacon>();
        b.fuseTime = Mathf.Max(0.05f, fuseTime);
        b.Build(dotHeight);
        return b;
    }

    // 수류탄 fuseTime이 생성 뒤에 정해지는 경우
    public void SetFuse(float time) => fuseTime = Mathf.Max(0.05f, time);

    private void Build(float height)
    {
        dotHeight = height;
        GameObject lightObj = new GameObject("BeaconLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * (dotHeight + 0.15f);
        beacon = lightObj.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = lightColor;
        beacon.range = lightRange;
        beacon.shadows = LightShadows.None;

        dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "BeaconDot";
        Destroy(dot.GetComponent<Collider>());
        dot.transform.SetParent(transform, false);
        dot.transform.localPosition = Vector3.up * dotHeight;
        dot.transform.localScale = Vector3.one * dotSize / Mathf.Max(0.0001f, transform.lossyScale.x);
        Renderer rend = dot.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material mat = GetDotMaterial();
        if (mat != null) rend.sharedMaterial = mat;

        body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.minVertexDistance = 0.05f;
            Material tm = GetTrailMaterial();
            if (tm != null) trail.sharedMaterial = tm;
        }
        Apply(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / fuseTime);
        phase += Time.deltaTime * Mathf.Lerp(blinkStart, blinkEnd, t * t);
        Apply(Mathf.Repeat(phase, 1f) < 0.5f);

        // 수류탄이 굴러도 빛 점은 항상 위쪽 (월드 기준)
        Vector3 top = transform.position + Vector3.up * dotHeight;
        if (dot != null) dot.transform.position = top;
        if (beacon != null) beacon.transform.position = top + Vector3.up * 0.15f;

        if (trail != null) trail.emitting = body != null && body.linearVelocity.sqrMagnitude > trailMinSpeed * trailMinSpeed;
    }

    private void Apply(bool on)
    {
        if (beacon != null) beacon.intensity = on ? lightOn : lightOff;
        if (dot != null) dot.SetActive(on);
    }

    private static Material GetDotMaterial()
    {
        if (dotMaterial != null) return dotMaterial;
        Shader shader = Shader.Find(DotShader);
        if (shader == null) return null;
        dotMaterial = new Material(shader) { name = "GrenadeBeaconDot", hideFlags = HideFlags.DontSave };
        dotMaterial.SetColor(FlashColorId, dotColor);
        return dotMaterial;
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null) return trailMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        trailMaterial = new Material(shader) { name = "GrenadeBeaconTrail", hideFlags = HideFlags.DontSave };
        return trailMaterial;
    }
}
