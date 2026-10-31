using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 메인 화면 3D 배경 (사용자 채택 2026-10-31 B안: 실제 맵 위 탑다운 패닝)
//   같은 씬의 BspArenaGenerator(전투 씬 설정 복사, Awake 생성)가 만든 맵을 게임 카메라 각도로 천천히 훑음
//   분위기 조명·후처리는 전투 씬과 같은 코드(AtmosphereLighting·GameplayPostProcess)를 그대로 생성
//   초점에 플레이어 빛 대신 따뜻한 포인트 라이트, 메뉴 쪽(왼쪽)은 그라데이션으로 어둡게 해 글자 가독성 확보
//   씬 배치: Tools > TD Shoot > Add Arena Backdrop To Main Menu (생성기 복사 + 카메라 각도 기록)
public class MainMenuBackdrop : MonoBehaviour
{
    [Header("카메라 (도구가 전투 씬 카메라 각도로 채움)")]
    [SerializeField] private Vector3 cameraEuler = new Vector3(65f, 0f, 0f);
    [SerializeField] private float orthographicSize = 9f;
    [Tooltip("초점에서 카메라까지 거리 — 그림자 거리(기본 50m) 안이어야 그림자가 보임")]
    [SerializeField] private float cameraDistance = 30f;
    [Tooltip("초점이 화면 가운데보다 오른쪽에 오도록 (화면 반폭 비율, 메뉴가 왼쪽)")]
    [Range(0f, 0.6f)] [SerializeField] private float focusScreenOffset = 0.3f;

    [Header("패닝")]
    [SerializeField] private float panSpeed = 1.4f;
    [Tooltip("방향을 바꾸는 부드러움 (m/s²)")]
    [SerializeField] private float panAcceleration = 0.6f;
    [Tooltip("다음 구역 후보 = 가까운 구역 몇 개 중 무작위")]
    [SerializeField] private int nextZoneCandidates = 3;
    [SerializeField] private float arriveDistance = 4f;

    [Header("초점 빛 (전투 플레이어 빛 대신)")]
    [SerializeField] private Color focusLightColor = new Color(1f, 0.82f, 0.6f);
    [SerializeField] private float focusLightRange = 12f;
    [SerializeField] private float focusLightIntensity = 3f;
    [SerializeField] private float focusLightHeight = 2.5f;

    [Header("메뉴 쪽 그라데이션")]
    [Range(0f, 1f)] [SerializeField] private float shadeAlpha = 0.85f;
    [Tooltip("화면 왼쪽부터 이 비율까지 진하게 유지")]
    [Range(0f, 1f)] [SerializeField] private float shadeSolidUntil = 0.25f;
    [Tooltip("이 비율에서 완전히 투명")]
    [Range(0f, 1f)] [SerializeField] private float shadeFadeUntil = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float overallShade = 0.1f;

    private Camera cam;
    private Light focusLight;
    private readonly List<Vector3> zones = new List<Vector3>();
    private readonly List<int> recent = new List<int>();
    private Vector3 focus;
    private Vector3 velocity;
    private int targetZone = -1;
    private Texture2D shadeTexture;

    private void Start()
    {
        AtmosphereLighting.EnsureCreated();
        GameplayPostProcess.EnsureCreated();

        cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = cameraDistance + 60f;
            cam.transform.rotation = Quaternion.Euler(cameraEuler);
        }

        BspArenaGenerator generator = FindAnyObjectByType<BspArenaGenerator>();
        if (generator != null && generator.Leaves.Count > 0) zones.AddRange(generator.GetZoneCenters());
        if (zones.Count > 0)
        {
            int start = Random.Range(0, zones.Count);
            focus = zones[start];
            recent.Add(start);
            PickNextZone();
        }

        GameObject lightObj = new GameObject("BackdropFocusLight");
        lightObj.transform.SetParent(transform, false);
        focusLight = lightObj.AddComponent<Light>();
        focusLight.type = LightType.Point;
        focusLight.color = focusLightColor;
        focusLight.range = focusLightRange;
        focusLight.intensity = focusLightIntensity;
        focusLight.shadows = LightShadows.None;

        shadeTexture = CreateShadeOverlay(transform, shadeAlpha, shadeSolidUntil, shadeFadeUntil, overallShade);
        UpdateCamera();
    }

    private void OnDestroy()
    {
        if (shadeTexture != null) Destroy(shadeTexture);
    }

    // 메뉴 화면이라 실제 시간 기준 (환경설정 등으로 timeScale이 바뀌어도 계속 흐름)
    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (targetZone >= 0)
        {
            Vector3 toTarget = zones[targetZone] - focus;
            toTarget.y = 0f;
            if (toTarget.magnitude < arriveDistance) PickNextZone();
            Vector3 desired = toTarget.normalized * panSpeed;
            velocity = Vector3.MoveTowards(velocity, desired, panAcceleration * dt);
            focus += velocity * dt;
        }
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (focusLight != null) focusLight.transform.position = focus + Vector3.up * focusLightHeight;
        if (cam == null) return;
        Transform t = cam.transform;
        t.rotation = Quaternion.Euler(cameraEuler);
        // 초점을 화면 오른쪽에 두려면 카메라를 왼쪽으로 옮김
        float halfWidth = orthographicSize * cam.aspect;
        Vector3 shift = -t.right * (halfWidth * focusScreenOffset);
        t.position = focus + shift - t.forward * cameraDistance;
    }

    // 가까운 구역 몇 개 중 무작위 (최근에 간 구역 제외) → 맵을 고르게 돌아다님
    private void PickNextZone()
    {
        if (zones.Count < 2) { targetZone = -1; return; }
        List<int> order = new List<int>();
        for (int i = 0; i < zones.Count; i++)
        {
            if (!recent.Contains(i)) order.Add(i);
        }
        if (order.Count == 0) { recent.Clear(); PickNextZone(); return; }
        Vector3 from = focus;
        order.Sort((a, b) => (zones[a] - from).sqrMagnitude.CompareTo((zones[b] - from).sqrMagnitude));
        targetZone = order[Random.Range(0, Mathf.Min(nextZoneCandidates, order.Count))];
        recent.Add(targetZone);
        if (recent.Count > Mathf.Max(1, zones.Count / 2)) recent.RemoveAt(0);
    }

    // 메뉴 캔버스 뒤(정렬 순서 -100) 오버레이: 왼쪽 진하게 → 오른쪽 투명 (디오라마 배경도 사용, 텍스처는 호출 쪽에서 파괴)
    internal static Texture2D CreateShadeOverlay(Transform parent, float shadeAlpha, float shadeSolidUntil, float shadeFadeUntil, float overallShade)
    {
        GameObject canvasObj = new GameObject("BackdropShade", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(parent, false);
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        const int width = 256;
        Texture2D shadeTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[width];
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)(width - 1);
            float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shadeSolidUntil, shadeFadeUntil, u));
            float a = Mathf.Max(k * shadeAlpha, overallShade);
            pixels[x] = new Color(0.01f, 0.015f, 0.025f, a);
        }
        shadeTexture.SetPixels32(pixels);
        shadeTexture.Apply();

        GameObject imageObj = new GameObject("Gradient", typeof(RectTransform), typeof(RawImage));
        imageObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rect = imageObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        RawImage image = imageObj.GetComponent<RawImage>();
        image.texture = shadeTexture;
        image.raycastTarget = false;
        return shadeTexture;
    }
}
