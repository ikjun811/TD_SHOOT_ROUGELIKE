using UnityEngine;
using JUTPS.WeaponSystem;

// 래퍼가 필요한 작동·특수 모듈의 무기별 실행 상태 (PlayerStatAndWeaponSync가 JU 무기 오브젝트에 붙이고 동기화마다 Configure)
// JU Weapon은 매 프레임 CurrentFireRateToShoot를 Fire_Rate와 비교해 사격하고 쏠 때 OnShot을 호출 → Fire_Rate를 실시간 조절
//   3점사(UMP): 점사 안 간격 0.05초, 점사 사이 0.25초, 쏠 때마다 분산 초기화 (피해 +15%는 모듈 damageMultiplier)
//   연사 예열(UMP): 계속 쏘면 간격 0.13초 → 1.5초에 걸쳐 0.05초, 0.3초 쉬면 초기화
//   거치 사격(Dual): 0.3초 이상 제자리면 연사 +50%, 이동 중 −15%
//   빠른 장전(P226): 장전 직후 3발 피해 +30% (장전 시간 −40%는 모듈 reloadTimeOverride) → BulletWeaponLink가 탄 생성 시 배율 적용
//   빈 탄창 급속 장전(Dual): 0발에서 시작한 장전은 시간 x0.55
//   재장전 시 폭발(특수): 장전 시작 시 플레이어 주변 4m에 사격 1회 피해 x 배율
// 간격은 모두 연사 스탯 적용 (fireRateFactor로 나눔) + 외부 연사 배율(탈출 집중) 적용. 장전 시작·완료는 PlayerStatAndWeaponSync가 감지해 호출
public class WeaponModuleRuntime : MonoBehaviour
{
    private const float BurstInterval = 0.05f;
    private const float BurstPause = 0.25f;
    private const int BurstShots = 3;
    private const float SpinStartInterval = 0.13f;
    private const float SpinMinInterval = 0.05f;
    private const float SpinUpTime = 1.5f;
    private const float SpinResetDelay = 0.3f;
    private const float StationaryDelay = 0.3f;
    private const float StationaryBonus = 1.5f;
    private const float MovingPenalty = 0.85f;
    private const float MoveSpeedThreshold = 0.3f;
    private const int PostReloadShots = 3;
    private const float PostReloadDamage = 1.3f;
    private const float EmptyReloadMultiplier = 0.55f;
    private const float ReloadExplosionRadius = 4f;
    private const float ReloadExplosionVfxScale = 0.6f;

    private Weapon weapon;
    private Rigidbody playerRb;
    private GameObject owner;
    private float baseInterval;
    private float fireRateFactor = 1f;
    private ModuleEffect fireModeEffect;
    private bool quickReload;
    private bool emptyQuickReload;
    private float reloadExplosionRatio;
    private float perShotDamage;

    private int burstCount;
    private float lastShotTime = -10f;
    private float spinTime;
    private float stillTime;
    private int postReloadShotsLeft;

    // 외부 연사 배율 (CharacterModuleRuntime: 탈출 집중 구역 안 1.2~1.4, 1 = 없음). 모든 무기에 공통
    public static float ExternalFireRateMultiplier = 1f;
    private float appliedExternal = 1f; // 사격 방식 래퍼가 없는 무기에 마지막으로 적용한 값

    private float Factor => fireRateFactor * ExternalFireRateMultiplier;

    // 사격 1회 피해 (산탄은 산탄 합, 공격력·작동 모듈 반영). 재장전 시 폭발·위기 반격(CharacterModuleRuntime) 기준
    public float PerShotDamage => perShotDamage;

    // 이번 장전의 시간 배율 (PlayerStatAndWeaponSync.ApplyReloadAnimationSpeed가 사용)
    public float CurrentReloadMultiplier { get; private set; } = 1f;

    // 이번 사격 탄의 피해 배율 (BulletWeaponLink가 탄 생성 시 사용, 산탄은 같은 사격의 산탄 모두 동일)
    public float ShotDamageMultiplier => postReloadShotsLeft > 0 ? PostReloadDamage : 1f;

    public void Configure(Weapon weapon, float baseInterval, float fireRateFactor, ModuleEffect fireModeEffect,
        bool quickReload, bool emptyQuickReload, float reloadExplosionRatio, float perShotDamage, Rigidbody playerRb, GameObject owner)
    {
        if (this.weapon != weapon)
        {
            if (this.weapon != null) this.weapon.OnShot.RemoveListener(OnShot);
            this.weapon = weapon;
            if (isActiveAndEnabled && weapon != null) weapon.OnShot.AddListener(OnShot);
        }

        this.baseInterval = baseInterval;
        this.fireRateFactor = Mathf.Max(0.01f, fireRateFactor);
        this.fireModeEffect = fireModeEffect;
        this.quickReload = quickReload;
        this.emptyQuickReload = emptyQuickReload;
        this.reloadExplosionRatio = reloadExplosionRatio;
        this.perShotDamage = perShotDamage;
        this.playerRb = playerRb;
        this.owner = owner;

        burstCount = 0;
        spinTime = 0f;
        stillTime = 0f;
        postReloadShotsLeft = 0;
        CurrentReloadMultiplier = 1f;
        ApplyFireRate();
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.OnShot.AddListener(OnShot);
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.OnShot.RemoveListener(OnShot);
    }

    // 플레이어 무기 사격 알림 (총구 섬광 PlayerFlashlight 등) — 모든 장착 무기에 이 래퍼가 붙어 있음
    public static event System.Action<Weapon> OnAnyPlayerShot;

    private void OnShot()
    {
        OnAnyPlayerShot?.Invoke(weapon);
        lastShotTime = Time.time;
        if (postReloadShotsLeft > 0) postReloadShotsLeft--;

        if (fireModeEffect == ModuleEffect.BurstFire)
        {
            burstCount++;
            weapon.ShotErrorProbability = 0f; // 점사 중 분산 없음
            if (burstCount >= BurstShots)
            {
                burstCount = 0;
                weapon.Fire_Rate = BurstPause / Factor;
            }
            else
            {
                weapon.Fire_Rate = BurstInterval / Factor;
            }
        }
    }

    private void Update()
    {
        if (weapon == null) return;

        switch (fireModeEffect)
        {
            case ModuleEffect.BurstFire:
                // 점사 도중 손을 떼면 다음 점사는 처음부터
                if (burstCount > 0 && Time.time - lastShotTime > BurstPause)
                {
                    burstCount = 0;
                    weapon.Fire_Rate = BurstInterval / Factor;
                }
                break;

            case ModuleEffect.SpinUp:
                if (Time.time - lastShotTime < SpinResetDelay) spinTime = Mathf.Min(SpinUpTime, spinTime + Time.deltaTime);
                else spinTime = 0f;
                weapon.Fire_Rate = Mathf.Lerp(SpinStartInterval, SpinMinInterval, spinTime / SpinUpTime) / Factor;
                break;

            case ModuleEffect.StationaryFire:
                bool moving = false;
                if (playerRb != null)
                {
                    Vector3 velocity = playerRb.linearVelocity;
                    velocity.y = 0f;
                    moving = velocity.magnitude > MoveSpeedThreshold;
                }
                stillTime = moving ? 0f : stillTime + Time.deltaTime;
                float multiplier = moving ? MovingPenalty : (stillTime >= StationaryDelay ? StationaryBonus : 1f);
                weapon.Fire_Rate = baseInterval / (multiplier * ExternalFireRateMultiplier);
                break;

            default:
                // 사격 방식 래퍼가 없으면 외부 배율이 바뀔 때만 다시 적용
                if (!Mathf.Approximately(appliedExternal, ExternalFireRateMultiplier)) ApplyFireRate();
                break;
        }
    }

    private void ApplyFireRate()
    {
        if (weapon == null) return;
        switch (fireModeEffect)
        {
            case ModuleEffect.BurstFire: weapon.Fire_Rate = BurstInterval / Factor; break;
            case ModuleEffect.SpinUp: weapon.Fire_Rate = SpinStartInterval / Factor; break;
            case ModuleEffect.StationaryFire: break; // 매 프레임 Update에서 계산
            default:
                weapon.Fire_Rate = baseInterval / ExternalFireRateMultiplier; // baseInterval = Sync가 계산한 기본 간격 (연사 스탯 포함)
                appliedExternal = ExternalFireRateMultiplier;
                break;
        }
    }

    // 장전 시작 (PlayerStatAndWeaponSync가 장전 감지 시 호출). triggerExplosion: 쌍권총 양손 중 한 번만 폭발
    public void OnReloadStarted(bool triggerExplosion)
    {
        CurrentReloadMultiplier = emptyQuickReload && weapon != null && weapon.BulletsAmounts <= 0 ? EmptyReloadMultiplier : 1f;

        if (triggerExplosion && reloadExplosionRatio > 0f && owner != null)
        {
            BulletHitEffects.ExplodeAt(owner.transform.position + Vector3.up, ReloadExplosionRadius,
                perShotDamage * reloadExplosionRatio, null, owner, ReloadExplosionVfxScale);
        }
    }

    public void OnReloadFinished()
    {
        CurrentReloadMultiplier = 1f;
        if (quickReload) postReloadShotsLeft = PostReloadShots;
    }
}
