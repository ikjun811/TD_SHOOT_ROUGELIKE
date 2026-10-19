using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 긴 글 스크롤: TextMeshProUGUI에 붙이면 실행 시 같은 자리에 스크롤 영역(마우스 휠·드래그·세로 스크롤바)을 만들고 글을 그 안으로 옮김
// 글 높이는 내용에 맞춰 늘어나고, 영역보다 길 때만 스크롤바 표시. 패널을 열 때마다 맨 위부터
[RequireComponent(typeof(TextMeshProUGUI))]
public class ScrollableText : MonoBehaviour
{
    [SerializeField] private float scrollSensitivity = 40f;
    [SerializeField] private float scrollbarWidth = 10f;
    [SerializeField] private Color scrollbarTrackColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color scrollbarHandleColor = new Color(64f / 255f, 208f / 255f, 236f / 255f, 0.8f);

    private ScrollRect scroll;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void Build()
    {
        RectTransform textRect = (RectTransform)transform;
        Transform parent = textRect.parent;

        // 스크롤 영역 = 글이 있던 자리 그대로
        GameObject viewObj = new GameObject(name + "_Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform view = viewObj.GetComponent<RectTransform>();
        view.SetParent(parent, false);
        view.SetSiblingIndex(textRect.GetSiblingIndex());
        view.anchorMin = textRect.anchorMin;
        view.anchorMax = textRect.anchorMax;
        view.pivot = textRect.pivot;
        view.anchoredPosition = textRect.anchoredPosition;
        view.sizeDelta = textRect.sizeDelta;
        viewObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // 보이지 않지만 휠·드래그를 받음

        // 글: 위쪽 고정, 높이는 내용에 맞춤 (오른쪽은 스크롤바 자리만큼 비움)
        textRect.SetParent(view, false);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(-(scrollbarWidth + 12f), 0f);
        ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 세로 스크롤바 (오른쪽)
        GameObject barObj = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform bar = barObj.GetComponent<RectTransform>();
        bar.SetParent(view, false);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(scrollbarWidth, 0f);
        barObj.GetComponent<Image>().color = scrollbarTrackColor;

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        RectTransform handle = handleObj.GetComponent<RectTransform>();
        handle.SetParent(bar, false);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;

        Scrollbar scrollbar = barObj.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        scroll = viewObj.GetComponent<ScrollRect>();
        scroll.content = textRect;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = scrollSensitivity;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalNormalizedPosition = 1f;
    }
}
