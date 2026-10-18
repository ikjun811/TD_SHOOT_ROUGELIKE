using UnityEngine;
using JUTPS.WeaponSystem;

// 탑다운 탄 수평화 (PlayerStatAndWeaponSync가 모든 플레이어 탄 템플릿에 붙임)
// JU는 총구 → 척추 높이 조준점으로 쏘므로 총구가 더 높으면 탄이 아래로 기울어 바닥에 박힘 (커서가 가까울수록 심함)
// 또 JU 분산은 방향의 x·y·z 모두에 무작위 값을 더해 위아래로도 퍼짐 (탑다운에선 "향해 쐈는데 안 맞음"으로 보임)
// → 생성 직후 진행 방향의 위아래 성분을 없애고 물리 직진으로 바꿈: 총구 높이 수평 비행, 분산은 좌우만 남음
// Weapon이 Instantiate 후에 FinalPoint를 넣으므로 Awake가 아닌 Start에서 처리 (첫 물리 이동 전)
[RequireComponent(typeof(Bullet))]
public class FlatBullet : MonoBehaviour
{
    private void Start()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(direction.normalized);

        // 조준점(FinalPoint)을 향하는 계산 이동 대신 정면 직진 (JU Physics 이동은 매 물리 프레임 forward x 속도)
        Bullet bullet = GetComponent<Bullet>();
        bullet.FinalPoint = Vector3.zero;
        bullet.MovementType = Bullet.BulletMovementType.Physics;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = transform.forward * bullet.BulletVelocity;
        }
    }
}
