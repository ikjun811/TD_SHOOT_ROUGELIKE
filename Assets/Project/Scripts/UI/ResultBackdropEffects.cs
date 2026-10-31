using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 결과 화면 배경 연출 (사용자 채택 2026-10-31 — 두 결과의 배경을 다른 모습으로). MainMenuBackdrop(같은 오브젝트)의 카메라를 조종
//   클리어(SURFACE REACHED) = 위로 빠져나가는 카메라: 패닝을 멈추고 맵 가운데로 가며 직교 크기가 커져 시설 전체가 작아지고,
//     위에서 차가운 흰빛이 서서히 화면을 채움 (지하에서 지상으로)
//   실패(MISSION FAILED) = 감시 카메라: 흑백·약한 필름 노이즈·주사선, 구역마다 고정 시점으로 몇 초씩 끊어 보여 줌(지지직 전환),
//     오른쪽 위 "● REC  CAM 07", 오른쪽 아래 "DEPTH 13 · 시간", 가끔 신호가 끊겨 노이즈 + SIGNAL LOST
//   오버레이는 결과 UI 뒤(정렬 -90, 배경 그라데이션 -100 위)라 글자를 가리지 않음. 모두 실제 시간
//   씬 배치: Tools > TD Shoot > Add Arena Backdrop To Result Scene이 함께 부착
[RequireComponent(typeof(MainMenuBackdrop))]
public class ResultBackdropEffects : MonoBehaviour
{
    [Header("클리어 — 위로 빠져나가기")]
    [SerializeField] private float riseDelay = 1.2f;
    [SerializeField] private float riseDuration = 16f;
    [Tooltip("다 올라갔을 때 직교 크기 (맵 한 변 90m → 약 45면 전체)")]
    [SerializeField] private float riseOrthographicSize = 42f;
    [SerializeField] private Color surfaceLightColor = new Color(0.9f, 0.96f, 1f);
    [Range(0f, 1f)] [SerializeField] private float surfaceLightMaxAlpha = 0.45f;

    [Header("실패 — 감시 카메라")]
    [SerializeField] private float shotDuration = 6.5f;
    [SerializeField] private float cutGlitchDuration = 0.25f;
    [Tooltip("이 횟수마다 한 번 신호 끊김")]
    [SerializeField] private int signalLostEvery = 3;
    [SerializeField] private float signalLostDuration = 1.4f;
    [SerializeField] private float cctvOrthographicSize = 8f;
    [Tooltip("감시 카메라 시점: 내려다보는 각도")]
    [SerializeField] private float cctvPitch = 52f;
    [Tooltip("노이즈는 전환·신호 끊김 순간에만 (평소 꺼짐 — 스크린샷 확인: 상시 노이즈가 배경을 덮음)")]
    [Range(0f, 1f)] [SerializeField] private float glitchNoiseAlpha = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float signalLostNoiseAlpha = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float scanlineAlpha = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float filmGrain = 0.35f;
    [Tooltip("배경 어둡게 처리(MainMenuBackdrop 오버레이)에 곱함 — 감시 화면은 배경이 보여야 함")]
    [Range(0f, 1f)] [SerializeField] private float shadeMultiplier = 0.5f;

    private MainMenuBackdrop backdrop;
    private RectTransform overlay;
    private Texture2D overlayTexture;
    private Texture2D noiseTexture;
    private Color32[] noisePixels;
    private RawImage noiseImage;
    private RawImage scanlineImage;
    private TextMeshProUGUI recText;
    private TextMeshProUGUI stampText;
    private VolumeProfile cctvProfile;
    private float glitchUntil;
    private bool signalLost;
    private int depth;
    private string camLabel = "CAM 01";

    private IEnumerator Start()
    {
        backdrop = GetComponent<MainMenuBackdrop>();
        if (!RunResultData.HasData) yield break;
        yield return null; // MainMenuBackdrop.Start(맵 구역·카메라) 뒤
        CreateOverlayCanvas();
        if (RunResultData.Cleared) yield return Rise();
        else
        {
            depth = DepthDisplay.FromRound(RunResultData.Round, RunResultData.MaxRound);
            SetupCctv();
            yield return Cctv();
        }
    }

    private void OnDestroy()
    {
        if (overlayTexture != null) Destroy(overlayTexture);
        if (noiseTexture != null) Destroy(noiseTexture);
        if (cctvProfile != null) Destroy(cctvProfile);
    }

    private void CreateOverlayCanvas()
    {
        GameObject canvasObj = new GameObject("ResultBackdropOverlay", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -90;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f;
        overlay = canvasObj.GetComponent<RectTransform>();
    }

    private RawImage FullScreen(string name, Texture texture)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(overlay, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    #region 클리어 — 위로 빠져나가기

    private IEnumerator Rise()
    {
        // 위(화면 위쪽)가 밝은 세로 그라데이션
        overlayTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
        {
            float v = y / 127f; // 0 아래 → 1 위
            Color c = surfaceLightColor;
            c.a = Mathf.Lerp(0.25f, 1f, v * v);
            overlayTexture.SetPixel(0, y, c);
        }
        overlayTexture.Apply();
        RawImage light = FullScreen("SurfaceLight", overlayTexture);
        light.color = new Color(1f, 1f, 1f, 0f);

        BspArenaGenerator generator = FindAnyObjectByType<BspArenaGenerator>();
        Vector3 center = generator != null ? generator.transform.position : backdrop.Focus;
        float startSize = backdrop.OrthographicSize;

        // 잠깐 평소처럼 훑다가 올라가기 시작
        float wait = 0f;
        while (wait < riseDelay) { wait += Time.unscaledDeltaTime; yield return null; }
        backdrop.PanEnabled = false;
        Vector3 startFocus = backdrop.Focus;

        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / riseDuration);
            float e = k * k * (3f - 2f * k);
            backdrop.Focus = Vector3.Lerp(startFocus, center, e);
            backdrop.OrthographicSize = Mathf.Lerp(startSize, riseOrthographicSize, e);
            light.color = new Color(1f, 1f, 1f, surfaceLightMaxAlpha * Mathf.Clamp01(k * 1.2f));
            yield return null;
        }
    }

    #endregion

    #region 실패 — 감시 카메라

    private void SetupCctv()
    {
        // 흑백·대비·필름 노이즈·비네트·약한 왜곡 (분위기 Volume 12·위기 15·히트 스톱 20보다 위)
        GameObject volumeObj = new GameObject("CctvVolume");
        volumeObj.transform.SetParent(transform, false);
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 30f;
        cctvProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        cctvProfile.hideFlags = HideFlags.DontSave;
        ColorAdjustments color = cctvProfile.Add<ColorAdjustments>(true);
        color.saturation.Override(-100f);
        color.contrast.Override(18f);
        color.postExposure.Override(0.7f);
        color.colorFilter.Override(new Color(0.86f, 1f, 0.9f));
        FilmGrain grain = cctvProfile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin2);
        grain.intensity.Override(filmGrain);
        grain.response.Override(0.6f);
        Vignette vignette = cctvProfile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.smoothness.Override(0.6f);
        LensDistortion distortion = cctvProfile.Add<LensDistortion>(true);
        distortion.intensity.Override(-0.18f);
        ChromaticAberration aberration = cctvProfile.Add<ChromaticAberration>(true);
        aberration.intensity.Override(0.2f);
        volume.sharedProfile = cctvProfile;

        // 주사선: 3px마다 어두운 줄 1개 (점 필터·반복)
        overlayTexture = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        overlayTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
        overlayTexture.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        overlayTexture.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        overlayTexture.Apply();
        scanlineImage = FullScreen("Scanlines", overlayTexture);
        scanlineImage.color = new Color(1f, 1f, 1f, scanlineAlpha);

        // 노이즈: 작은 무작위 회색 텍스처를 매 프레임 다시 칠함
        noiseTexture = new Texture2D(320, 180, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        noisePixels = new Color32[320 * 180];
        noiseImage = FullScreen("Noise", noiseTexture);
        noiseImage.enabled = false;

        // 배경 어둡게 처리 약하게
        Transform shade = transform.Find("BackdropShade/Gradient");
        if (shade != null && shade.TryGetComponent(out RawImage shadeImage)) shadeImage.color = new Color(1f, 1f, 1f, shadeMultiplier);

        recText = CornerText("Rec", new Vector2(1f, 1f), new Vector2(-48f, -36f), TextAlignmentOptions.TopRight);
        stampText = CornerText("Stamp", new Vector2(1f, 0f), new Vector2(-48f, 36f), TextAlignmentOptions.BottomRight);
    }

    private TextMeshProUGUI CornerText(string name, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(overlay, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(700f, 40f);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, UIFontRole.Heading);
        text.fontSize = 24f;
        text.characterSpacing = 4f;
        text.alignment = alignment;
        text.color = new Color(0.85f, 0.95f, 0.88f, 0.85f);
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator Cctv()
    {
        int shot = 0;
        while (true)
        {
            // 새 시점: 무작위 구역, 대각선 방향 중 하나에서 내려다봄
            int camNumber = Random.Range(1, 17);
            backdrop.PanEnabled = false;
            backdrop.JumpToRandomZone();
            backdrop.CameraEuler = new Vector3(cctvPitch, 45f + 90f * Random.Range(0, 4), 0f);
            backdrop.OrthographicSize = cctvOrthographicSize;
            glitchUntil = Time.unscaledTime + cutGlitchDuration;
            camLabel = $"CAM {camNumber:00}";

            float t = 0f;
            while (t < shotDuration)
            {
                t += Time.unscaledDeltaTime;
                backdrop.OrthographicSize = cctvOrthographicSize - 0.6f * (t / shotDuration); // 아주 느린 디지털 줌
                yield return null;
            }

            shot++;
            if (signalLostEvery > 0 && shot % signalLostEvery == 0)
            {
                signalLost = true;
                float lost = 0f;
                while (lost < signalLostDuration) { lost += Time.unscaledDeltaTime; yield return null; }
                signalLost = false;
            }
        }
    }

    private void Update()
    {
        if (noiseTexture == null) return;
        float now = Time.unscaledTime;

        // 노이즈: 전환 순간·신호 끊김에만 켜고 그때만 다시 칠함
        bool noisy = signalLost || now < glitchUntil;
        noiseImage.enabled = noisy;
        if (noisy)
        {
            for (int i = 0; i < noisePixels.Length; i++)
            {
                byte v = (byte)Random.Range(0, 256);
                noisePixels[i] = new Color32(v, v, v, 255);
            }
            noiseTexture.SetPixels32(noisePixels);
            noiseTexture.Apply(false);
            noiseImage.color = new Color(1f, 1f, 1f, signalLost ? signalLostNoiseAlpha : glitchNoiseAlpha);
        }

        // 주사선이 천천히 아래로 흐름
        if (scanlineImage != null)
        {
            float rows = Mathf.Max(1f, Screen.height / 3f);
            scanlineImage.uvRect = new Rect(0f, now * 0.6f, 1f, rows);
        }

        bool blink = Mathf.Repeat(now, 1f) < 0.6f;
        if (recText != null)
        {
            recText.text = (blink ? "<color=#FF3B30>●</color> REC   " : "<alpha=#00>●</alpha> REC   ") + camLabel;
        }
        if (stampText != null)
        {
            if (signalLost)
            {
                stampText.text = blink ? "<color=#FF3B30>SIGNAL LOST</color>" : "";
            }
            else
            {
                int seconds = Mathf.FloorToInt(now);
                stampText.text = $"{DepthDisplay.Label(depth)}   ·   {seconds / 3600 % 24:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
            }
        }
    }

    #endregion
}
