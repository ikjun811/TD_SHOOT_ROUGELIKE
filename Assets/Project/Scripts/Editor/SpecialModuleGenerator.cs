using System.IO;
using UnityEditor;
using UnityEngine;

// 특수 모듈 에셋 일괄 생성 (메뉴: Tools > TD Shoot > Generate Special Modules)
// A3a 도탄·관통 + A3b 넉백·착탄 폭발·처형 + A3c 마지막 탄·탄환 환수 + A4a 재장전 시 폭발 + A4b 수류탄 4종 + A4c 캐릭터 4종 x 레어도 4종 = 64개 (GDD 8-6 #17). 같은 이름의 에셋이 이미 있으면 건너뜀
// 칸 수 7/6/5/4 (일반/희귀/엘리트/레전더리), 레어도별 횟수는 effectCount, 효과 수치는 effectValue
public static class SpecialModuleGenerator
{
    private const string ParentFolder = "Assets/Project/Scripts/ScriptableObjects/Modules";
    private const string OutputFolder = ParentFolder + "/Special";

    private class Spec
    {
        public string fileKey;
        public string displayName;
        public ModuleEffect effect;
        public ModuleTarget target = ModuleTarget.All;
        public MechanismSlot slot = MechanismSlot.None;
        public int[] counts;            // 레어도별 effectCount (없으면 0)
        public float effectValue;       // 레어도 공통 effectValue
        public float[] values;          // 레어도별 effectValue (있으면 effectValue 대신)
        public string descriptionFormat; // {0} = effectCount, {1} = effectValue x 100 (%), {2} = effectValue
    }

    private static readonly Spec[] Specs =
    {
        new Spec { fileKey = "Ricochet", displayName = "도탄", effect = ModuleEffect.Ricochet, slot = MechanismSlot.BulletPath,
            counts = new[] { 1, 1, 2, 4 }, effectValue = 0.7f,
            descriptionFormat = "적에게 맞으면 12m 안의 다른 적에게 튕김 ({0}회, 튕길 때마다 피해 70%).\n관통과 함께 배치 불가. 산탄총에는 효과 없음." },
        new Spec { fileKey = "Pierce", displayName = "관통", effect = ModuleEffect.Pierce, slot = MechanismSlot.BulletPath,
            counts = new[] { 2, 3, 4, 6 }, effectValue = 1f, // 일렬 적이 있어야 하는 대신 도탄보다 강하게 (선택성)
            descriptionFormat = "탄이 적을 관통해 계속 날아감 ({0}회, 피해 감소 없음).\n도탄과 함께 배치 불가." },
        // A3b 적중 효과 (충돌 칸 없음 → 서로 함께 사용 가능, 같은 효과는 중복)
        new Spec { fileKey = "Knockback", displayName = "넉백", effect = ModuleEffect.Knockback,
            values = new[] { 0.35f, 0.45f, 0.55f, 0.75f },
            descriptionFormat = "맞은 적을 플레이어 반대 방향으로 {2:0.##}m 밀어냄.\n엘리트는 30%만 밀림." },
        new Spec { fileKey = "ImpactExplosion", displayName = "착탄 폭발", effect = ModuleEffect.ImpactExplosion,
            values = new[] { 0.15f, 0.2f, 0.25f, 0.35f },
            descriptionFormat = "적중 시 {1:0}% 확률로 반경 2.5m 폭발.\n주변 적에게 탄 피해의 50%." },
        new Spec { fileKey = "Execute", displayName = "처형", effect = ModuleEffect.Execute,
            values = new[] { 0.1f, 0.12f, 0.15f, 0.2f },
            descriptionFormat = "적중 후 남은 체력이 {1:0}% 이하면 즉사.\n엘리트는 기준 절반." },
        // A3c
        new Spec { fileKey = "LastRoundBonus", displayName = "마지막 탄 추가 피해", effect = ModuleEffect.LastRoundBonus,
            values = new[] { 0.5f, 0.7f, 0.9f, 1.2f },
            descriptionFormat = "탄창의 마지막 탄 피해 +{1:0}%.\n탄창이 작은 무기일수록 효과적 (산탄총은 마지막 사격의 산탄 전부)." },
        new Spec { fileKey = "AmmoRefund", displayName = "탄환 환수", effect = ModuleEffect.AmmoRefund,
            values = new[] { 0.1f, 0.15f, 0.2f, 0.25f },
            descriptionFormat = "탄으로 적 처치 시 탄창의 {1:0}%를 즉시 돌려받음 (최소 1발)." },
        // A4a (동작은 WeaponModuleRuntime)
        new Spec { fileKey = "ReloadExplosion", displayName = "재장전 시 폭발", effect = ModuleEffect.ReloadExplosion,
            values = new[] { 1.5f, 2f, 2.5f, 3.5f },
            descriptionFormat = "장전을 시작할 때 주변 4m에 충격파.\n피해: 사격 1회 피해의 {1:0}% (산탄총은 산탄 합)." },
        // A4b 수류탄 특수 (동작은 GrenadeSkill·GrenadeProjectile)
        new Spec { fileKey = "MultiThrow", displayName = "다중 투척", effect = ModuleEffect.MultiThrow, target = ModuleTarget.Grenade,
            counts = new[] { 2, 2, 3, 3 }, values = new[] { 0.6f, 0.65f, 0.55f, 0.6f },
            descriptionFormat = "한 번에 수류탄 {0}개를 좌우로 투척 (개당 피해 {1:0}%).\n자동 투척에도 적용." },
        new Spec { fileKey = "ClusterGrenade", displayName = "집속탄", effect = ModuleEffect.ClusterGrenade, target = ModuleTarget.Grenade,
            values = new[] { 0.3f, 0.35f, 0.4f, 0.5f },
            descriptionFormat = "폭발 시 주변 3m에 소형 폭탄 4개 (반경 2m, 피해 {1:0}%)." },
        new Spec { fileKey = "ExtraCharge", displayName = "충전 +1", effect = ModuleEffect.ExtraCharge, target = ModuleTarget.Grenade,
            counts = new[] { 1, 1, 1, 2 },
            descriptionFormat = "수류탄 최대 충전 +{0}.\n자동 투척과 함께 쓰면 효과 없음." },
        new Spec { fileKey = "KillCooldownRefund", displayName = "처치 시 쿨감", effect = ModuleEffect.KillCooldownRefund, target = ModuleTarget.Grenade,
            values = new[] { 0.5f, 0.7f, 0.9f, 1.2f },
            descriptionFormat = "적 처치 시 수류탄 충전 대기 시간 −{2:0.#}초.\n자동 투척과 함께 쓰면 효과 없음." },
        // A4c 캐릭터 특수 (동작은 CharacterModuleRuntime)
        new Spec { fileKey = "Lifesteal", displayName = "흡혈", effect = ModuleEffect.Lifesteal, target = ModuleTarget.Character,
            counts = new[] { 15, 20, 25, 30 }, values = new[] { 0.005f, 0.007f, 0.0085f, 0.01f }, // count = 라운드 회복 상한 %
            descriptionFormat = "적 처치 시 최대 체력의 {1:0.##}% 회복.\n라운드당 최대 체력의 {0}%까지." },
        new Spec { fileKey = "DashBomb", displayName = "대시 폭탄", effect = ModuleEffect.DashBomb, target = ModuleTarget.Character,
            values = new[] { 0.5f, 0.6f, 0.7f, 0.9f },
            descriptionFormat = "대시를 시작한 자리에 폭탄을 남김.\n0.5초 뒤 반경 3m, 수류탄 피해의 {1:0}% (내부 쿨 4초)." },
        new Spec { fileKey = "ExtractionFocus", displayName = "탈출 집중", effect = ModuleEffect.ExtractionFocus, target = ModuleTarget.Character,
            counts = new[] { 15, 20, 25, 30 }, values = new[] { 0.2f, 0.25f, 0.3f, 0.4f }, // count = 받는 피해 감소 %
            descriptionFormat = "탈출 구역 안에 있는 동안\n연사 +{1:0}%, 받는 피해 −{0}%." },
        new Spec { fileKey = "LastStand", displayName = "위기 반격", effect = ModuleEffect.LastStand, target = ModuleTarget.Character,
            values = new[] { 1.5f, 2f, 2.5f, 3.5f },
            descriptionFormat = "체력 30% 이하에서 피격 시 주변 4m에 충격파.\n사격 1회 피해의 {1:0}% + 넉백 2m (내부 쿨 12초)." },
    };

    private static readonly string[] RarityNames = { "일반", "희귀", "엘리트", "레전더리" };
    private static readonly int[] CellCounts = { 7, 6, 5, 4 }; // 일반/희귀/엘리트/레전더리

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
            case 7:
                return variant == 0
                    ? new Shape(3, 3, new[] { T, T, T, T, T, T, F, T, F })   // 3x3 - 아래 두 모서리
                    : new Shape(3, 3, new[] { T, T, F, T, T, T, F, T, T });  // 3x3 - 대각 두 모서리
            case 6:
                return variant == 0
                    ? new Shape(2, 3, new[] { T, T, T, T, T, T })            // 2x3
                    : new Shape(3, 2, new[] { T, T, T, T, T, T });           // 3x2
            case 5:
                return variant == 0
                    ? new Shape(2, 3, new[] { T, T, T, T, T, F })            // P자
                    : new Shape(3, 2, new[] { T, F, T, T, T, T });           // U자
            default:
                return variant == 0
                    ? new Shape(3, 2, new[] { T, T, T, F, T, F })            // T자
                    : new Shape(2, 2, new[] { T, T, T, T });                 // 정사각형
        }
    }

    [MenuItem("Tools/TD Shoot/Generate Special Modules")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(ParentFolder))
        {
            AssetDatabase.CreateFolder("Assets/Project/Scripts/ScriptableObjects", "Modules");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(ParentFolder, "Special");
        }

        int created = 0;
        int skipped = 0;

        for (int i = 0; i < Specs.Length; i++)
        {
            Spec spec = Specs[i];
            foreach (RarityType rarity in System.Enum.GetValues(typeof(RarityType)))
            {
                string path = $"{OutputFolder}/Module_Special_{spec.fileKey}_{rarity}.asset";
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                int count = spec.counts != null ? spec.counts[(int)rarity] : 0;
                float value = spec.values != null ? spec.values[(int)rarity] : spec.effectValue;
                Shape shape = GetShape(CellCounts[(int)rarity], i % 2);

                ModuleDataSO module = ScriptableObject.CreateInstance<ModuleDataSO>();
                module.moduleName = $"{spec.displayName} ({RarityNames[(int)rarity]})";
                module.rarity = rarity;
                module.category = ModuleCategory.Special;
                module.target = spec.target;
                module.mechanismSlot = spec.slot;
                module.effect = spec.effect;
                module.effectCount = count;
                module.effectValue = value;
                module.description = string.Format(spec.descriptionFormat, count, value * 100f, value);
                module.width = shape.width;
                module.height = shape.height;
                module.shapeGrid = (bool[])shape.cells.Clone();
                module.statValue = 0f;

                AssetDatabase.CreateAsset(module, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SpecialModuleGenerator] 생성 {created}개, 건너뜀 {skipped}개 -> {OutputFolder}");
    }
}
