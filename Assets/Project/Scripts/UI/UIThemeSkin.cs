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
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(image);
#endif
    }
}
