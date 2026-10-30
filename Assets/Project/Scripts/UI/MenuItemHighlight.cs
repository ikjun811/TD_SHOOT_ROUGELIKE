using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 테두리 없는 글자 메뉴 항목 (메인 화면 A안 "글자 목록", 사용자 채택 2026-10-30)
//   평소 흐린 흰색, 마우스를 올리면 청록으로 밝아지며 왼쪽 막대가 세로로 펼쳐지고 글자가 살짝 오른쪽으로
//   비활성(튜토리얼 준비 중 등)은 아주 흐리게. 실제 시간 기준 (timeScale 무관)
//   Tools > TD Shoot > Restyle Main Menu (Minimal)이 버튼에 붙이고 label·bar를 연결함
[RequireComponent(typeof(Selectable))]
public class MenuItemHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label;
    public Graphic bar;
    public Color normalColor = new Color(0.72f, 0.78f, 0.84f);
    public Color hoverColor = new Color(0.45f, 0.9f, 1f);
    public Color disabledColor = new Color(0.4f, 0.43f, 0.47f, 0.6f);
    [Tooltip("마우스를 올렸을 때 글자가 오른쪽으로 밀리는 거리")]
    public float hoverShift = 14f;
    [Tooltip("전환 속도 (1/초)")]
    public float speed = 12f;

    private Selectable selectable;
    private Vector2 labelBasePosition;
    private bool hovered;
    private float amount; // 0 평소 → 1 강조
    private bool lastInteractable = true;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (label != null) labelBasePosition = label.rectTransform.anchoredPosition;
        Apply();
    }

    private void OnDisable()
    {
        hovered = false;
        amount = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        bool interactable = selectable.IsInteractable();
        float target = hovered && interactable ? 1f : 0f;
        // 비활성 전환(MainMenuUI가 Awake 순서와 무관하게 끌 수 있음)도 반영
        if (Mathf.Approximately(amount, target) && interactable == lastInteractable) return;
        lastInteractable = interactable;
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * speed);
        Apply();
    }

    private void Apply()
    {
        float eased = 1f - (1f - amount) * (1f - amount);
        bool interactable = selectable == null || selectable.IsInteractable();

        if (label != null)
        {
            label.color = interactable ? Color.Lerp(normalColor, hoverColor, eased) : disabledColor;
            label.rectTransform.anchoredPosition = labelBasePosition + Vector2.right * hoverShift * eased;
        }

        if (bar != null)
        {
            bar.enabled = eased > 0.01f;
            Color c = hoverColor;
            c.a = eased;
            bar.color = c;
            bar.rectTransform.localScale = new Vector3(1f, eased, 1f);
        }
    }
}
