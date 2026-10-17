using UnityEngine;
using UnityEngine.UI;

// 보급 상자 (SupplyCrateManager가 런타임에 생성, GDD 8-6 #13)
// 플레이어가 반경 안에 머무르면 열기 진행, 벗어나면 진행도 유지(일시정지). 다 차면 무기 1개 드랍 후 사라짐
public class SupplyCrate : MonoBehaviour
{
    [HideInInspector] public float openRadius = 2f;
    [HideInInspector] public float openTime = 3f;
    [HideInInspector] public Color progressColor = new Color(1f, 0.8f, 0.2f);

    public bool IsOpened { get; private set; }
    public float Progress => Mathf.Clamp01(openTimer / openTime);

    private float openTimer;
    private Transform player;

    private GameObject barRoot;
    private RectTransform fillRect;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        BuildProgressBar();
    }

    private void Update()
    {
        if (IsOpened || player == null) return;

        // 수평 거리로 범위 판정 (시간 정지 중에는 deltaTime 0이라 진행 안 됨)
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= openRadius * openRadius)
        {
            openTimer += Time.deltaTime;
            if (openTimer >= openTime) Open();
        }

        UpdateProgressBar();
    }

    private void Open()
    {
        IsOpened = true;

        int round = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 1;
        if (ItemDropManager.Instance != null) ItemDropManager.Instance.DropCrateWeapon(transform.position, round);

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
