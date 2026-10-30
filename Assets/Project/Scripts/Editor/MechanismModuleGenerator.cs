using System.IO;
using UnityEditor;
using UnityEngine;

// 작동 모듈 에셋 일괄 생성 (메뉴: Tools > TD Shoot > Generate Mechanism Modules)
// A2 JU 무기 값만 바꾸는 작동 모듈 8종 + A4a 래퍼 작동 모듈 5종 + A4b 자동 투척 = 14종 x 엘리트 1종 = 14개 (GDD 8-6 #17). 같은 이름의 에셋이 이미 있으면 건너뜀
// 효과는 레어도와 무관하게 같으므로 엘리트(4칸) 1종만 운영 (A5a, 2026-10-19). 다른 레어도가 뽑혀도 이 판본이 드랍됨
// 수치는 생성 후 에셋에서 직접 조정 가능 (PlayerStatAndWeaponSync.ApplyWeaponData가 적용)
public static class MechanismModuleGenerator
{
    private const string ParentFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";
    private const string OutputFolder = ParentFolder + "/Mechanism";

    private class Spec
    {
        public string fileKey;
        public string displayName;
        public ModuleEffect effect;
        public ModuleTarget target;
        public MechanismSlot slot;
        public string description;
        public int magazine;
        public float reloadTime;
        public float fireInterval;
        public float damageMultiplier = 1f;
        public int pelletCount;
        public float spread;
        public FireModeOverride fireMode = FireModeOverride.None;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { fileKey = "DrumMagazine", displayName = "드럼 탄창", effect = ModuleEffect.DrumMagazine,
            target = ModuleTarget.SMG, slot = MechanismSlot.Magazine, magazine = 76, reloadTime = 2.9f,
            description = "탄창 38→76발.\n대가: 장전 시간 1.8→2.9초." },
        new Spec { fileKey = "MachinePistol", displayName = "기관권총 개조", effect = ModuleEffect.MachinePistol,
            target = ModuleTarget.Pistol, slot = MechanismSlot.FireMode, fireMode = FireModeOverride.Auto, fireInterval = 0.06f, damageMultiplier = 0.8f,
            description = "누르고 있으면 연사, 발사 간격 0.1→0.06초.\n대가: 발당 피해 −20%." },
        new Spec { fileKey = "RevolverConversion", displayName = "리볼버 개조", effect = ModuleEffect.RevolverConversion,
            target = ModuleTarget.Pistol, slot = MechanismSlot.FireMode, magazine = 6, reloadTime = 1.5f, fireInterval = 0.35f, damageMultiplier = 3f, // 2026-10-31: 2.2 → 3 + 적중 충전 0.3초 (권총 자동 연사 후 장점 보강)
            description = "대구경 6연발, 발당 피해 ×3, 적중당 수류탄 충전 +0.3초.\n대가: 탄창 16→6발, 발사 간격 0.35초, 장전 1.2→1.5초." },
        new Spec { fileKey = "ExtendedMagazine", displayName = "확장 탄창", effect = ModuleEffect.ExtendedMagazine,
            target = ModuleTarget.DualPistols, slot = MechanismSlot.Magazine, magazine = 28, reloadTime = 2.7f,
            description = "탄창 16→28발.\n대가: 장전 시간 1.8→2.7초." },
        new Spec { fileKey = "SlugRounds", displayName = "슬러그탄", effect = ModuleEffect.SlugRounds,
            target = ModuleTarget.Shotgun, slot = MechanismSlot.FireMode, pelletCount = 1, spread = 0.005f, damageMultiplier = 6f,
            description = "산탄 대신 단일 대형 탄, 피해 ×6, 퍼짐 없음.\n대가: 근거리 산탄 범위 상실." },
        new Spec { fileKey = "DoubleBarrel", displayName = "더블배럴 개조", effect = ModuleEffect.DoubleBarrel,
            target = ModuleTarget.Shotgun, slot = MechanismSlot.Magazine, magazine = 2, fireInterval = 0.3f, reloadTime = 1.0f, damageMultiplier = 0.8f,
            description = "2연발 속사 (발사 간격 0.3초), 장전 1.0초.\n대가: 탄창 2발, 발당 피해 −20%." },
        new Spec { fileKey = "MarksmanConversion", displayName = "지정사수 개조", effect = ModuleEffect.MarksmanConversion,
            target = ModuleTarget.Sniper, slot = MechanismSlot.FireMode, fireMode = FireModeOverride.Auto, fireInterval = 0.45f, magazine = 12, damageMultiplier = 0.55f, // Auto: M82 기본 BoltAction의 볼트 동작 제거
            description = "발사 간격 1.2→0.45초, 탄창 7→12발.\n대가: 발당 피해 −45%." },
        new Spec { fileKey = "LargeMagazine", displayName = "대형 탄창", effect = ModuleEffect.LargeMagazine,
            target = ModuleTarget.Sniper, slot = MechanismSlot.Magazine, magazine = 14, reloadTime = 4.5f,
            description = "탄창 7→14발.\n대가: 장전 시간 3.0→4.5초." },
        // A4a 래퍼 모듈 (동작은 WeaponModuleRuntime, 수치는 그 상수)
        new Spec { fileKey = "BurstFire", displayName = "3점사", effect = ModuleEffect.BurstFire,
            target = ModuleTarget.SMG, slot = MechanismSlot.FireMode, damageMultiplier = 1.15f,
            description = "3발 점사 (점사 안 간격 0.05초), 점사 중 분산 없음, 피해 +15%.\n대가: 점사 사이 0.25초 멈춤." },
        new Spec { fileKey = "SpinUp", displayName = "연사 예열", effect = ModuleEffect.SpinUp,
            target = ModuleTarget.SMG, slot = MechanismSlot.FireMode,
            description = "계속 쏘면 발사 간격 0.13→0.05초 (1.5초에 걸쳐).\n대가: 쏘기 시작할 때 느림, 0.3초 쉬면 초기화." },
        new Spec { fileKey = "QuickReload", displayName = "빠른 장전", effect = ModuleEffect.QuickReload,
            target = ModuleTarget.Pistol, slot = MechanismSlot.Reload, reloadTime = 0.72f,
            description = "장전 시간 1.2→0.72초 (−40%).\n장전 직후 3발 피해 +30%." },
        new Spec { fileKey = "StationaryFire", displayName = "거치 사격", effect = ModuleEffect.StationaryFire,
            target = ModuleTarget.DualPistols, slot = MechanismSlot.FireMode,
            description = "0.3초 이상 제자리에 서 있으면 연사 +50%.\n대가: 이동 중 연사 −15%." },
        // A4b 수류탄 작동 (동작은 GrenadeSkill)
        new Spec { fileKey = "AutoThrow", displayName = "자동 투척", effect = ModuleEffect.AutoThrow,
            target = ModuleTarget.Grenade, slot = MechanismSlot.GrenadeThrow,
            description = "내 탄이 적에게 맞으면 8% 확률로 그 적에게 수류탄 자동 투척 (내부 쿨 1초, 피해 60%, 충전 무관).\n대가: 직접 투척 불가. 충전 +1·처치 시 쿨감은 효과 없음." },
        new Spec { fileKey = "EmptyQuickReload", displayName = "빈 탄창 급속 장전", effect = ModuleEffect.EmptyQuickReload,
            target = ModuleTarget.DualPistols, slot = MechanismSlot.Reload,
            description = "탄창이 0발일 때 시작한 장전은 시간 −45% (1.8→1.0초).\nR키로 미리 하는 장전은 원래 속도." },
    };

    // 생성·운영하는 레어도 (Tools > TD Shoot > Remove Unused Module Rarities가 나머지를 정리)
    public static readonly RarityType[] Rarities = { RarityType.Elite };

    private static readonly string[] RarityNames = { "일반", "희귀", "엘리트", "레전더리" };
    private static readonly int[] CellCounts = { 5, 5, 4, 3 }; // 일반/희귀/엘리트/레전더리

    private struct Shape
    {
        public int width, height;
        public bool[] cells;
        public Shape(int width, int height, bool[] cells) { this.width = width; this.height = height; this.cells = cells; }
    }

    // 칸 수별 모양 2종 (모듈마다 번갈아 사용)
    private static Shape GetShape(int cellCount, int variant)
    {
        bool T = true, F = false;
        switch (cellCount)
        {
            case 5:
                return variant == 0
                    ? new Shape(2, 3, new[] { T, T, T, T, T, F })       // P자
                    : new Shape(3, 2, new[] { T, F, T, T, T, T });      // U자
            case 4:
                return variant == 0
                    ? new Shape(3, 2, new[] { T, T, T, F, T, F })       // T자
                    : new Shape(3, 2, new[] { T, F, F, T, T, T });      // L자
            default:
                return variant == 0
                    ? new Shape(2, 2, new[] { T, F, T, T })             // 작은 L자
                    : new Shape(3, 1, new[] { T, T, T });               // 일자
        }
    }

    [MenuItem("Tools/TD Shoot/Generate Mechanism Modules")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(ParentFolder))
        {
            AssetDatabase.CreateFolder("Assets/Project/Scripts/ScriptableObjects", "Modules");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(ParentFolder, "Mechanism");
        }

        int created = 0;
        int skipped = 0;

        for (int i = 0; i < Specs.Length; i++)
        {
            Spec spec = Specs[i];
            foreach (RarityType rarity in Rarities)
            {
                string path = $"{OutputFolder}/Module_Mech_{spec.fileKey}_{rarity}.asset";
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                Shape shape = GetShape(CellCounts[(int)rarity], i % 2);

                ModuleDataSO module = ScriptableObject.CreateInstance<ModuleDataSO>();
                module.moduleName = $"{spec.displayName} ({RarityNames[(int)rarity]})";
                module.rarity = rarity;
                module.category = ModuleCategory.Mechanism;
                module.target = spec.target;
                module.mechanismSlot = spec.slot;
                module.effect = spec.effect;
                module.description = spec.description;
                module.width = shape.width;
                module.height = shape.height;
                module.shapeGrid = (bool[])shape.cells.Clone();
                module.statValue = 0f;
                module.magazineOverride = spec.magazine;
                module.reloadTimeOverride = spec.reloadTime;
                module.fireIntervalOverride = spec.fireInterval;
                module.damageMultiplier = spec.damageMultiplier;
                module.pelletCountOverride = spec.pelletCount;
                module.spreadOverride = spec.spread;
                module.fireModeOverride = spec.fireMode;

                AssetDatabase.CreateAsset(module, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MechanismModuleGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
