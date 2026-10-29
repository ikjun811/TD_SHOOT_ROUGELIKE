using JU;
using JUTPS.WeaponSystem;
using UnityEngine;

// 손전등 + 총구 섬광 (몰입 제안 1, 사용자 채택 2026-10-29) — GameplayBootstrap이 플레이어에 부착 (JU 비수정)
//   손전등: 마우스가 가리키는 방향으로 머리 높이에서 앞쪽 바닥을 비추는 스포트라이트, 실시간 그림자(장애물 뒤가 어두워짐)
//     몸에 가려 생기는 그림자를 피하려고 광원은 몸보다 조금 앞. 사망·정비(timeScale 0) 중에는 끔
//   총구 섬광: 플레이어 무기가 쏠 때(WeaponModuleRuntime.OnAnyPlayerShot) 총구 위치에서 주황 포인트 라이트가 짧게 번쩍임 (그림자 없음)
public class PlayerFlashlight : MonoBehaviour
{
    [Header("손전등")]
    public Color lightColor = new Color(1f, 0.94f, 0.82f);
    public float intensity = 8f;
    public float range = 18f;
    public float spotAngle = 60f;
    public float innerSpotAngle = 32f;
    public float height = 1.7f;
    public float forwardOffset = 0.5f;   // 몸보다 앞 (자기 그림자 방지)
    public float aimDistance = 7f;       // 이 거리의 바닥을 향해 비춤 (빛 웅덩이가 앞쪽에 생기게)
    public float turnSpeed = 18f;        // 조준 방향을 따라가는 빠르기
    public LightShadows shadows = LightShadows.Soft;

    [Header("총구 섬광")]
    public Color flashColor = new Color(1f, 0.68f, 0.32f);
    public float flashIntensity = 7f;
    public float flashRange = 7f;
    public float flashTime = 0.06f;

    // 탈출 텔레포트로 플레이어 모습이 사라진 동안 손전등·섬광 끔 (ExtractionTeleportEffect)
    public static bool Suppressed;

    private Light flashlight;
    private Light muzzleFlash;
    private JUHealth health;
    private Vector3 aimDir = Vector3.forward;
    private float flashEndTime;

    public static void EnsureOnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<PlayerFlashlight>() == null) player.AddComponent<PlayerFlashlight>();
    }

    private void Awake()
    {
        Suppressed = false; // 씬 다시 시작 시 초기화
        health = GetComponent<JUHealth>();
        aimDir = Flat(transform.forward);

        // 손전등은 회전이 플레이어와 따로 놀아야 해서 부모 없이 두고 매 프레임 따라감
        GameObject go = new GameObject("PlayerFlashlight");
        flashlight = go.AddComponent<Light>();
        flashlight.type = LightType.Spot;
        flashlight.shadows = shadows;
        flashlight.shadowNearPlane = 0.3f;
        flashlight.shadowBias = 0.05f;

        GameObject flashObj = new GameObject("MuzzleFlashLight");
        muzzleFlash = flashObj.AddComponent<Light>();
        muzzleFlash.type = LightType.Point;
        muzzleFlash.shadows = LightShadows.None;
        muzzleFlash.enabled = false;

        WeaponModuleRuntime.OnAnyPlayerShot += HandleShot;
    }

    private void OnDestroy()
    {
        WeaponModuleRuntime.OnAnyPlayerShot -= HandleShot;
        if (flashlight != null) Destroy(flashlight.gameObject);
        if (muzzleFlash != null) Destroy(muzzleFlash.gameObject);
    }

    private void LateUpdate()
    {
        bool active = !Suppressed && Time.timeScale > 0f && (health == null || !health.IsDead);
        flashlight.enabled = active;
        if (!active)
        {
            muzzleFlash.enabled = false;
            return;
        }

        flashlight.color = lightColor;
        flashlight.intensity = intensity;
        flashlight.range = range;
        flashlight.spotAngle = spotAngle;
        flashlight.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
        flashlight.shadows = shadows;

        // 조준 방향 (마우스 → 발 높이 평면), 부드럽게 따라감 — 실제 시간 기준 (히트 스톱 중에도 반응)
        Vector3 target = GetAimDirection();
        aimDir = Vector3.Slerp(aimDir, target, 1f - Mathf.Exp(-turnSpeed * Time.unscaledDeltaTime));
        aimDir = Flat(aimDir);

        Vector3 origin = transform.position + Vector3.up * height + aimDir * forwardOffset;
        Vector3 floorPoint = transform.position + aimDir * aimDistance;
        flashlight.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(floorPoint - origin, Vector3.up));

        // 총구 섬광: 빠르게 사라짐
        if (muzzleFlash.enabled)
        {
            float t = 1f - (flashEndTime - Time.unscaledTime) / flashTime;
            if (t >= 1f) muzzleFlash.enabled = false;
            else muzzleFlash.intensity = flashIntensity * (1f - t) * (1f - t);
        }
    }

    private void HandleShot(Weapon weapon)
    {
        if (weapon == null || Time.timeScale <= 0f || Suppressed) return;
        Vector3 pos = weapon.Shoot_Position != null ? weapon.Shoot_Position.position : transform.position + Vector3.up * 1.2f + aimDir * 0.6f;
        muzzleFlash.transform.position = pos;
        muzzleFlash.color = flashColor;
        muzzleFlash.range = flashRange * Random.Range(0.85f, 1.15f);
        muzzleFlash.intensity = flashIntensity;
        muzzleFlash.enabled = true;
        flashEndTime = Time.unscaledTime + flashTime;
    }

    private Vector3 GetAimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null) return aimDir;
        Plane ground = new Plane(Vector3.up, transform.position);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!ground.Raycast(ray, out float enter)) return aimDir;
        Vector3 dir = Flat(ray.GetPoint(enter) - transform.position);
        return dir.sqrMagnitude > 0.0001f ? dir : aimDir;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
