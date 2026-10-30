using UnityEditor;

// 튜토리얼 확인용 에디터 메뉴
//   Tools > TD Shoot > Tutorial Test Mode: 켜면 GameplayScene을 바로 실행해도 튜토리얼 모드로 시작 (메인 화면을 거칠 필요 없음)
//   본 게임을 확인할 때는 꺼 둘 것 (메뉴에 체크 표시)
public static class TutorialTools
{
    private const string MenuPath = "Tools/TD Shoot/Tutorial Test Mode";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(GameSession.EditorTutorialTestKey, false);
        EditorPrefs.SetBool(GameSession.EditorTutorialTestKey, on);
        UnityEngine.Debug.Log($"Tutorial Test Mode: {(on ? "켜짐 — GameplayScene 실행 시 튜토리얼" : "꺼짐")}");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(GameSession.EditorTutorialTestKey, false));
        return true;
    }
}
