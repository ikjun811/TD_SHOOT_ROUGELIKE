using UnityEngine;
using UnityEngine.UI;

// 씬에 이미 있는 UI Image를 UI 테마 스프라이트로 바꿈 (역할만 고르면 됨, 코드 수정 불필요)
// 실행 시(Awake) 적용, 에디터에서는 값을 바꿀 때·컴포넌트 우클릭 > Apply Theme로 미리 보기
// 테마에 해당 스프라이트가 없으면 원래 모습 유지
[RequireComponent(typeof(Image))]
public class UIThemeSkin : MonoBehaviour
{
    public enum Role { Panel, InsetPanel, Window, Tooltip, Bracket, Button }

    public Role role = Role.Panel;
    [Tooltip("스프라이트 고유 색을 그대로 보이게 Image 색을 흰색으로 (끄면 기존 색으로 물들임)")]
    public bool resetColor = true;
    [Tooltip("Button 역할: 안의 글자 색을 테마 buttonTextColor로")]
    public bool applyLabelColor = true;

    private void Awake()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate 안에서 다른 컴포넌트를 바로 바꾸면 경고가 나므로 다음 에디터 갱신 때 적용
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
    }
#endif

    [ContextMenu("Apply Theme")]
    public void Apply()
    {
        UITheme theme = UITheme.Current;
        Image image = GetComponent<Image>();
        if (theme == null || image == null) return;

#if UNITY_EDITOR
        // 바뀐 게 있을 때만 씬 변경 표시 (매번 표시하면 저장 직후에도 변경 있음으로 뜸)
        string before = Snapshot(image);
#endif

        Sprite sprite = role switch
        {
            Role.Panel => theme.panelSprite,
            Role.InsetPanel => theme.insetPanelSprite,
            Role.Window => theme.windowSprite,
            Role.Tooltip => theme.tooltipSprite,
            Role.Bracket => theme.bracketSprite,
            Role.Button => theme.buttonNormal,
            _ => null
        };
        if (!theme.ApplySprite(image, sprite)) return;

        if (resetColor) image.color = Color.white;

        // 버튼: 상태별 스프라이트 교체 (에셋 Button 프리팹과 같은 Sprite Swap 방식)
        if (role == Role.Button)
        {
            Selectable selectable = GetComponent<Selectable>();
            if (selectable != null)
            {
                selectable.transition = Selectable.Transition.SpriteSwap;
                selectable.targetGraphic = image;
                SpriteState state = selectable.spriteState;
                state.highlightedSprite = theme.buttonHover;
                state.selectedSprite = theme.buttonHover;
                state.pressedSprite = theme.buttonPressed;
                state.disabledSprite = theme.buttonDisabled;
                selectable.spriteState = state;
            }

            if (applyLabelColor)
            {
                foreach (TMPro.TMP_Text label in GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (label.color == theme.buttonTextColor) continue;
                    label.color = theme.buttonTextColor;
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(label);
#endif
                }
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && Snapshot(image) != before)
        {
            UnityEditor.EditorUtility.SetDirty(image);
            Selectable changed = GetComponent<Selectable>();
            if (changed != null) UnityEditor.EditorUtility.SetDirty(changed);
        }
#endif
    }

#if UNITY_EDITOR
    private string Snapshot(Image image)
    {
        string text = $"{N(image.sprite)}|{image.type}|{image.pixelsPerUnitMultiplier}|{image.color}";
        Selectable selectable = GetComponent<Selectable>();
        if (selectable != null)
        {
            SpriteState s = selectable.spriteState;
            text += $"|{selectable.transition}|{N(selectable.targetGraphic)}|{N(s.highlightedSprite)}|{N(s.selectedSprite)}|{N(s.pressedSprite)}|{N(s.disabledSprite)}";
        }
        return text;
    }

    private static string N(Object o) => o == null ? "-" : o.name;
#endif
}
