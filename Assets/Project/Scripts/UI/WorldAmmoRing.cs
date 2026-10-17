using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JUTPS;
using JUTPS.WeaponSystem;

// 캐릭터 화면 오른쪽 원형 장탄 링 (플레이어에 부착)
// 사격 시 링이 줄어들고, 재장전 중에는 장전 애니메이션 진행도에 맞춰 차오름
// 링 스프라이트는 런타임에 생성 (에셋 불필요). 쌍권총은 양손 탄을 합산
[RequireComponent(typeof(JUCharacterController))]
public class WorldAmmoRing : MonoBehaviour
{
    [Header("Position")]
    public float sideOffset = 1.0f;     // 화면 기준 오른쪽 거리 (m)
    public float heightOffset = 1.2f;   // 발밑 기준 높이 (m)
    public float ringSize = 0.6f;       // 링 지름 (m)

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color lowAmmoColor = new Color(1f, 0.3f, 0.3f);
    public Color reloadColor = new Color(0.3f, 0.85f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
    [Range(0f, 1f)] public float lowAmmoThreshold = 0.25f;

    private const int TextureSize = 128;
    private const float InnerRadiusRatio = 0.72f; // 링 두께 (안쪽 반지름 / 바깥 반지름)

    private JUCharacterController controller;
    private Camera mainCamera;

    private GameObject ringRoot;
    private Image fillImage;
    private TextMeshProUGUI ammoText;

    private void Awake()
    {
        controller = GetComponent<JUCharacterController>();
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

        Sprite ringSprite = CreateRingSprite();

        Image background = CreateImage("Background", rootRect, ringSprite, backgroundColor);
        background.type = Image.Type.Simple;

        fillImage = CreateImage("Fill", rootRect, ringSprite, normalColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;

        GameObject textObj = new GameObject("AmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        ammoText = textObj.GetComponent<TextMeshProUGUI>();
        ammoText.alignment = TextAlignmentOptions.Center;
        ammoText.fontSize = 36f;
        ammoText.fontStyle = FontStyles.Bold;
        ammoText.textWrappingMode = TextWrappingModes.NoWrap;
        ammoText.raycastTarget = false;
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

    // 흰색 도넛 모양 텍스처 생성 (가장자리 1픽셀 안티앨리어싱)
    private Sprite CreateRingSprite()
    {
        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (TextureSize - 1) * 0.5f;
        float outer = TextureSize * 0.5f - 1f;
        float inner = outer * InnerRadiusRatio;

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(outer - dist + 0.5f) * Mathf.Clamp01(dist - inner + 0.5f);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
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

        float ammoRatio = magazine > 0 ? Mathf.Clamp01((float)ammo / magazine) : 0f;

        if (TryGetReloadProgress(out float reloadProgress))
        {
            // 재장전: 남은 양에서 가득 찰 때까지 장전 진행도에 맞춰 차오름
            fillImage.fillAmount = Mathf.Lerp(ammoRatio, 1f, reloadProgress);
            fillImage.color = reloadColor;
            ammoText.color = reloadColor;
        }
        else
        {
            fillImage.fillAmount = ammoRatio;
            Color c = ammoRatio <= lowAmmoThreshold ? lowAmmoColor : normalColor;
            fillImage.color = c;
            ammoText.color = c;
        }
        ammoText.text = ammo.ToString();

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
    }
}
