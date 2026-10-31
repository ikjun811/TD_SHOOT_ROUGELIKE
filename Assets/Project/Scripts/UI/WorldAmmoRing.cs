using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JUTPS;
using JUTPS.WeaponSystem;

// 캐릭터 화면 오른쪽 원형 장탄 링 (플레이어에 부착)
// 링 스프라이트는 런타임에 생성 (에셋 불필요). 쌍권총은 양손 탄을 합산
// 외형(2026-10-31): 탄 수만큼 나뉜 칸 링(탄창이 maxSegments보다 크면 여러 발을 한 칸으로) + 바깥 가는 테두리 원 + 숫자 뒤 남색 원판,
//   숫자 갈무리 외곽선 + 아래 작은 "/ 탄창", 재장전은 빈 링에서부터 칸이 차례로 차오름(남은 탄부터가 아니라 — 사용자),
//   탄 부족이면 빨강 맥박. 수류탄 충전 = 링 오른쪽 바깥 호를 따라 마름모(아래부터 차오름, 다 차면 반짝)
[RequireComponent(typeof(JUCharacterController))]
public class WorldAmmoRing : MonoBehaviour
{
    [Header("Position")]
    public float sideOffset = 1.0f;     // 화면 기준 오른쪽 거리 (m)
    public float heightOffset = 1.2f;   // 발밑 기준 높이 (m)
    public float ringSize = 0.6f;       // 링 지름 (m)

    [Header("Style (씬에 저장된 옛 색 값이 덮지 않도록 이름 변경)")]
    public Color ammoColor = new Color(0.92f, 0.97f, 1f);
    public Color ammoLowColor = new Color(1f, 0.3f, 0.25f);
    public Color ammoReloadColor = new Color(0.35f, 0.88f, 1f);
    public Color slotColor = new Color(0.6f, 0.8f, 0.9f, 0.16f);
    public Color ringFrameColor = new Color(0.35f, 0.85f, 1f, 0.45f);
    public Color discColor = new Color(0.02f, 0.035f, 0.05f, 0.6f);
    public Color magazineTextColor = new Color(0.7f, 0.8f, 0.88f, 0.75f);
    [Range(0f, 1f)] public float lowAmmoThreshold = 0.25f;
    [Tooltip("탄창이 이보다 크면 여러 발을 한 칸으로 묶음")]
    public int maxSegments = 30;
    [Tooltip("칸 사이 틈 (링 텍스처 256 기준 픽셀)")]
    public float segmentGapPixels = 4f;

    [Header("Grenade Charges (링 오른쪽 바깥 호를 따라 마름모)")]
    public Color grenadeReadyColor = new Color(1f, 0.6f, 0.15f);
    public Color grenadeChargingColor = new Color(1f, 0.6f, 0.15f, 0.35f);
    public Color grenadeSlotColor = new Color(1f, 0.6f, 0.15f, 0.15f);
    public Color grenadeFrameColor = new Color(0.02f, 0.035f, 0.05f, 0.85f);
    public float grenadeIconSize = 15f;   // 링 캔버스(100x100) 기준 단위
    public float grenadeIconGap = 5f;     // 마름모 사이 간격
    public float grenadeRingGap = 6f;     // 링 바깥과 마름모 사이 간격
    public float grenadeReadyFlashTime = 0.3f;

    private const int TextureSize = 256;
    private const float FrameOuter = 127f;   // 텍스처 픽셀 반지름
    private const float FrameInner = 124.5f;
    private const float SegmentOuter = 119f;
    private const float SegmentInner = 89f;

    private JUCharacterController controller;
    private Camera mainCamera;

    private GameObject ringRoot;
    private Image slotImage;
    private Image fillImage;
    private TextMeshProUGUI ammoText;
    private TextMeshProUGUI magazineText;
    private int builtSegments = -1;
    private readonly Dictionary<int, Sprite> segmentSprites = new Dictionary<int, Sprite>();
    private readonly List<Texture2D> textures = new List<Texture2D>();

    private GrenadeSkill grenadeSkill;
    private Image[] grenadeFills;
    private RectTransform[] grenadeIcons;
    private float[] grenadeFlash;
    private GameObject[] grenadeBars;      // 칸 (최대 충전 수가 바뀌면 다시 만듦)
    private RectTransform grenadeBarParent;
    private Sprite diamondSprite;
    private int lastCharges = -1;

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
        grenadeSkill = GetComponent<GrenadeSkill>();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        BuildRing();
    }

    private void BuildRing()
    {
        // 캐릭터 회전 영향을 받지 않도록 부모 없이 생성
        ringRoot = new GameObject($"{name}_AmmoRing", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = ringRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rootRect = ringRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100f, 100f);
        ringRoot.transform.localScale = Vector3.one * (ringSize / 100f);

        CreateImage("Disc", rootRect, CreateRingSprite(1, SegmentInner - 3f, 0f), discColor);
        CreateImage("Frame", rootRect, CreateRingSprite(1, FrameOuter, FrameInner), ringFrameColor);

        Sprite segments = GetSegmentSprite(1);
        slotImage = CreateImage("Slots", rootRect, segments, slotColor);
        fillImage = CreateImage("Fill", rootRect, segments, ammoColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;
        builtSegments = 1;

        ammoText = CreateText("AmmoText", rootRect, new Vector2(0f, 5f), new Vector2(80f, 40f), 34f, true);
        magazineText = CreateText("MagazineText", rootRect, new Vector2(0f, -17f), new Vector2(60f, 16f), 12f, false);
        magazineText.color = magazineTextColor;

        diamondSprite = CreateDiamondSprite();
        if (grenadeSkill != null) BuildGrenadeBars(rootRect);

        // 벽 뒤에서도 보이게
        WorldUIOverlay.Apply(ringRoot);
    }

    private TextMeshProUGUI CreateText(string objName, RectTransform parent, Vector2 position, Vector2 size, float fontSize, bool outline)
    {
        GameObject textObj = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, UIFontRole.Heading); // WorldUIOverlay가 재질을 바꾸기 전에
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        if (outline)
        {
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 220);
        }
        return text;
    }

    // 링 오른쪽 바깥 호를 따라 수류탄 충전 마름모를 아래→위로 배치 (캔버스 100x100 기준 좌표)
    // 충전 +1 모듈로 최대 충전 수가 바뀌면 UpdateGrenadeBars가 기존 칸을 지우고 다시 호출
    private void BuildGrenadeBars(RectTransform parent)
    {
        grenadeBarParent = parent;
        if (grenadeBars != null)
        {
            foreach (GameObject old in grenadeBars)
            {
                if (old != null) Destroy(old);
            }
        }

        int count = Mathf.Max(1, grenadeSkill.MaxCharges);
        float radius = 50f + grenadeRingGap + grenadeIconSize * 0.5f;
        float step = (grenadeIconSize + grenadeIconGap) / radius; // 라디안
        grenadeFills = new Image[count];
        grenadeIcons = new RectTransform[count];
        grenadeFlash = new float[count];
        grenadeBars = new GameObject[count];
        lastCharges = -1;

        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * step; // 0 = 오른쪽, +는 위
            GameObject icon = new GameObject($"Grenade_{i}", typeof(RectTransform));
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.SetParent(parent, false);
            iconRect.sizeDelta = Vector2.one * grenadeIconSize;
            iconRect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            Image frame = CreateImage("Frame", iconRect, diamondSprite, grenadeFrameColor);
            RectTransform frameRect = frame.rectTransform;
            frameRect.offsetMin = Vector2.one * -2.5f;
            frameRect.offsetMax = Vector2.one * 2.5f;
            CreateImage("Slot", iconRect, diamondSprite, grenadeSlotColor);

            Image fill = CreateImage("Fill", iconRect, diamondSprite, grenadeReadyColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;

            grenadeFills[i] = fill;
            grenadeIcons[i] = iconRect;
            grenadeBars[i] = icon;
        }
    }

    // 충전된 칸은 가득(준비 색), 충전 중인 칸은 쿨타임 진행도만큼 아래에서 위로(충전 중 색), 새로 찬 칸은 잠깐 커지며 흰빛
    private void UpdateGrenadeBars()
    {
        if (grenadeFills == null || grenadeSkill == null) return;

        // 최대 충전 수가 바뀌었으면 칸 다시 만들기 (벽 뒤 표시 재질도 다시 적용)
        if (grenadeFills.Length != Mathf.Max(1, grenadeSkill.MaxCharges) && grenadeBarParent != null)
        {
            BuildGrenadeBars(grenadeBarParent);
            WorldUIOverlay.Apply(ringRoot);
        }

        // 자동 투척 모듈이면 충전을 쓰지 않으므로 숨김
        bool visible = grenadeSkill.UsesCharges;
        foreach (GameObject bar in grenadeBars)
        {
            if (bar != null && bar.activeSelf != visible) bar.SetActive(visible);
        }
        if (!visible) return;

        int charges = grenadeSkill.CurrentCharges;
        float progress = Mathf.Clamp01(grenadeSkill.CooldownProgress);
        if (lastCharges >= 0 && charges > lastCharges)
        {
            for (int i = lastCharges; i < Mathf.Min(charges, grenadeFlash.Length); i++) grenadeFlash[i] = grenadeReadyFlashTime;
        }
        lastCharges = charges;

        for (int i = 0; i < grenadeFills.Length; i++)
        {
            float amount;
            Color color;
            if (i < charges) { amount = 1f; color = grenadeReadyColor; }
            else if (i == charges) { amount = progress; color = grenadeChargingColor; }
            else { amount = 0f; color = grenadeChargingColor; }

            float flash = 0f;
            if (grenadeFlash[i] > 0f && grenadeReadyFlashTime > 0f)
            {
                grenadeFlash[i] -= Time.deltaTime;
                flash = Mathf.Clamp01(grenadeFlash[i] / grenadeReadyFlashTime);
            }
            grenadeFills[i].fillAmount = amount;
            grenadeFills[i].color = Color.Lerp(color, Color.white, flash * 0.8f);
            grenadeIcons[i].localScale = Vector3.one * (1f + 0.45f * flash * flash);
        }
    }

    private Image CreateImage(string objName, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Sprite GetSegmentSprite(int segments)
    {
        if (!segmentSprites.TryGetValue(segments, out Sprite sprite))
        {
            sprite = CreateRingSprite(segments, SegmentOuter, SegmentInner);
            segmentSprites[segments] = sprite;
        }
        return sprite;
    }

    // 흰색 도넛(inner 0이면 원판) 텍스처 생성, segments > 1이면 12시부터 같은 각도로 나눈 경계에 틈 (가장자리 안티앨리어싱)
    // 틈이 칸 경계 한가운데라 Radial360 채움(12시 시계 방향)을 k/N으로 자르면 틈에서 깔끔하게 끊김
    private Sprite CreateRingSprite(int segments, float outer, float inner)
    {
        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        textures.Add(tex);

        float center = (TextureSize - 1) * 0.5f;
        float segmentAngle = Mathf.PI * 2f / Mathf.Max(1, segments);

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(outer - dist + 0.5f);
                if (inner > 0f) alpha *= Mathf.Clamp01(dist - inner + 0.5f);
                if (segments > 1 && alpha > 0f)
                {
                    float angle = Mathf.Atan2(dx, dy); // 12시 = 0, 시계 방향 +
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    float local = angle % segmentAngle;
                    float arc = Mathf.Min(local, segmentAngle - local) * dist;
                    alpha *= Mathf.Clamp01(arc - segmentGapPixels * 0.5f + 0.5f);
                }
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateDiamondSprite()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        textures.Add(tex);
        float c = (size - 1) * 0.5f;
        float r = size * 0.5f - 1f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((r - d) * 0.7f + 0.5f));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void LateUpdate()
    {
        if (ringRoot == null) return;

        Weapon right = controller.RightHandWeapon;
        Weapon left = controller.LeftHandWeapon;

        // 총을 들고 있지 않거나 사망 시 숨김
        bool visible = (right != null || left != null) && !controller.IsDead;
        if (ringRoot.activeSelf != visible) ringRoot.SetActive(visible);
        if (!visible) return;

        int ammo = 0;
        int magazine = 0;
        if (right != null) { ammo += right.BulletsAmounts; magazine += right.BulletsPerMagazine; }
        if (left != null) { ammo += left.BulletsAmounts; magazine += left.BulletsPerMagazine; }

        // 무기·탄창 모듈로 칸 수가 바뀌면 스프라이트 교체
        int segments = Mathf.Clamp(magazine, 1, Mathf.Max(1, maxSegments));
        if (segments != builtSegments)
        {
            Sprite sprite = GetSegmentSprite(segments);
            slotImage.sprite = sprite;
            fillImage.sprite = sprite;
            builtSegments = segments;
        }

        float ammoRatio = magazine > 0 ? Mathf.Clamp01((float)ammo / magazine) : 0f;
        Color color;
        int shownAmmo;

        if (TryGetReloadProgress(out float reloadProgress))
        {
            // 재장전: 빈 링에서부터 장전 진행도에 맞춰 칸이 차례로 차오름
            fillImage.fillAmount = Mathf.Round(reloadProgress * segments) / segments;
            shownAmmo = Mathf.RoundToInt(reloadProgress * magazine);
            color = ammoReloadColor;
        }
        else
        {
            // 남은 탄이 조금이라도 있는 칸은 켬 (묶음 칸)
            fillImage.fillAmount = ammo <= 0 ? 0f : Mathf.Ceil(ammoRatio * segments - 0.0001f) / segments;
            shownAmmo = ammo;
            if (ammoRatio <= lowAmmoThreshold)
            {
                color = ammoLowColor;
                color.a *= Mathf.Lerp(0.6f, 1f, 0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f * 2.5f));
            }
            else color = ammoColor;
        }
        fillImage.color = color;
        ammoText.color = color;
        ammoText.text = shownAmmo.ToString();
        magazineText.text = "/ " + magazine;

        UpdateGrenadeBars();

        // 캐릭터 기준 화면 오른쪽 + 카메라를 향하도록 회전
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            ringRoot.transform.position = transform.position + mainCamera.transform.right * sideOffset + Vector3.up * heightOffset;
            ringRoot.transform.rotation = mainCamera.transform.rotation;
        }
    }

    // 재장전 중이면 true, progress는 장전 애니메이션 진행도 (0~1). 애니메이션 진입 전에는 0
    private bool TryGetReloadProgress(out float progress)
    {
        progress = 0f;
        Animator anim = controller.anim;
        if (anim == null) return controller.IsReloading;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(controller.AnimatorParameters._BothArmsLayerIndex);
        bool inReloadState = state.IsName("Reload Right Hand Weapon") || state.IsName("Reload Left Hand Weapon");

        if (inReloadState) progress = Mathf.Clamp01(state.normalizedTime);
        return controller.IsReloading || inReloadState;
    }

    private void OnDisable()
    {
        if (ringRoot != null) ringRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ringRoot != null) Destroy(ringRoot);
        foreach (Texture2D tex in textures)
        {
            if (tex != null) Destroy(tex);
        }
    }
}
