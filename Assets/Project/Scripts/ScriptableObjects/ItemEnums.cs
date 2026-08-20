using UnityEngine;

// 4단계 레어도
public enum RarityType
{
    Common,     // 일반 (흰색)
    Rare,       // 희귀 (파란색)
    Elite,      // 엘리트 (보라색)
    Legendary   // 레전더리 (노란색)
}

// 4대 무기군 (확정)
public enum WeaponType
{
    DualPistols,   // 쌍권총 (P226 Dual)
    AssaultRifle,  // 자동소총 (UMP5)
    Shotgun,       // 산탄총 (Benelli M4)
    SniperRifle    // 저격총 (SNIPER M82)
}

// 무기군별 고유 특성 (Affix)
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