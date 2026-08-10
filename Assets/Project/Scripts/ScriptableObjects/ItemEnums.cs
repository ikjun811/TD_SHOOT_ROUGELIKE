using UnityEngine;

// 4단계 레어도
public enum RarityType
{
    Common,     // 일반 (흰색)
    Rare,       // 희귀 (파란색)
    Elite,      // 엘리트 (보라색)
    Legendary   // 레전더리 (노란색)
}

// 무기군 종류
public enum WeaponType
{
    AssaultRifle, // 자동소총
    Shotgun,      // 산탄총
    Pistol        // 권총
}

// 무기 붙는 특성(Affix) 종류 (기획안 반영)
public enum WeaponTrait
{
    None,
    // 자동소총 전용/공용
    Ricochet,           // 도탄
    FireRateUp,         // 연사력 증가
    ExtraMissile,       // 추가 미사일
    // 산탄총 전용/공용
    GuaranteedCrit,     // 확정 치명타
    FastReload,         // 빠른 장전
    IncendiaryRounds    // 소이탄 (화염)
}

// 강화모듈 스탯 스태킹 종류
public enum ModuleStatType
{
    AttackDamagePercent, // 공격력 % 증가
    MaxHealthBonus,      // 최대 체력 증가
    ArmorBonus,          // 방어력 증가
    FireRatePercent,     // 연사력 % 증가
    ReloadSpeedPercent,  // 장전 속도 % 증가
    CriticalChance,      // 치명타 확률 %
    MoveSpeedPercent     // 이동 속도 % 증가
}