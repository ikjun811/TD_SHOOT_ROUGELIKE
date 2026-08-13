using UnityEngine;
using JUTPSActions;
using UnityEngine.InputSystem;

namespace JUTPS.ActionScripts
{
    [AddComponentMenu("JU TPS/Third Person System/Additionals/Aim On Mouse Position")]
    public class AimOnMousePosition : JUTPSAction
    {
        [HideInInspector] public static Vector3 AimPosition;

        [Header("Settings")]
        public bool Enabled = true;
        public float NormalOffset = 0.1f;
        public bool PreventResetingAimPosition;

        [Header("Top Down Flat Aim Settings ⭐")]
        [Tooltip("체크 시 마우스 위치와 관계없이 조준 높이가 가슴 높이로 고정되어 수평 사격됩니다.")]
        public bool LockToChestHeight = true;
        public float CustomChestHeightOffset = 1.2f;

        [Header("Two Dimensional Settings")]
        public bool TwoDimensional;

        void Update()
        {
            if (Enabled == false || cam == null)
            {
                AimPosition = Vector3.zero;
                TPSCharacter.LookAtPosition = AimPosition;
                return;
            }

            Vector2 mousePosition = Vector2.zero;
            if (Mouse.current != null)
            {
                mousePosition = Mouse.current.position.value;
            }

            if (TwoDimensional)
            {
                // 2D 모드 로직
                Ray MouseRay = cam.ScreenPointToRay(mousePosition);
                Vector3 pivotPosition = transform.position;
                pivotPosition.y = TPSCharacter.HumanoidSpine.position.y;

                Vector3 MousePosition = MouseRay.origin + MouseRay.direction * Vector3.Distance(pivotPosition, MouseRay.origin);
                MousePosition.z = transform.position.z;

                Vector3 mousePosNoHeight = MousePosition; mousePosNoHeight.y = pivotPosition.y;
                float HorizontalDistance = Vector3.Distance(pivotPosition, mousePosNoHeight);

                MousePosition.z = Mathf.Lerp(TPSCharacter.transform.position.z - 3f, pivotPosition.z, HorizontalDistance);
                AimPosition = Vector3.Lerp(AimPosition, MousePosition, 10 * Time.deltaTime);

                Debug.DrawLine(pivotPosition, AimPosition, Color.red);
            }
            else
            {
                //  3D 물리 콜라이더(상자, 벽, 몬스터)를 치지 않는 무한 가상 평면 연산
                float chestY = (TPSCharacter != null && TPSCharacter.HumanoidSpine != null)
                    ? TPSCharacter.HumanoidSpine.position.y
                    : transform.position.y + CustomChestHeightOffset;

                // 플레이어 가슴 높이에 위치한 무한 수평 평면 생성
                Plane aimPlane = new Plane(Vector3.up, new Vector3(0, chestY, 0));
                Ray ray = cam.ScreenPointToRay(mousePosition);

                // 평면과의 무한 교점 계산 (장애물에 전혀 방해받지 않음)
                if (aimPlane.Raycast(ray, out float enterDistance))
                {
                    Vector3 targetPoint = ray.GetPoint(enterDistance);
                    AimPosition = Vector3.Lerp(AimPosition, targetPoint, 25 * Time.deltaTime);
                }
            }

            TPSCharacter.LookAtPosition = AimPosition;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            if (TwoDimensional)
            {
                Gizmos.DrawWireCube(AimPosition, new Vector3(0.1f, 0.1f, 0));
                Gizmos.DrawWireCube(AimPosition, new Vector3(0.5f, 0.5f, 0));
            }
            else
            {
                Gizmos.DrawWireCube(AimPosition, new Vector3(0.1f, 0, 0.1f));
                Gizmos.DrawWireCube(AimPosition, new Vector3(0.5f, 0, 0.5f));
            }
        }
    }
}