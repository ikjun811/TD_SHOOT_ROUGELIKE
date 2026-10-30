using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 획득 알림 (C3) — DroppedItem이 아이템을 획득할 때 호출, 첫 호출 시 자동 생성 (씬 연결 불필요)
//   화면 왼쪽 아래에 "[레어도] 이름"을 레어도 색으로 표시, 새 알림이 아래에 쌓이고 최대 maxEntries줄
//   표시 시간·사라짐은 실제 시간 기준 (탈출 시 한꺼번에 흡수돼도 정상 표시), 정비·결과 화면(timeScale 0)에서는 숨김
//   픽셀 스킨 (HUD와 통일, 사용자 채택): 줄 배경 = 테마 레어도 슬롯 테두리(정비 카드와 같은 그림), 없으면 어두운 상자 + 왼쪽 색 막대
public class PickupNoticeUI : MonoBehaviour
{
    private static PickupNoticeUI instance;

    public int maxEntries = 6;
    public float showTime = 2.5f;
    public float fadeTime = 0.4f;
    public float fontSize = 26f;
    public Vector2 anchoredPosition = new Vector2(40f, 300f); // 화면 왼쪽 아래 기준 (1920x1080)
    public float framePixelScale = 2f;                        // 레어도 테두리 픽셀 배율
    [Range(0f, 1f)] public float frameAlpha = 0.92f;

    private Canvas canvas;
    private RectTransform container;

    private class Entry
    {
        public GameObject root;
        public CanvasGroup group;
        public float endTime;
    }

    private readonly List<Entry> entries = new List<Entry>();

    public static void ShowModule(ModuleDataSO module)
    {
        if (module == null) return;
        bool unique = module.category == ModuleCategory.Unique;
        string label = unique ? "유니크" : ItemTooltipUI.GetRarityName(module.rarity);
        Show($"[{label}] {StripRaritySuffix(module.moduleName)}", UITheme.Current.GetRarityColor(module.rarity), module.rarity, module.rarity >= RarityType.Legendary || unique);
    }

    // 모듈 이름 끝의 "(희귀)" 등 레어도 괄호 제거 — 앞의 [레어도]와 두 번 겹쳐 보이던 문제 (에셋 이름은 그대로)
    private static string StripRaritySuffix(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.EndsWith(")")) return name;
        int open = name.LastIndexOf('(');
        if (open <= 0) return name;
        string inner = name.Substring(open + 1, name.Length - open - 2).Trim();
        bool isRarity = inner == "유니크";
        foreach (RarityType rarity in System.Enum.GetValues(typeof(RarityType)))
        {
            if (inner == ItemTooltipUI.GetRarityName(rarity)) isRarity = true;
        }
        return isRarity ? name.Substring(0, open).TrimEnd() : name;
    }

    public static void ShowWeapon(WeaponDataSO weapon)
    {
        if (weapon == null) return;
        Show($"[{ItemTooltipUI.GetRarityName(weapon.rarity)}] {weapon.weaponName} (무기)", weapon.GetRarityColor(), weapon.rarity, weapon.rarity >= RarityType.Legendary);
    }

    // 아이템이 아닌 획득 알림 (의료 상자 회복 등) — 일반 테두리 + 지정 색, 굵게
    public static void ShowMessage(string text, Color color)
    {
        Show(text, color, RarityType.Common, true);
    }

    private static void Show(string text, Color color, RarityType rarity, bool emphasize)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PickupNoticeUI");
            instance = go.AddComponent<PickupNoticeUI>();
        }
        instance.AddEntry(text, color, rarity, emphasize);
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject containerObj = new GameObject("Entries", typeof(RectTransform));
        container = containerObj.GetComponent<RectTransform>();
        container.SetParent(transform, false);
        container.anchorMin = container.anchorMax = Vector2.zero;
        container.pivot = Vector2.zero;
        container.anchoredPosition = anchoredPosition;
        container.sizeDelta = new Vector2(700f, 400f);

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.spacing = 6f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void AddEntry(string text, Color color, RarityType rarity, bool emphasize)
    {
        while (entries.Count >= maxEntries)
        {
            Destroy(entries[0].root);
            entries.RemoveAt(0);
        }

        float size = emphasize ? fontSize * 1.15f : fontSize;
        float height = size + 22f; // 테두리(픽셀 3 x 배율 2) 안에 글자가 들어가도록

        // 배경 (어두운 반투명) + 왼쪽 레어도 색 막대 + 글자
        GameObject root = new GameObject("Notice", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(container, false);
        rect.pivot = new Vector2(0f, 0.5f);
        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.05f, 0.08f, emphasize ? 0.8f : 0.6f);
        bg.raycastTarget = false;
        bool framed = UITheme.Current.ApplySprite(bg, UITheme.Current.GetRaritySlotSprite(rarity), framePixelScale);
        if (framed) bg.color = new Color(1f, 1f, 1f, frameAlpha);

        HorizontalLayoutGroup row = root.GetComponent<HorizontalLayoutGroup>();
        row.padding = framed ? new RectOffset(14, 16, 0, 0) : new RectOffset(0, 14, 0, 0);
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        rect.sizeDelta = new Vector2(0f, height);

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(root.transform, false);
        Image barImage = bar.GetComponent<Image>();
        barImage.color = color;
        barImage.raycastTarget = false;
        LayoutElement barLayout = bar.GetComponent<LayoutElement>();
        barLayout.preferredWidth = emphasize ? 8f : 5f;
        if (framed) bar.SetActive(false); // 테두리 색이 레어도를 보여 줌

        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = size;
        tmp.fontStyle = emphasize ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        entries.Add(new Entry
        {
            root = root,
            group = root.GetComponent<CanvasGroup>(),
            endTime = Time.unscaledTime + showTime
        });
    }

    private void Update()
    {
        bool visible = Time.timeScale > 0f;
        if (canvas.enabled != visible) canvas.enabled = visible;

        float now = Time.unscaledTime;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            float remaining = entry.endTime - now;
            if (remaining <= -fadeTime)
            {
                Destroy(entry.root);
                entries.RemoveAt(i);
                continue;
            }
            entry.group.alpha = remaining >= 0f ? 1f : 1f + remaining / fadeTime;
        }
    }
}
