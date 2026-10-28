using UnityEngine;
using UnityEngine.UI;

// 보급 상자 (SupplyCrateManager가 런타임에 생성, GDD 8-6 #13)
// 플레이어가 반경 안에 머무르면 열기 진행, 벗어나면 진행도 유지(일시정지). 다 차면 무기 1개 드랍 후 사라짐
// 연출 (C4): 여는 중 진행도만큼 떨림(막바지 더 빠르게) → 열림 순간 살짝 부풀었다 줄어들며 사라짐 + CrateOpenEffect(나온 무기 레어도 색)
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

    private GameObject barRoot;
    private RectTransform fillRect;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        basePosition = transform.position;

        BuildProgressBar();
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

        UpdateProgressBar();
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
        if (barRoot != null) barRoot.SetActive(false);
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

    // 상자 위 진행 막대 (진행이 시작된 뒤에만 표시)
    private void BuildProgressBar()
    {
        barRoot = new GameObject("CrateProgress", typeof(RectTransform), typeof(Canvas));
        barRoot.transform.SetParent(transform, false);
        barRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        RectTransform rootRect = barRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100f, 12f);

        // 상자 스케일 영향을 받지 않도록 월드 크기 기준으로 보정 (가로 1.2m)
        Vector3 parentScale = transform.lossyScale;
        barRoot.transform.localScale = new Vector3(1.2f / 100f / parentScale.x, 0.15f / 12f / parentScale.y, 1f / parentScale.z);

        CreateBarImage("Background", new Color(0f, 0f, 0f, 0.6f), rootRect);
        fillRect = CreateBarImage("Fill", progressColor, rootRect);
        fillRect.anchorMax = new Vector2(0f, 1f);

        barRoot.SetActive(false);
    }

    private RectTransform CreateBarImage(string objName, Color color, RectTransform parent)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    private void UpdateProgressBar()
    {
        if (barRoot == null) return;

        bool visible = openTimer > 0f;
        if (barRoot.activeSelf != visible) barRoot.SetActive(visible);
        if (!visible) return;

        fillRect.anchorMax = new Vector2(Progress, 1f);

        // 상자 위 1.5m, 카메라를 향하도록
        barRoot.transform.position = transform.position + Vector3.up * 1.5f;
        Camera cam = Camera.main;
        if (cam != null) barRoot.transform.rotation = cam.transform.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, openRadius);
    }
}
