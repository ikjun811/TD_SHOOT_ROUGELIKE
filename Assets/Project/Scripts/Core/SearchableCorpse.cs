using UnityEngine;

// 시체 수색 (탐색 보상 S1, 사용자 채택 2026-10-30) — SupplyCrateManager가 층마다 생성
//   시설에 쓰러진 대원의 시체: 옆(searchRadius)에 머무르면 수색 진행, 벗어나면 진행도 유지. 다 차면 모듈 1~2개(현재 층 레어도 표)
//   적 드랍률 감소·천장과 무관 (ItemDropManager.DropBonusModules). 화살표 안내 없음 — 직접 찾아다니는 보상
//   표식 = 상자와 같은 CrateMarker를 청록으로 (바닥 원·진행 호·위 꺾쇠), 수색이 끝나면 표식만 사라지고 시체는 남음
//   충돌 없음(SupplyCrateManager가 콜라이더 제거) → 이동을 막지 않음
public class SearchableCorpse : MonoBehaviour
{
    public static Color markerColor = new Color(0.35f, 0.9f, 1f, 1f);

    [HideInInspector] public float searchRadius = 1.6f;
    [HideInInspector] public float searchTime = 2f;
    [HideInInspector] public Vector2Int moduleCount = new Vector2Int(1, 2);

    public bool IsSearched { get; private set; }
    public float Progress => Mathf.Clamp01(timer / searchTime);

    private float timer;
    private Transform player;
    private CrateMarker marker;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        marker = CrateMarker.Create(transform, () => searchRadius, () => Progress, player, markerColor);
    }

    private void Update()
    {
        if (IsSearched || player == null) return;

        // 수평 거리로 범위 판정 (시간 정지 중에는 deltaTime 0이라 진행 안 됨)
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > searchRadius * searchRadius) return;

        timer += Time.deltaTime;
        if (timer >= searchTime) Search();
    }

    private void Search()
    {
        IsSearched = true;
        if (marker != null) marker.Hide();

        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        int count = Random.Range(moduleCount.x, moduleCount.y + 1);
        if (ItemDropManager.Instance != null) ItemDropManager.Instance.DropBonusModules(transform.position, count, round);
        CrateOpenEffect.Spawn(transform.position, markerColor);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = markerColor;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}
