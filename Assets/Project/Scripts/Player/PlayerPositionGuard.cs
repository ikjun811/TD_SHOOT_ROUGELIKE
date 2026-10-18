using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 플레이어 위치 안전장치 (전투 씬 로드 시 GameplayBootstrap이 플레이어에 자동 부착)
//   1) 벽 통과 방지: 물리 계산 직후마다 직전 안전 위치 → 현재 위치 사이를 Walls 레이어로 스피어캐스트, 벽을 넘었으면 벽 앞으로 되돌림
//      (JU 대시는 rb.MovePosition으로 순간 이동하듯 움직여 얇은 벽을 뚫을 수 있음. 적에게 밀려 뚫리는 경우도 같이 막음)
//   2) 추락 복구: 바닥 아래로 떨어지거나 BSP 맵 밖으로 나가면 가장 가까운 NavMesh 위치로 복귀
// 한 번에 teleportDistance 이상 움직이면 순간이동(라운드 시작 재배치 등)으로 보고 검사 없이 기준 위치만 갱신
public class PlayerPositionGuard : MonoBehaviour
{
    [Header("Wall")]
    [SerializeField] private string wallLayerName = "Walls";
    [SerializeField] private float castHeight = 1f;      // 스피어캐스트 높이 (장애물 3m·외곽 벽 4m보다 낮게)
    [SerializeField] private float castRadius = 0.25f;   // 플레이어 캡슐보다 약간 작게
    [SerializeField] private float wallSkin = 0.05f;
    [SerializeField] private float teleportDistance = 8f;

    [Header("Fall")]
    [SerializeField] private float fallHeight = -3f;
    [SerializeField] private float recoverSearchRadius = 10f;

    private Rigidbody rb;
    private int wallMask;
    private Vector3 lastSafePosition;
    private BspArenaGenerator arena;

    // GameplayBootstrap이 전투 씬 로드마다 호출
    public static void EnsureOnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<PlayerPositionGuard>() == null)
        {
            player.AddComponent<PlayerPositionGuard>();
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wallMask = LayerMask.GetMask(wallLayerName);
        lastSafePosition = transform.position;
    }

    private void OnEnable()
    {
        lastSafePosition = transform.position;
        StartCoroutine(AfterPhysicsRoutine());
    }

    private void Start()
    {
        arena = FindFirstObjectByType<BspArenaGenerator>();
    }

    // WaitForFixedUpdate: 물리 계산(이동·충돌 해소)이 끝난 직후 실행
    private IEnumerator AfterPhysicsRoutine()
    {
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (true)
        {
            yield return wait;
            CheckPosition();
        }
    }

    private void CheckPosition()
    {
        if (rb == null || rb.isKinematic) return;

        Vector3 current = rb.position;
        Vector3 horizontal = current - lastSafePosition;
        horizontal.y = 0f;
        float distance = horizontal.magnitude;

        if (distance > teleportDistance)
        {
            lastSafePosition = current;
            return;
        }

        if (distance > 0.001f)
        {
            Vector3 direction = horizontal / distance;
            Vector3 origin = lastSafePosition + Vector3.up * castHeight;
            if (Physics.SphereCast(origin, castRadius, direction, out RaycastHit hit, distance, wallMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 corrected = lastSafePosition + direction * Mathf.Max(0f, hit.distance - wallSkin);
                corrected.y = current.y;
                SetPosition(corrected, false);
                current = corrected;
            }
        }

        bool outsideArena = arena != null && arena.isActiveAndEnabled && arena.GetZoneIndexAt(current) < 0;
        if (current.y < fallHeight || outsideArena)
        {
            Recover();
            return;
        }

        lastSafePosition = current;
    }

    // 마지막 안전 위치 근처 NavMesh로, 없으면 시작 지점으로
    private void Recover()
    {
        Vector3 target = lastSafePosition;
        if (NavMesh.SamplePosition(lastSafePosition, out NavMeshHit hit, recoverSearchRadius, NavMesh.AllAreas))
        {
            target = hit.position;
        }
        else if (arena != null)
        {
            target = arena.PlayerStartPosition;
        }

        target.y = Mathf.Max(target.y, 0f) + 0.1f;
        SetPosition(target, true);
        lastSafePosition = target;
        Debug.LogWarning($"PlayerPositionGuard: 플레이어가 추락하거나 맵 밖으로 나가 {target}로 복귀시켰습니다.");
    }

    private void SetPosition(Vector3 position, bool resetVerticalVelocity)
    {
        rb.position = position;
        transform.position = position;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        if (resetVerticalVelocity) velocity.y = 0f;
        rb.linearVelocity = velocity;
    }
}
