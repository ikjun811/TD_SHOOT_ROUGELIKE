using UnityEngine;

// 보급 상자 (SupplyCrateManager가 런타임에 생성, GDD 8-6 #13)
// 플레이어가 반경 안에 머무르면 열기 진행, 벗어나면 진행도 유지(일시정지). 다 차면 무기 1개 드랍 후 사라짐
// 연출 (C4): 여는 중 진행도만큼 떨림(막바지 더 빠르게) → 열림 순간 살짝 부풀었다 줄어들며 사라짐 + CrateOpenEffect(나온 무기 레어도 색)
// 표식 (CrateMarker): 열기 범위 노란 바닥 원 + 진행 호 + 위 꺾쇠 — 예전 상자 위 진행 막대 대체 (사용자 2026-10-29)
public class SupplyCrate : MonoBehaviour
{
    [HideInInspector] public float openRadius = 2f;
    [HideInInspector] public float openTime = 3f;
    [HideInInspector] public Color progressColor = new Color(1f, 0.8f, 0.2f);

    public bool IsOpened { get; private set; }
    public float Progress => Mathf.Clamp01(openTimer / openTime);

    private float openTimer;
    private Transform player;
    private Vector3 basePosition;

    private const float ShakeAmplitude = 0.04f;  // 진행도 100%일 때 흔들림 (m)
    private const float PopScale = 1.15f;
    private const float PopUpTime = 0.06f;
    private const float PopDownTime = 0.2f;

    private CrateMarker marker;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        basePosition = transform.position;

        marker = CrateMarker.Create(this, player);
        AtmosphereLighting.AttachCrateLight(transform); // 노란 빛 (렌더링 ③)
    }

    private void Update()
    {
        if (IsOpened || player == null) return;

        // 수평 거리로 범위 판정 (시간 정지 중에는 deltaTime 0이라 진행 안 됨)
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        bool opening = offset.sqrMagnitude <= openRadius * openRadius;
        if (opening)
        {
            openTimer += Time.deltaTime;
            if (openTimer >= openTime)
            {
                Open();
                return;
            }
        }
        UpdateShake(opening);
    }

    // 여는 중 떨림: 진행도만큼 세지고 막바지(80%~)에 빨라짐, 범위를 벗어나면 제자리
    private void UpdateShake(bool opening)
    {
        if (!opening || Time.timeScale <= 0f)
        {
            transform.position = basePosition;
            return;
        }
        float p = Progress;
        float frequency = p > 0.8f ? 45f : 28f;
        float amplitude = ShakeAmplitude * p;
        float time = Time.time * frequency;
        transform.position = basePosition + new Vector3(Mathf.Sin(time * 1.3f), 0f, Mathf.Cos(time)) * amplitude;
    }

    private void Open()
    {
        IsOpened = true;
        transform.position = basePosition;

        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        WeaponDataSO weapon = ItemDropManager.Instance != null ? ItemDropManager.Instance.DropCrateWeapon(basePosition, round) : null;

        CrateOpenEffect.Spawn(basePosition, weapon != null ? weapon.GetRarityColor() : progressColor);
        if (marker != null) marker.Hide();
        StartCoroutine(PopAndDestroy());
    }

    // 살짝 부풀었다가 줄어들며 사라짐
    private System.Collections.IEnumerator PopAndDestroy()
    {
        Vector3 baseScale = transform.localScale;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

        float elapsed = 0f;
        while (elapsed < PopUpTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = baseScale * Mathf.Lerp(1f, PopScale, elapsed / PopUpTime);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < PopDownTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / PopDownTime);
            transform.localScale = baseScale * Mathf.Lerp(PopScale, 0f, t * t);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, openRadius);
    }
}
