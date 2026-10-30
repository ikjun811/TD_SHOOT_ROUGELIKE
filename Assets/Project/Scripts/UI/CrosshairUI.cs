using JU;
using JUTPS;
using JUTPS.WeaponSystem;
using UnityEngine;
using UnityEngine.UI;

// 조준점 (사용자 요청 2026-10-30, GameplayBootstrap이 자동 생성)
//   전투 중 시스템 커서를 숨기고 마우스 위치에 조준점: 가운데 점 + 상하좌우 짧은 선 4개 (흰색 + 검정 외곽, HUD "선과 글자" 스타일)
//   선 간격 = 기본 간격(산탄총은 넓게) + JU 무기의 현재 탄 퍼짐(ShotErrorProbability, 실제 정확도) + 쏠 때마다 짧게 벌어짐(OnAnyPlayerShot)
//   커서 아래 적이 있으면 붉게. 장전 원호·탄창 빈 표시는 하지 않음 (장탄 링이 이미 보여 줌 — 사용자)
//   숨김(시스템 커서 보임): 정비·일시정지·결과(timeScale 0), 탈출 연출, 사망, 결과 화면
public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance { get; private set; }

    [Header("모양 (1920x1080 기준 px)")]
    public float dotSize = 4f;
    public Vector2 lineSize = new Vector2(3f, 10f);   // 두께, 길이
    public float outline = 1.5f;
    public float baseGap = 8f;                         // 가운데에서 선 안쪽 끝까지
    public float shotgunGap = 20f;

    [Header("퍼짐")]
    [Tooltip("JU 탄 퍼짐(ShotErrorProbability) 1당 벌어지는 px")]
    public float spreadPixels = 28f;
    public float maxSpreadGap = 36f;
    [Tooltip("한 발마다 짧게 벌어지는 px (0.15초에 걸쳐 돌아옴)")]
    public float shotKick = 6f;
    public float kickRecover = 40f;                    // px/초
    [Tooltip("간격 변화 부드럽게 (1/초)")]
    public float gapSmoothing = 20f;

    [Header("색")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.95f);
    public Color enemyColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color outlineColor = new Color(0f, 0f, 0f, 0.85f);
    [Tooltip("커서 아래 적 판정 반경 (m)")]
    public float enemyPickRadius = 0.35f;

    private Canvas canvas;
    private RectTransform root;
    private Image dot;
    private readonly RectTransform[] lines = new RectTransform[4];
    private readonly Image[] lineImages = new Image[4];
    private float kick;
    private float gap;
    private bool shown;

    private JUCharacterController player;
    private JUHealth playerHealth;
    private ExtractionZone extractionZone;
    private Camera mainCamera;
    private readonly RaycastHit[] hits = new RaycastHit[8];

    private static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    public static CrosshairUI EnsureCreated()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("CrosshairUI");
        return go.AddComponent<CrosshairUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Build();
        WeaponModuleRuntime.OnAnyPlayerShot += HandleShot;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        WeaponModuleRuntime.OnAnyPlayerShot -= HandleShot;
        Cursor.visible = true; // 씬을 떠날 때 커서 복구
        Instance = null;
    }

    private void HandleShot(Weapon weapon)
    {
        kick = Mathf.Min(kick + shotKick, maxSpreadGap);
    }

    private void LateUpdate()
    {
        bool show = ShouldShow();
        if (show != shown)
        {
            shown = show;
            canvas.enabled = show;
            Cursor.visible = !show;
        }
        if (!show) return;
        if (Cursor.visible) Cursor.visible = false; // 다른 곳(일시정지 복구 등)이 켜 두었으면 다시 숨김

        root.position = Input.mousePosition;

        // 간격: 기본 + 실제 탄 퍼짐 + 사격 반동
        Weapon weapon = player != null ? player.RightHandWeapon : null;
        float target = baseGap;
        if (weapon != null)
        {
            if (weapon.FireMode == Weapon.WeaponFireMode.Shotgun) target = shotgunGap;
            target += Mathf.Min(weapon.ShotErrorProbability * spreadPixels, maxSpreadGap);
        }
        kick = Mathf.MoveTowards(kick, 0f, kickRecover * Time.unscaledDeltaTime);
        target += kick;
        gap = Mathf.Lerp(gap, target, 1f - Mathf.Exp(-gapSmoothing * Time.unscaledDeltaTime));

        for (int i = 0; i < 4; i++)
        {
            float offset = gap + lineSize.y * 0.5f;
            lines[i].anchoredPosition = Directions[i] * offset;
        }

        Color color = IsEnemyUnderCursor() ? enemyColor : normalColor;
        dot.color = color;
        for (int i = 0; i < 4; i++) lineImages[i].color = color;
    }

    private bool ShouldShow()
    {
        if (Time.timeScale <= 0f) return false; // 정비·일시정지·결과
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return false;
            player = playerObj.GetComponent<JUCharacterController>();
            playerHealth = playerObj.GetComponent<JUHealth>();
        }
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (extractionZone == null) extractionZone = FindAnyObjectByType<ExtractionZone>();
        if (extractionZone != null && extractionZone.IsSequenceActive) return false;
        if (RunResultManager.Instance != null && RunResultManager.Instance.IsShowing) return false;
        if (PauseMenuUI.IsPaused) return false;
        return true;
    }

    // 마우스 광선에 살아 있는 적이 걸리는지 (직교 카메라, 벽 뒤 적도 실루엣으로 보이므로 첫 적중만 보지 않음)
    private bool IsEnemyUnderCursor()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int count = Physics.SphereCastNonAlloc(ray, enemyPickRadius, hits, 200f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = hits[i].collider.GetComponentInParent<EnemyBase>();
            if (enemy != null && !enemy.IsDead) return true;
        }
        return false;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80; // HUD·알림 위, 일시정지 메뉴(90) 아래
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f;
        canvas.enabled = false;

        GameObject rootObj = new GameObject("Crosshair", typeof(RectTransform));
        root = rootObj.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        dot = CreatePart("Dot", new Vector2(dotSize, dotSize), out _);
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i < 2;
            Vector2 size = vertical ? new Vector2(lineSize.x, lineSize.y) : new Vector2(lineSize.y, lineSize.x);
            lineImages[i] = CreatePart("Line_" + i, size, out lines[i]);
        }
    }

    // 검정 외곽(바탕) + 흰 본체. 반환 = 본체, rect = 위치를 옮길 바탕 (본체는 그 자식)
    private Image CreatePart(string name, Vector2 size, out RectTransform rect)
    {
        GameObject backObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        rect = backObj.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.sizeDelta = size + Vector2.one * outline * 2f;
        Image back = backObj.GetComponent<Image>();
        back.color = outlineColor;
        back.raycastTarget = false;

        GameObject frontObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform front = frontObj.GetComponent<RectTransform>();
        front.SetParent(rect, false);
        front.sizeDelta = size;
        Image image = frontObj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
