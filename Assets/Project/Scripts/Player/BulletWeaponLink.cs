using UnityEngine;
using JUTPS.WeaponSystem;

// 탄 생성 시 무기 래퍼(WeaponModuleRuntime)의 사격 피해 배율 적용 (빠른 장전: 장전 직후 3발 +30%)
// PlayerStatAndWeaponSync가 해당 모듈이 있을 때만 무기별 런타임 탄 템플릿에 붙임
// JU는 탄 생성 후 OnShot을 호출하므로, 생성 순간(Awake)의 배율이 이번 사격 값 (산탄은 같은 사격의 산탄 모두 동일)
[RequireComponent(typeof(Bullet))]
public class BulletWeaponLink : MonoBehaviour
{
    public Weapon sourceWeapon;

    private void Awake()
    {
        if (sourceWeapon == null) return;
        WeaponModuleRuntime runtime = sourceWeapon.GetComponent<WeaponModuleRuntime>();
        if (runtime == null) return;

        GetComponent<Bullet>().BulletDamage *= runtime.ShotDamageMultiplier;
    }
}
