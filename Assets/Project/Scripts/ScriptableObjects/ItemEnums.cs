using UnityEngine;

// 4단계 레어도
public enum RarityType
{
    Common,     // 일반 (흰색)
    Rare,       // 희귀 (파란색)
    Elite,      // 엘리트 (보라색)
    Legendary   // 레전더리 (노란색)
}

// 무기 5종 (에셋 외형 기준, GDD 8-6 #6)
public enum WeaponType
{
    SMG,           // 기관단총 (UMP, 시작 무기)
    Pistol,        // 권총 (P226)
    DualPistols,   // 쌍권총 (P226 Dual)
    Shotgun,       // 산탄총 (Benelli M4)
    Sniper         // 저격총 (SNIPER M82)
}

// 무기군별 고유 특성 (Affix)
// ※ 무기에서는 더 이상 사용하지 않음. 모듈 개편(작동/특수 모듈) 시 재정의 예정 (GDD 8-6 #7)
public enum WeaponTrait
{
    None,
    // 쌍권총/권총
    DoubleTap,          // 2연발 타격
    FastReload,         // 빠른 장전
    MobilityBonus,      // 이동 속도 증가
    // 자동소총
    Ricochet,           // 도탄 (벽/적 튕김)
    FireRateUp,         // 연사력 % 증가
    ExtraMissile,       // 사격 시 추가 미사일 유도 발사
    // 산탄총
    GuaranteedCrit,     // 확정 치명타
    IncendiaryRounds,   // 소이탄 (화염 지속 데미지)
    Knockback,          // 적 밀쳐내기
    // 저격총
    PiercingRounds,     // 적 일렬 관통
    ExecuteLowHealth,   // 체력 30% 이하 적 즉사/처형
    CritDamageBonus     // 치명타 데미지 극대화
}

// 테트리스 강화모듈 스탯 종류
public enum ModuleStatType
{
    AttackDamagePercent, // 공격력 % 증가
    MaxHealthBonus,      // 최대 체력 증가
    ArmorBonus,          // 방어력 증가
    FireRatePercent,     // 연사력 % 증가
    ReloadSpeedPercent,  // 장전 속도 % 증가
    CriticalChance,      // 치명타 확률 %
    MoveSpeedPercent,    // 이동 속도 % 증가
    SkillCooldownReduce  // 수류탄/스킬 쿨타임 감소 %
}

// 모듈 계열 (GDD 8-6 #17). 기본값 Stat → 기존 스탯 모듈 에셋은 그대로 스탯 계열
public enum ModuleCategory
{
    Stat,       // 스탯: 공용 수치 증가
    Mechanism,  // 작동: 무기·수류탄의 작동 방식 변경 (같은 대상·같은 칸 1개만)
    Special,    // 특수: 부가 효과 (중첩 없음)
    Unique      // 유니크: 레전더리 전용 교차 효과
}

// 모듈 적용 대상
public enum ModuleTarget
{
    All,          // 공용
    SMG,          // UMP
    Pistol,       // P226
    DualPistols,  // P226 Dual
    Shotgun,      // Benelli M4
    Sniper,       // M82
    Grenade,      // 수류탄
    Character     // 캐릭터
}

// 충돌 칸 (계열 무관, 대상이 겹치고 같은 칸이면 1개만 배치 가능)
public enum MechanismSlot
{
    None,
    FireMode,      // 사격 방식
    Magazine,      // 탄창 (반드시 장전 시간도 변경)
    Reload,        // 장전
    GrenadeThrow,  // 투척 방식
    BulletPath     // 탄 궤적 (관통·도탄 — 방향이 다른 선택지라 동시 불가)
}

// 작동 모듈의 사격 모드 변경 (JU WeaponFireMode에 대응, None = 원래 모드 유지)
public enum FireModeOverride
{
    None,
    Auto,      // 누르고 있으면 연사
    SemiAuto   // 클릭당 1발
}

// 모듈 효과 종류 (실제 효과는 A2~A5에서 단계별 구현)
public enum ModuleEffect
{
    None,
    // 작동·무기
    BurstFire,           // UMP 3점사
    SpinUp,              // UMP 연사 예열
    DrumMagazine,        // UMP 드럼 탄창
    MachinePistol,       // P226 기관권총 개조
    RevolverConversion,  // P226 리볼버 개조
    QuickReload,         // P226 빠른 장전
    StationaryFire,      // Dual 거치 사격
    ExtendedMagazine,    // Dual 확장 탄창
    EmptyQuickReload,    // Dual 빈 탄창 급속 장전
    SlugRounds,          // Benelli 슬러그탄
    DoubleBarrel,        // Benelli 더블배럴 개조
    ShellReload,         // Benelli 쉘 장전 (제외 후보)
    MarksmanConversion,  // M82 지정사수 개조
    LargeMagazine,       // M82 대형 탄창
    // 작동·수류탄
    AutoThrow,           // 자동 투척
    MultiThrow,          // 다중 투척
    // 특수·무기 공용
    Ricochet,            // 도탄
    Pierce,              // 관통
    Knockback,           // 넉백
    ImpactExplosion,     // 착탄 폭발
    Execute,             // 처형
    LastRoundBonus,      // 마지막 탄 추가 피해
    AmmoRefund,          // 탄환 환수
    ReloadExplosion,     // 재장전 시 폭발
    // 특수·수류탄
    ClusterGrenade,      // 집속탄
    ExtraCharge,         // 충전 +1
    KillCooldownRefund,  // 처치 시 쿨감
    // 특수·캐릭터
    DashBomb,            // 대시 폭탄
    Lifesteal,           // 흡혈
    ExtractionFocus,     // 탈출 집중
    LastStand,           // 위기 반격
    // 유니크
    ShotgunRicochet,     // 산탄총 도탄
    PistolMissile        // 권총 미사일 (제외 후보)
}

// 아이템 드래그 출처 구분
public enum DragSource
{
    LootList,   // 전리품 가방
    EquipSlot,  // 장착 무기 슬롯
    VaultSlot,  // 금고 보관함 슬롯
    Grid        // 그리드 배치 모듈
}