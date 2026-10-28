using UnityEngine;
using UnityEngine.Rendering;
using JU;

// 카메라 흔들림 (GameplayBootstrap이 생성, JU 카메라 비수정)
//   충격값(trauma, 0~1)을 쌓고 실제 시간 기준으로 줄임, 흔들림 크기 = trauma² (작은 충격은 거의 안 흔들리고 큰 충격만 확실히)
//   메인 카메라를 그리기 직전(beginCameraRendering)에만 화면 평면 위치·기울기를 흔들고 그린 뒤 원래대로 되돌림
//     → JU 카메라 추적·CameraLookAhead·마우스 조준(ScreenPointToRay)에는 영향 없음
//   실제 시간 기준이라 히트 스톱(엘리트 처치) 중에도 흔들림
//   계기: 폭발(BulletHitEffects·GrenadeProjectile, 크기 비례·거리 감쇄) / 플레이어 피격 / 엘리트 처치
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("흔들림")]
    public float maxOffset = 1f;        // trauma 1일 때 위치 흔들림 (m)
    public float maxRoll = 1.5f;        // trauma 1일 때 기울기 (도)
    public float frequency = 22f;       // 노이즈 속도
    public float decayPerSecond = 1.6f; // trauma 감소 속도

    [Header("계기별 충격값")]
    public float playerHitTrauma = 0.4f;
    public float eliteKillTrauma = 0.75f;
    public float explosionFalloffRadius = 25f; // 이 거리 이상 떨어진 폭발은 흔들지 않음

    private float trauma;
    private float seed;
    private Transform player;

    private Camera shakenCamera;
    private Vector3 savedPosition;
    private Quaternion savedRotation;

    public static CameraShake EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("CameraShake");
        return go.AddComponent<CameraShake>();
    }

    public static void AddTrauma(float amount)
    {
        if (Instance == null || amount <= 0f) return;
        Instance.trauma = Mathf.Clamp01(Instance.trauma + amount);
    }

    // 위치가 있는 충격 (폭발) — 플레이어와 거리가 멀수록 약하게
    public static void AddTrauma(float amount, Vector3 position)
    {
        if (Instance == null) return;
        float falloff = 1f;
        if (Instance.player != null && Instance.explosionFalloffRadius > 0f)
        {
            Vector3 d = position - Instance.player.position;
            d.y = 0f;
            falloff = Mathf.Clamp01(1f - d.magnitude / Instance.explosionFalloffRadius);
        }
        AddTrauma(amount * falloff);
    }

    // 폭발 이펙트 크기(vfxScale)를 충격값으로 — 착탄 폭발 0.3 → 0.21(거의 안 흔들림), 수류탄 0.5 → 0.35, 봄버 0.75 → 0.53
    public static void AddExplosion(Vector3 position, float vfxScale)
    {
        AddTrauma(Mathf.Clamp(vfxScale * 0.7f, 0.1f, 0.6f), position);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        seed = Random.Range(0f, 100f);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        IHealth.OnAnyDamaged += HandleAnyDamaged;
        EnemyBase.OnAnyEnemyDeath += HandleEnemyDeath;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        IHealth.OnAnyDamaged -= HandleAnyDamaged;
        EnemyBase.OnAnyEnemyDeath -= HandleEnemyDeath;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        Restore();
        Instance = null;
    }

    private void HandleAnyDamaged(IHealth.DamageResultInfo result)
    {
        Component target = result.DamagedObject as Component;
        if (target == null || result.RealDamage <= 0f) return;
        if (target.CompareTag("Player")) AddTrauma(playerHitTrauma);
    }

    private void HandleEnemyDeath(EnemyBase enemy)
    {
        if (enemy != null && enemy.IsElite) AddTrauma(eliteKillTrauma);
    }

    private void Update()
    {
        if (trauma > 0f) trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (trauma <= 0f || Time.timeScale <= 0f) return; // 정비·결과 화면(timeScale 0)에서는 흔들지 않음
        if (cam != Camera.main) return;

        float shake = trauma * trauma;
        float t = Time.unscaledTime * frequency;
        float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float y = Mathf.PerlinNoise(seed + 10f, t) * 2f - 1f;
        float roll = Mathf.PerlinNoise(seed + 20f, t) * 2f - 1f;

        Transform camTransform = cam.transform;
        shakenCamera = cam;
        savedPosition = camTransform.position;
        savedRotation = camTransform.rotation;

        camTransform.position = savedPosition + (camTransform.right * x + camTransform.up * y) * (maxOffset * shake);
        camTransform.rotation = savedRotation * Quaternion.Euler(0f, 0f, roll * maxRoll * shake);
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == shakenCamera) Restore();
    }

    private void Restore()
    {
        if (shakenCamera == null) return;
        shakenCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
        shakenCamera = null;
    }
}
