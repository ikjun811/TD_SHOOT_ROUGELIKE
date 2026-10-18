using UnityEngine;

[CreateAssetMenu(fileName = "NewModuleData", menuName = "GameData/Module Data")]
public class ModuleDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string moduleName;
    public RarityType rarity;
    public Sprite moduleIcon;
    [Tooltip("사용하지 않음 — category(Unique)로 대체")]
    public bool isUniqueModule; // 유니크 전용 모듈 여부 (구버전 필드, category 사용)

    [Header("Module Type (GDD 8-6 #17)")]
    public ModuleCategory category = ModuleCategory.Stat;
    public ModuleTarget target = ModuleTarget.All;
    [Tooltip("충돌 칸: 대상이 겹치고 같은 칸이면 1개만 배치 가능 (계열 무관)")]
    public MechanismSlot mechanismSlot = MechanismSlot.None;
    public ModuleEffect effect = ModuleEffect.None;
    [Tooltip("효과 보조 수치 (배율·확률 등, 효과마다 의미가 다름)")]
    public float effectValue;
    [Tooltip("효과 보조 횟수 (튕김 수·발 수 등)")]
    public int effectCount;
    [TextArea(2, 4)]
    public string description;

    [Header("Weapon Override (작동 모듈, 0 = 변경 없음)")]
    [Tooltip("탄창 크기")]
    public int magazineOverride;
    [Tooltip("장전 시간 (초)")]
    public float reloadTimeOverride;
    [Tooltip("발사 간격 (초, 연사 스탯 적용 전)")]
    public float fireIntervalOverride;
    [Tooltip("피해 배율 (1 = 변경 없음, 공격력 스탯 적용 전)")]
    public float damageMultiplier = 1f;
    [Tooltip("산탄 수 (산탄총)")]
    public int pelletCountOverride;
    [Tooltip("산탄 퍼짐 (JU LossOfAccuracyPerShot)")]
    public float spreadOverride;
    public FireModeOverride fireModeOverride = FireModeOverride.None;

    [Header("Tetris Shape Settings")]
    [Tooltip("그리드 모양 (예: 2x2, 3x1 일자 등) 가로/세로 칸 수")]
    public int width = 2;
    public int height = 2;

    [Tooltip("모듈 모양 배열 (true: 칸 있음, false: 빈 공간)")]
    // 1차원 배열로 저장 후 2D로 사용 (인스펙터 편의성)
    public bool[] shapeGrid = new bool[4] { true, true, true, true };

    [Header("Stat Boost")]
    public ModuleStatType statType;
    public float statValue; // 예: 0.15 (+15% 공격력) 또는 +20 (체력)

    // 레어도에 따른 계수 보정 연산
    public float GetFinalStatValue()
    {
        float multiplier = rarity switch
        {
            RarityType.Common => 1.0f,
            RarityType.Rare => 1.3f,
            RarityType.Elite => 1.7f,
            RarityType.Legendary => 2.5f,
            _ => 1.0f
        };

        return statValue * multiplier;
    }

    // 충돌: 계열 무관, 같은 칸, 대상이 같거나 한쪽이 공용 (작동 모듈의 사격 방식·탄창 등, 특수 모듈의 탄 궤적)
    public bool ConflictsWith(ModuleDataSO other)
    {
        if (other == null) return false;
        if (mechanismSlot == MechanismSlot.None || mechanismSlot != other.mechanismSlot) return false;
        return target == other.target || target == ModuleTarget.All || other.target == ModuleTarget.All;
    }

    // 이 모듈이 해당 무기군을 대상으로 하는지 (공용 제외, 무기 전용 대상만)
    public bool TargetsWeapon(WeaponType weaponType)
    {
        return target switch
        {
            ModuleTarget.SMG => weaponType == WeaponType.SMG,
            ModuleTarget.Pistol => weaponType == WeaponType.Pistol,
            ModuleTarget.DualPistols => weaponType == WeaponType.DualPistols,
            ModuleTarget.Shotgun => weaponType == WeaponType.Shotgun,
            ModuleTarget.Sniper => weaponType == WeaponType.Sniper,
            _ => false
        };
    }

    // 같은 특수·유니크 효과인지 (중첩 없음 → 2개째부터 효과 없음)
    public bool IsSameEffect(ModuleDataSO other)
    {
        if (other == null || effect == ModuleEffect.None) return false;
        bool stackless = category == ModuleCategory.Special || category == ModuleCategory.Unique;
        return stackless && other.category == category && other.effect == effect;
    }
}
