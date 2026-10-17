using UnityEngine;
using JUTPS.WeaponSystem;

// 치명타 판정 (PlayerStatAndWeaponSync가 무기별 런타임 탄 템플릿에 붙임)
// JU가 탄을 복제(Instantiate)하는 즉시 Awake에서 확률 판정 → 명중 전에 BulletDamage를 배율만큼 올림
// 산탄총은 탄알마다 따로 판정
[RequireComponent(typeof(Bullet))]
public class CriticalBullet : MonoBehaviour
{
    public float critChance = 0f;        // 0.05 = 5%
    public float critMultiplier = 2f;    // 치명타 피해 배율

    public bool IsCritical { get; private set; }

    private void Awake()
    {
        if (Random.value >= critChance) return;

        Bullet bullet = GetComponent<Bullet>();
        bullet.BulletDamage *= critMultiplier;
        IsCritical = true;
    }
}
