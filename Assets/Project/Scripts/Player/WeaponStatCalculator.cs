using System.Collections.Generic;
using UnityEngine;
using JUTPS.WeaponSystem;

// 무기 최종 수치 계산 (U5) — 실제 적용(PlayerStatAndWeaponSync.ApplyWeaponData)과 툴팁(ItemTooltipUI)이 같은 계산을 쓰도록 분리
// 순서: WeaponDataSO 기본값 → 이 무기군 대상 작동 모듈 덮어쓰기 → 스탯 모듈 % (공격력·연사·치명타·장전 속도)
// 상황에 따라 바뀌는 효과(3점사·연사 예열·거치 사격·빠른 장전 후 강화 등)는 WeaponModuleRuntime이 실행 중에 처리 → 여기 수치에는 없음
public static class WeaponStatCalculator
{
    // 무기군 고유 특성 (사용자 채택 2026-10-31) — 저격총: 한 발이 크게 터지는 무기
    public const float SniperBaseCritChance = 0.15f;  // 기본 치명타 확률 (모듈 치명타에 더함)
    public const float SniperCritMultiplier = 3.5f;   // 치명타 피해 배율 (다른 무기는 PlayerStatAndWeaponSync.critDamageMultiplier 2배)
    public const int SniperBasePierce = 1;            // 탄 궤적 모듈(관통·도탄)이 없을 때 기본 관통 횟수

    // 권총: 누르고 있으면 연사하는 최소 간격 (W2)
    public const float PistolAutoInterval = 0.15f;

    public static float BaseCritChance(WeaponType type) => type == WeaponType.Sniper ? SniperBaseCritChance : 0f;
    public static float CritMultiplier(WeaponType type, float defaultMultiplier) => type == WeaponType.Sniper ? SniperCritMultiplier : defaultMultiplier;

    // JU 무기 프리팹의 원래 값 (작동 모듈이 바꾸지 않을 때 그대로 쓰는 값)
    public struct BaseValues
    {
        public Weapon.WeaponFireMode fireMode;
        public bool continuousUse;
        public int pelletCount;
        public float spread;
    }

    public struct Result
    {
        public float damage;             // 탄 1발 최종 피해 (공격력 % 포함)
        public int pellets;              // 사격 1회 탄 수 (산탄총이 아니면 1)
        public float fireIntervalBeforeStat; // 작동 모듈까지 반영한 발사 간격 (연사 % 전)
        public float fireRateFactor;     // 1 + 연사 %
        public float FireInterval => fireIntervalBeforeStat / fireRateFactor;
        public int magazine;
        public float reloadTime;         // 작동 모듈까지 반영한 장전 모션 길이 (장전 속도 % 전, 실제 적용은 애니메이터 속도로)
        public float reloadSpeedPercent;
        public float FinalReloadTime => reloadTime / (1f + reloadSpeedPercent);
        public float attackPercent;
        public float critChance;         // 0~1

        public Weapon.WeaponFireMode fireMode;
        public bool continuousUse;
        public int pelletCountSetting;   // JU NumberOfShotgunBulletsPerShot에 넣을 값
        public float spread;

        public ModuleEffect fireModeEffect; // 실행 중 발사 간격을 조절하는 사격 방식 모듈 (3점사·연사 예열·거치 사격)
        public bool quickReload;
        public bool emptyQuickReload;
        public List<ModuleDataSO> mechanisms; // 반영된 작동 모듈

        public float PerShotDamage => damage * pellets;
    }

    public static Result Calculate(WeaponDataSO data, BaseValues baseValues, List<GridInventorySystem.PlacedModule> placed)
    {
        Result r = new Result
        {
            magazine = data.maxAmmo,
            reloadTime = data.reloadTime,
            fireIntervalBeforeStat = data.fireRate,
            fireMode = baseValues.fireMode,
            continuousUse = baseValues.continuousUse,
            pelletCountSetting = baseValues.pelletCount,
            spread = baseValues.spread,
            fireModeEffect = ModuleEffect.None,
            mechanisms = new List<ModuleDataSO>()
        };

        float damageMultiplier = 1f;
        float attackPercent = 0f, fireRatePercent = 0f, critChance = 0f, reloadSpeedPercent = 0f;

        if (placed != null)
        {
            foreach (var pm in placed)
            {
                ModuleDataSO m = pm.moduleData;
                if (m == null) continue;

                // 스탯 모듈 (중첩됨)
                if (m.category == ModuleCategory.Stat)
                {
                    float value = m.GetFinalStatValue();
                    switch (m.statType)
                    {
                        case ModuleStatType.AttackDamagePercent: attackPercent += value; break;
                        case ModuleStatType.FireRatePercent: fireRatePercent += value; break;
                        case ModuleStatType.CriticalChance: critChance += value; break;
                        case ModuleStatType.ReloadSpeedPercent: reloadSpeedPercent += value; break;
                    }
                    continue;
                }

                // 이 무기군 대상 작동 모듈: SO 기본값 덮어쓰기 (같은 칸은 충돌 규칙으로 1개만 배치됨)
                if (m.category != ModuleCategory.Mechanism || !m.TargetsWeapon(data.weaponType)) continue;

                if (m.magazineOverride > 0) r.magazine = m.magazineOverride;
                if (m.reloadTimeOverride > 0f) r.reloadTime = m.reloadTimeOverride;
                if (m.fireIntervalOverride > 0f) r.fireIntervalBeforeStat = m.fireIntervalOverride;
                if (m.damageMultiplier > 0f) damageMultiplier *= m.damageMultiplier;
                if (m.pelletCountOverride > 0) r.pelletCountSetting = m.pelletCountOverride;
                if (m.spreadOverride > 0f) r.spread = m.spreadOverride;
                if (m.fireModeOverride == FireModeOverride.Auto)
                {
                    r.fireMode = Weapon.WeaponFireMode.Auto;
                    r.continuousUse = true;
                }
                else if (m.fireModeOverride == FireModeOverride.SemiAuto)
                {
                    r.fireMode = Weapon.WeaponFireMode.SemiAuto;
                    r.continuousUse = false;
                }
                if (m.effect == ModuleEffect.BurstFire || m.effect == ModuleEffect.SpinUp || m.effect == ModuleEffect.StationaryFire) r.fireModeEffect = m.effect;
                if (m.effect == ModuleEffect.QuickReload) r.quickReload = true;
                if (m.effect == ModuleEffect.EmptyQuickReload) r.emptyQuickReload = true;
                r.mechanisms.Add(m);
            }
        }

        // 권총 자동 연사 필수 (W2, 사용자 2026-10-31 — 클릭 피로 제거): 반자동 단일 권총은 누르고 있으면 연사
        //   간격은 최소 PistolAutoInterval (리볼버 개조 0.35초처럼 더 느리면 그대로), 기관권총 개조는 이미 자동
        if (data.weaponType == WeaponType.Pistol && r.fireMode == Weapon.WeaponFireMode.SemiAuto)
        {
            r.fireMode = Weapon.WeaponFireMode.Auto;
            r.continuousUse = true;
            r.fireIntervalBeforeStat = Mathf.Max(r.fireIntervalBeforeStat, PistolAutoInterval);
        }

        r.attackPercent = attackPercent;
        r.fireRateFactor = 1f + fireRatePercent;
        r.critChance = Mathf.Clamp01(critChance + BaseCritChance(data.weaponType));
        r.reloadSpeedPercent = reloadSpeedPercent;
        r.damage = data.GetFinalDamage() * damageMultiplier * (1f + attackPercent);
        r.pellets = r.fireMode == Weapon.WeaponFireMode.Shotgun ? Mathf.Max(1, r.pelletCountSetting) : 1;
        r.magazine = Mathf.Max(1, r.magazine);
        return r;
    }
}
