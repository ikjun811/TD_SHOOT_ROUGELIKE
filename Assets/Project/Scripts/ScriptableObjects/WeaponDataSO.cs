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

    // 레어도에 따른 테두리 색상 반환 유틸리티
    public Color GetRarityColor()
    {
        return rarity switch
        {
            RarityType.Common => Color.white,
            RarityType.Rare => new Color(0.2f, 0.6f, 1f),       // 파란색
            RarityType.Elite => new Color(0.7f, 0.3f, 0.9f),     // 보라색
            RarityType.Legendary => new Color(1f, 0.8f, 0.1f),   // 노란색
            _ => Color.white
        };
    }
}