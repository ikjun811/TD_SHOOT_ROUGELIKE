using UnityEngine;

// 무기 데이터 (기본 수치의 기준값, 동기화 시 JU 무기에 적용)
// 연사 방식·장전 특징·특수 능력은 무기가 아닌 모듈이 담당 (GDD 8-6 #6, #7)
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "GameData/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string weaponName;
    public WeaponType weaponType;
    public RarityType rarity;
    public Sprite weaponIcon;
    public GameObject weaponPrefab; // 3D 월드 드랍 및 착용용 모델

    // 작동 모듈 조합별 아이콘 (Tools > TD Shoot > Generate Weapon Icons가 생성, 직접 그린 아이콘으로 교체 가능)
    [System.Serializable]
    public class IconVariant
    {
        public ModuleEffect[] effects;
        public Sprite icon;
    }
    public IconVariant[] iconVariants = new IconVariant[0];

    // 장착된 작동 모듈 효과에 맞는 아이콘: 효과가 모두 장착된 변형 중 효과 수가 가장 많은 것, 없으면 기본 아이콘
    // coveredCount = 아이콘 모양에 반영된 효과 수 (나머지 작동 효과는 슬롯 구석 작동 표시로)
    public Sprite GetIcon(System.Collections.Generic.ICollection<ModuleEffect> activeEffects, out int coveredCount)
    {
        Sprite best = weaponIcon;
        int bestCount = 0;
        if (iconVariants != null && activeEffects != null)
        {
            foreach (IconVariant variant in iconVariants)
            {
                if (variant == null || variant.icon == null || variant.effects == null || variant.effects.Length <= bestCount) continue;
                bool allActive = true;
                foreach (ModuleEffect e in variant.effects)
                {
                    if (!activeEffects.Contains(e)) { allActive = false; break; }
                }
                if (!allActive) continue;
                best = variant.icon;
                bestCount = variant.effects.Length;
            }
        }

        coveredCount = bestCount;
        return best;
    }

    [Header("JU Link")]
    [Tooltip("플레이어 JUInventory의 무기 ItemName과 정확히 일치해야 함 (예: UMP, P226, P226 Dual, Benelli M4, M82)")]
    public string juItemName;

    [Header("Base Stats")]
    public float baseDamage = 15f;
    public float fireRate = 0.15f;    // 발사 간격 (초)
    public int maxAmmo = 30;
    public float reloadTime = 2.0f;   // 장전 모션 전체 길이 (초). PlayerStatAndWeaponSync가 장전 중 애니메이터 속도로 적용

    // 레어도에 따른 최종 피해 (레어도는 기본 피해 배율에만 적용, 계수는 임시값 GDD 8-2)
    public float GetFinalDamage()
    {
        float multiplier = rarity switch
        {
            RarityType.Common => 1.0f,
            RarityType.Rare => 1.3f,
            RarityType.Elite => 1.7f,
            RarityType.Legendary => 2.5f,
            _ => 1.0f
        };

        return baseDamage * multiplier;
    }

    // 레어도 색상 (UI 테마에서, Resources/UITheme)
    public Color GetRarityColor()
    {
        return UITheme.Current.GetRarityColor(rarity);
    }
}