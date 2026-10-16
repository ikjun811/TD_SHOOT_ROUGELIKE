using UnityEngine;

// 플레이어 머리 위에서 탈출 지점 3D 좌표를 실시간 가리키는 나침반 화살표 클래스
public class ExtractionIndicator : MonoBehaviour
{
    [Header("Target Reference")]
    [SerializeField] private Transform extractionZoneTransform;
    [SerializeField] private Vector3 offsetFromPlayer = new Vector3(0f, 2.2f, 0f); // 플레이어 머리 위 높이

    private Transform playerTransform;

    private void Start()
    {
        FindPlayer();
        FindExtractionZone();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void FindExtractionZone()
    {
        if (extractionZoneTransform == null)
        {
            ExtractionZone zone = FindObjectOfType<ExtractionZone>();
            if (zone != null)
            {
                extractionZoneTransform = zone.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // 플레이어 머리 위 위치 실시간 추종
        transform.position = playerTransform.position + offsetFromPlayer;

        if (extractionZoneTransform == null)
        {
            FindExtractionZone();
            return;
        }

        // 탈출 지점 방향 수평 회전 연산
        Vector3 direction = (extractionZoneTransform.position - transform.position).normalized;
        direction.y = 0f; // 수평 평면 회전만 고정

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}