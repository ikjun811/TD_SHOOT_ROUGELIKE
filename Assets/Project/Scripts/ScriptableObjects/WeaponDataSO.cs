using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "GameData/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string weaponName;
    public WeaponType weaponType;
    public RarityType rarity;
    public Sprite weaponIcon;
    public GameObject weaponPrefab; // 3D 월드 드랍 및 착용용 모델

    [Header("Base Stats")]
    public float baseDamage = 15f;
    public float fireRate = 0.15f;    // 발사 간격 (초)
    public int maxAmmo = 30;
    public float reloadTime = 2.0f;

    [Header("Traits (Max 2)")]
    public List<WeaponTrait> traits = new List<WeaponTrait>();

    [Header("Sub-Variant Unique Mechanics ⭐")]
    public bool innateRicochet = false;        // 기본 도탄 보유 (리볼버 등)
    public bool innatePiercing = false;        // 기본 적 관통 (저격총 등)
    public float ammoRecycleChance = 0f;       // 탄환 환수 확률 % (쌍권총 등)
    public float innateKnockbackForce = 0f;    // 기본 넉백 힘 (더블배럴 등)

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