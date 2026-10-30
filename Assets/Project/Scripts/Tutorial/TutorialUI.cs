using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 튜토리얼 안내 UI (T2, TutorialDirector가 생성) — 코드 생성, 오버레이 순서 85 (조준점 80 위, 일시정지 메뉴 90 아래)
//   목표 카드(실시간): 왼쪽 위 목숨·드랍률 아래. 단계 표시 + 제목 + 설명 + 할 일 줄([키] 내용 ··· 진행/DONE). 게임은 멈추지 않음
//   정지 팝업: 화면을 멈추고(timeScale 0·소리 정지·조작 끔) 규칙 설명, 확인(Enter) → 다시 진행. 여러 개면 차례로
//     정비 중(이미 timeScale 0)에 열면 시간·조작은 건드리지 않음
//   알림(실시간): 화면 위쪽 가운데 한 줄, 몇 초 뒤 사라짐 (탈출 시작 권장 알림 등)
//   스타일: 일시정지 메뉴와 같은 창 스킨, 카드는 메인 화면 패널처럼 반투명 남색 + 왼쪽 청록 선. 모두 실제 시간 기준
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }
    public static bool IsPopupOpen => Instance != null && Instance.popupOpen;

    public struct ObjectiveItem
    {
        public string key;    // 키 칸 글자 (비우면 칸 없음)
        public string label;
        public ObjectiveItem(string key, string label) { this.key = key; this.label = label; }
    }

    [Header("색")]
    public Color accentColor = new Color(0.35f, 0.85f, 0.95f, 1f);
    public Color cardColor = new Color(0.02f, 0.035f, 0.05f, 0.85f);
    public Color textColor = new Color(0.9f, 0.94f, 0.97f, 1f);
    public Color subTextColor = new Color(0.68f, 0.74f, 0.8f, 1f);
    public Color doneColor = new Color(0.45f, 0.95f, 0.55f, 1f);
    public Color noticeColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("목표 카드 (1920x1080 기준)")]
    public Vector2 cardPosition = new Vector2(32f, -32f); // 왼쪽 위 기준 (튜토리얼은 목숨·드랍률을 숨겨 그 자리 사용)
    public float cardWidth = 500f;

    private Canvas canvas;
    private RectTransform card;
    private CanvasGroup cardGroup;
    private TextMeshProUGUI cardHeader;
    private TextMeshProUGUI cardTitle;
    private TextMeshProUGUI cardDesc;
    private RectTransform itemList;
    private readonly List<Row> rows = new List<Row>();
    private float cardFlashTime = -1f;

    private GameObject popupRoot;
    private RectTransform popupWindow;
    private TextMeshProUGUI popupTitle;
    private TextMeshProUGUI popupBody;
    private TextMeshProUGUI popupButtonLabel;
    private RectTransform popupButton;
    private bool popupOpen;
    private bool pausedByPopup;
    private readonly Queue<PopupRequest> popupQueue = new Queue<PopupRequest>();
    private System.Action popupClosed;

    private TextMeshProUGUI notice;
    private float noticeStart = -1f;
    private float noticeDuration;

    private const float PopupWidth = 860f;
    private const float NoticeFade = 0.3f;

    private class Row
    {
        public RectTransform rect;
        public Image keyBox;
        public TextMeshProUGUI key;
        public TextMeshProUGUI label;
        public TextMeshProUGUI state;
        public bool done;
    }

    private struct PopupRequest
    {
        public string title, body, button;
        public System.Action onClosed;
    }

    public static TutorialUI EnsureCreated()
    {
        if (Instance != null) return Instance;
        return new GameObject("TutorialUI").AddComponent<TutorialUI>();
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
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (pausedByPopup) AudioListener.pause = false;
        Instance = null;
    }

    private void Update()
    {
        if (popupOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) ClosePopup();

        // 정비·결과(timeScale 0, 팝업 때문이 아닌 정지) 중에는 카드 숨김
        bool showCard = card.gameObject.activeSelf && (Time.timeScale > 0f || pausedByPopup);
        float targetAlpha = showCard ? 1f : 0f;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 8f);

        // 단계 완료 깜빡임: 단계 표시를 초록으로 0.8초
        if (cardFlashTime >= 0f)
        {
            float t = Time.unscaledTime - cardFlashTime;
            float blink = Mathf.Abs(Mathf.Sin(t * 12f));
            cardHeader.color = Color.Lerp(accentColor, doneColor, t < 0.8f ? blink : 1f);
        }

        UpdateNotice();
    }

    #region 목표 카드

    // 카드 내용 교체 (header 예: "TRAINING  1 / 6")
    public void ShowObjective(string header, string title, string description, params ObjectiveItem[] items)
    {
        cardFlashTime = -1f;
        cardHeader.text = header;
        cardHeader.color = accentColor;
        cardTitle.text = title;
        cardDesc.text = description;
        cardDesc.gameObject.SetActive(!string.IsNullOrEmpty(description));

        foreach (Row row in rows) Destroy(row.rect.gameObject);
        rows.Clear();
        foreach (ObjectiveItem item in items) rows.Add(CreateRow(item));

        card.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
    }

    public void HideObjective()
    {
        card.gameObject.SetActive(false);
    }

    // 진행 표시 (예: "2 / 3"), 완료 전까지
    public void SetItemProgress(int index, string progress)
    {
        if (index < 0 || index >= rows.Count || rows[index].done) return;
        rows[index].state.text = progress;
    }

    public void SetItemDone(int index)
    {
        if (index < 0 || index >= rows.Count || rows[index].done) return;
        Row row = rows[index];
        row.done = true;
        row.state.text = "DONE";
        row.state.color = doneColor;
        Color dim = subTextColor;
        dim.a = 0.55f;
        row.label.color = dim;
        row.key.color = dim;
        row.keyBox.color = new Color(doneColor.r, doneColor.g, doneColor.b, 0.35f);
    }

    public bool IsItemDone(int index) => index >= 0 && index < rows.Count && rows[index].done;

    public bool AllItemsDone
    {
        get
        {
            foreach (Row row in rows) if (!row.done) return false;
            return true;
        }
    }

    // 단계 완료 표시 (단계 표시 글자를 CLEAR로 바꾸고 초록 깜빡임)
    public void FlashComplete()
    {
        cardHeader.text = cardHeader.text + "   CLEAR";
        cardFlashTime = Time.unscaledTime;
    }

    private Row CreateRow(ObjectiveItem item)
    {
        Row row = new Row();
        GameObject obj = new GameObject("Item", typeof(RectTransform), typeof(LayoutElement));
        row.rect = obj.GetComponent<RectTransform>();
        row.rect.SetParent(itemList, false);
        obj.GetComponent<LayoutElement>().preferredHeight = 38f;

        float x = 0f;
        // 키 칸: 청록 테두리 상자 + 갈무리 글자
        row.keyBox = CreateImage("KeyBox", row.rect, new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));
        Outline outline = row.keyBox.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        row.key = CreateText("Key", row.keyBox.rectTransform, 24f, TextAlignmentOptions.Center, UIFontRole.Heading);
        row.key.text = item.key;
        row.key.color = textColor;
        Stretch(row.key.rectTransform);
        if (!string.IsNullOrEmpty(item.key))
        {
            float keyWidth = Mathf.Max(36f, row.key.GetPreferredValues(item.key).x + 16f);
            PlaceLeft(row.keyBox.rectTransform, new Vector2(0f, 0f), new Vector2(keyWidth, 30f));
            x = keyWidth + 12f;
        }
        else
        {
            row.keyBox.gameObject.SetActive(false);
        }

        row.label = CreateText("Label", row.rect, 22f, TextAlignmentOptions.Left);
        row.label.text = item.label;
        row.label.color = textColor;
        row.label.overflowMode = TextOverflowModes.Ellipsis; // 긴 무기 이름 등이 진행 표시와 겹치지 않게
        PlaceLeft(row.label.rectTransform, new Vector2(x, 0f), new Vector2(cardWidth - 48f - x - 90f, 34f));

        row.state = CreateText("State", row.rect, 24f, TextAlignmentOptions.Right, UIFontRole.Heading);
        row.state.color = subTextColor;
        row.state.rectTransform.anchorMin = row.state.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        row.state.rectTransform.pivot = new Vector2(1f, 0.5f);
        row.state.rectTransform.anchoredPosition = Vector2.zero;
        row.state.rectTransform.sizeDelta = new Vector2(90f, 34f);
        return row;
    }

    #endregion

    #region 정지 팝업

    public void ShowPopup(string title, string body, System.Action onClosed = null, string button = "확인")
    {
        popupQueue.Enqueue(new PopupRequest { title = title, body = body, button = button, onClosed = onClosed });
        if (!popupOpen) OpenNextPopup();
    }

    private void OpenNextPopup()
    {
        if (popupQueue.Count == 0) return;
        PopupRequest request = popupQueue.Dequeue();

        popupTitle.text = request.title;
        popupBody.text = request.body;
        popupButtonLabel.text = $"{request.button}  <size=24><alpha=#88>ENTER</size>";
        popupClosed = request.onClosed;

        // 창 높이 = 본문 높이 + 제목·버튼 자리
        float bodyHeight = popupBody.GetPreferredValues(request.body, PopupWidth - 120f, 0f).y;
        popupBody.rectTransform.sizeDelta = new Vector2(PopupWidth - 120f, bodyHeight);
        popupWindow.sizeDelta = new Vector2(PopupWidth, bodyHeight + 280f);

        // 버튼 폭 = 글자 + 여백 (고정 폭이면 "메인 화면으로 ENTER" 같은 긴 글자가 넘침)
        float labelWidth = popupButtonLabel.GetPreferredValues(popupButtonLabel.text).x;
        popupButton.sizeDelta = new Vector2(Mathf.Max(300f, labelWidth + 72f), popupButton.sizeDelta.y);

        if (!popupOpen)
        {
            popupOpen = true;
            // 전투 중이면 정지 (히트 스톱 등 1이 아닌 값이어도 닫을 때 1로), 정비 중이면 그대로
            pausedByPopup = Time.timeScale > 0f;
            if (pausedByPopup)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(false);
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EnsureEventSystem();
        }
        popupRoot.SetActive(true);
    }

    private void ClosePopup()
    {
        if (!popupOpen) return;
        System.Action closed = popupClosed;
        popupClosed = null;

        if (popupQueue.Count > 0)
        {
            closed?.Invoke();
            OpenNextPopup();
            return;
        }

        popupOpen = false;
        popupRoot.SetActive(false);
        if (pausedByPopup)
        {
            pausedByPopup = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (PlayerStatAndWeaponSync.Instance != null) PlayerStatAndWeaponSync.Instance.SetPlayerControlEnabled(true); // 마우스를 뗀 뒤 켜짐
        }
        closed?.Invoke();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    #endregion

    #region 알림

    public void ShowNotice(string text, float duration = 4f)
    {
        notice.text = text;
        noticeStart = Time.unscaledTime;
        noticeDuration = duration;
        notice.gameObject.SetActive(true);
    }

    private void UpdateNotice()
    {
        if (noticeStart < 0f) return;
        float t = Time.unscaledTime - noticeStart;
        if (t >= noticeDuration)
        {
            noticeStart = -1f;
            notice.gameObject.SetActive(false);
            return;
        }
        float alpha = Mathf.Min(1f, t / NoticeFade, (noticeDuration - t) / NoticeFade);
        Color c = noticeColor;
        c.a = alpha;
        notice.color = c;
        // 나타날 때 살짝 커졌다 돌아옴
        float scale = t < 0.2f ? Mathf.Lerp(1.12f, 1f, t / 0.2f) : 1f;
        notice.rectTransform.localScale = Vector3.one * scale;
    }

    #endregion

    #region 생성

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildCard();
        BuildNotice();
        BuildPopup();
    }

    private void BuildCard()
    {
        GameObject obj = new GameObject("ObjectiveCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card = obj.GetComponent<RectTransform>();
        card.SetParent(transform, false);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = cardPosition;
        card.sizeDelta = new Vector2(cardWidth, 0f);
        Image bg = obj.GetComponent<Image>();
        bg.color = cardColor;
        bg.raycastTarget = false;
        cardGroup = obj.GetComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 20, 16, 18);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = obj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 왼쪽 청록 세로선 (메인 화면 패널과 같은 표식)
        Image edge = CreateImage("AccentEdge", card, accentColor);
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.anchorMin = new Vector2(0f, 0f);
        edgeRect.anchorMax = new Vector2(0f, 1f);
        edgeRect.pivot = new Vector2(0f, 0.5f);
        edgeRect.sizeDelta = new Vector2(3f, 0f);
        edgeRect.anchoredPosition = Vector2.zero;

        cardHeader = CreateText("Header", card, 24f, TextAlignmentOptions.Left, UIFontRole.Heading);
        cardHeader.characterSpacing = 4f;
        cardHeader.color = accentColor;
        cardTitle = CreateText("Title", card, 28f, TextAlignmentOptions.Left);
        cardTitle.fontStyle = FontStyles.Bold;
        cardTitle.color = textColor;
        cardDesc = CreateText("Description", card, 20f, TextAlignmentOptions.Left);
        cardDesc.color = subTextColor;
        cardDesc.textWrappingMode = TextWrappingModes.Normal;
        cardDesc.lineSpacing = 8f;

        GameObject listObj = new GameObject("Items", typeof(RectTransform), typeof(VerticalLayoutGroup));
        itemList = listObj.GetComponent<RectTransform>();
        itemList.SetParent(card, false);
        VerticalLayoutGroup listLayout = listObj.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 6f;
        listLayout.padding = new RectOffset(0, 0, 8, 0);
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        card.gameObject.SetActive(false);
    }

    private void BuildNotice()
    {
        notice = CreateText("Notice", (RectTransform)transform, 28f, TextAlignmentOptions.Center);
        notice.fontStyle = FontStyles.Bold;
        notice.textWrappingMode = TextWrappingModes.Normal;
        notice.outlineWidth = 0.2f;
        notice.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        RectTransform rect = notice.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -290f); // 경고 띠(위에서 200 가운데) 아래
        rect.sizeDelta = new Vector2(1200f, 90f);
        notice.gameObject.SetActive(false);
    }

    private void BuildPopup()
    {
        popupRoot = new GameObject("Popup", typeof(RectTransform));
        RectTransform rootRect = popupRoot.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        Stretch(rootRect);

        Image dim = CreateImage("Dim", rootRect, new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true; // 뒤 화면 클릭 막음
        Stretch(dim.rectTransform);

        GameObject windowObj = new GameObject("Window", typeof(RectTransform), typeof(Image));
        popupWindow = windowObj.GetComponent<RectTransform>();
        popupWindow.SetParent(rootRect, false);
        popupWindow.anchorMin = popupWindow.anchorMax = popupWindow.pivot = new Vector2(0.5f, 0.5f);
        popupWindow.sizeDelta = new Vector2(PopupWidth, 400f);
        windowObj.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.97f);
        UIThemeSkin skin = windowObj.AddComponent<UIThemeSkin>();
        skin.role = UIThemeSkin.Role.Window;
        skin.Apply();

        popupTitle = CreateText("Title", popupWindow, 48f, TextAlignmentOptions.Center, UIFontRole.Heading);
        popupTitle.fontStyle = FontStyles.Bold;
        Place(popupTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(PopupWidth - 80f, 60f));

        popupBody = CreateText("Body", popupWindow, 26f, TextAlignmentOptions.Center);
        popupBody.textWrappingMode = TextWrappingModes.Normal;
        popupBody.lineSpacing = 14f;
        popupBody.color = textColor;
        RectTransform bodyRect = popupBody.rectTransform;
        bodyRect.anchorMin = bodyRect.anchorMax = bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -130f);

        GameObject buttonObj = new GameObject("Confirm", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        popupButton = buttonRect;
        buttonRect.SetParent(popupWindow, false);
        Place(buttonRect, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 64f));
        buttonObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
        UIThemeSkin buttonSkin = buttonObj.AddComponent<UIThemeSkin>();
        buttonSkin.role = UIThemeSkin.Role.Button;
        buttonSkin.Apply();
        popupButtonLabel = CreateText("Label", buttonRect, 36f, TextAlignmentOptions.Center, UIFontRole.Heading);
        popupButtonLabel.fontStyle = FontStyles.Bold;
        Stretch(popupButtonLabel.rectTransform);
        buttonObj.GetComponent<Button>().onClick.AddListener(ClosePopup);

        popupRoot.SetActive(false);
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent, float size, TextAlignmentOptions alignment,
                                              UIFontRole role = UIFontRole.Body)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        UITheme.Current.ApplyFont(text, role);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 부모 왼쪽 가운데 기준
    private static void PlaceLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    #endregion
}
