using System.Collections;
using UnityEngine;
using JUTPS;
using JUTPS.CameraSystems;

// 카메라가 마우스 방향으로 부드럽게 시야를 앞당김 (플레이어에 부착, JU 카메라 코드 비수정)
// JU TDCameraController의 TargetToFollow를 "플레이어 + 마우스 방향 오프셋" 위치의 프록시로 교체
// 오프셋은 화면 중앙 대비 마우스 위치 비율로 계산해, 카메라 이동이 다시 오프셋에 영향을 주지 않음
[RequireComponent(typeof(JUCharacterController))]
public class CameraLookAhead : MonoBehaviour
{
    [Header("Look Ahead")]
    [SerializeField] private float maxOffset = 4f;           // 화면 끝에 마우스가 있을 때 최대 이동 거리 (m)
    [Range(0f, 0.5f)][SerializeField] private float deadZone = 0.1f; // 화면 중앙 근처 무시 비율
    [SerializeField] private float smoothTime = 0.25f;       // 클수록 더 천천히 따라감

    private JUCharacterController controller;
    private TDCameraController cameraController;
    private Transform originalTarget;
    private Transform lookTarget;

    private Vector3 currentOffset;
    private Vector3 offsetVelocity;

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
    }

    private IEnumerator Start()
    {
        // JU 카메라가 Start에서 기본 대상(척추 뼈)을 정한 뒤에 교체
        yield return null;

        cameraController = FindFirstObjectByType<TDCameraController>();
        if (cameraController == null || cameraController.TargetToFollow == null)
        {
            Debug.LogWarning("CameraLookAhead: TDCameraController 또는 추적 대상을 찾지 못했습니다.");
            yield break;
        }

        originalTarget = cameraController.TargetToFollow;
        lookTarget = new GameObject("CameraLookTarget").transform;
        lookTarget.position = originalTarget.position;
        cameraController.TargetToFollow = lookTarget;
    }

    private void Update()
    {
        if (lookTarget == null || originalTarget == null) return;

        Vector3 targetOffset = controller.IsDead ? Vector3.zero : GetMouseOffset();
        currentOffset = Vector3.SmoothDamp(currentOffset, targetOffset, ref offsetVelocity, smoothTime);

        lookTarget.position = originalTarget.position + currentOffset;
    }

    // 화면 중앙 기준 마우스 위치(-1~1)를 카메라 기준 수평 방향 오프셋으로 변환
    private Vector3 GetMouseOffset()
    {
        Camera cam = Camera.main;
        if (cam == null || Screen.width <= 0 || Screen.height <= 0) return Vector3.zero;

        Vector3 mouse = Input.mousePosition;
        Vector2 v = new Vector2(mouse.x / Screen.width * 2f - 1f, mouse.y / Screen.height * 2f - 1f);
        float magnitude = Mathf.Min(v.magnitude, 1f);
        if (magnitude <= deadZone) return Vector3.zero;

        // 데드존 바깥을 0~1로 다시 매핑
        float strength = (magnitude - deadZone) / (1f - deadZone);
        Vector2 dir = v.normalized * strength;

        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.Cross(right, Vector3.up); // 화면 위쪽에 해당하는 수평 방향

        return (right * dir.x + forward * dir.y) * maxOffset;
    }

    private void OnDisable()
    {
        // 원래 추적 대상으로 복귀
        if (cameraController != null && originalTarget != null) cameraController.TargetToFollow = originalTarget;
    }

    private void OnEnable()
    {
        if (cameraController != null && lookTarget != null) cameraController.TargetToFollow = lookTarget;
    }

    private void OnDestroy()
    {
        if (lookTarget != null) Destroy(lookTarget.gameObject);
    }
}
