using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 메인 화면 3D 배경 배치 (MainMenuBackdrop)
//   Tools > TD Shoot > Add Arena Backdrop To Main Menu: 열린 MainMenu 씬에 "ArenaBackdrop" 생성(있으면 갱신)
//   - 전투 씬(GameplayScene)의 BspArenaGenerator 설정(바닥·벽·장애물 프리팹 등)을 그대로 복사, 시작 시 생성·시드 무작위
//   - 전투 씬 메인 카메라 각도를 MainMenuBackdrop에 기록 (게임과 같은 시점)
//   Tools > TD Shoot > Add Arena Backdrop To Result Scene: 열린 ResultScene에 같은 배경 (R3 — 결과에 따른 조명, 화면 전체 어둡게)
//   전투 씬 설정을 바꿨다면 다시 실행. Undo 가능, 씬 저장은 직접
public static class MainMenuBackdropTools
{
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string BackdropName = "ArenaBackdrop";

    [MenuItem("Tools/TD Shoot/Add Arena Backdrop To Main Menu")]
    public static void AddBackdrop()
    {
        MainMenuUI menuUI = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menuUI == null)
        {
            EditorUtility.DisplayDialog("Main Menu", "열린 씬에서 MainMenuUI를 찾지 못했습니다. MainMenu 씬을 열어 주세요.", "확인");
            return;
        }
        AddTo(menuUI.gameObject.scene, null);
    }

    // 결과 화면(R3): 같은 배경 + 결과에 따른 조명(실패 비상등·클리어 흰빛), 초점 가운데, 화면 전체 고르게 어둡게, 조금 느리게
    [MenuItem("Tools/TD Shoot/Add Arena Backdrop To Result Scene")]
    public static void AddBackdropToResult()
    {
        ResultSceneUI resultUI = Object.FindAnyObjectByType<ResultSceneUI>(FindObjectsInactive.Include);
        if (resultUI == null)
        {
            EditorUtility.DisplayDialog("Result Scene", "열린 씬에서 ResultSceneUI를 찾지 못했습니다. ResultScene을 열어 주세요.", "확인");
            return;
        }
        AddTo(resultUI.gameObject.scene, view =>
        {
            view.FindProperty("moodFromResult").boolValue = true;
            view.FindProperty("focusScreenOffset").floatValue = 0f;
            view.FindProperty("panSpeed").floatValue = 1f;
            view.FindProperty("shadeAlpha").floatValue = 0.55f;
            view.FindProperty("shadeSolidUntil").floatValue = 1f;
            view.FindProperty("shadeFadeUntil").floatValue = 1f;
            view.FindProperty("overallShade").floatValue = 0.55f;
            // 결과별 배경 연출: 클리어 = 위로 빠져나가기, 실패 = 감시 카메라
            GameObject backdrop = ((Component)view.targetObject).gameObject;
            if (backdrop.GetComponent<ResultBackdropEffects>() == null) Undo.AddComponent<ResultBackdropEffects>(backdrop);
        });
    }

    private static void AddTo(Scene menuScene, System.Action<SerializedObject> configure)
    {

        // 전투 씬: 이미 열려 있으면 그대로, 아니면 추가로 열었다가 닫음
        Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
        bool openedHere = false;
        if (!gameplayScene.isLoaded)
        {
            gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        BspArenaGenerator source = null;
        Camera sourceCamera = null;
        foreach (GameObject root in gameplayScene.GetRootGameObjects())
        {
            if (source == null) source = root.GetComponentInChildren<BspArenaGenerator>(true);
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
            {
                if (sourceCamera == null && c.CompareTag("MainCamera")) sourceCamera = c;
            }
        }

        if (source == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(gameplayScene, true);
            EditorUtility.DisplayDialog("Main Menu", "GameplayScene에서 BspArenaGenerator를 찾지 못했습니다.", "확인");
            return;
        }

        SceneManager.SetActiveScene(menuScene);
        GameObject backdrop = null;
        foreach (GameObject root in menuScene.GetRootGameObjects())
        {
            if (root.name == BackdropName) { backdrop = root; break; }
        }
        if (backdrop == null)
        {
            backdrop = new GameObject(BackdropName);
            SceneManager.MoveGameObjectToScene(backdrop, menuScene);
            Undo.RegisterCreatedObjectUndo(backdrop, "Add Arena Backdrop");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(backdrop, "Add Arena Backdrop");
        }
        backdrop.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

        BspArenaGenerator generator = backdrop.GetComponent<BspArenaGenerator>();
        if (generator == null) generator = Undo.AddComponent<BspArenaGenerator>(backdrop);
        EditorUtility.CopySerialized(source, generator);
        SerializedObject genSo = new SerializedObject(generator);
        genSo.FindProperty("seed").intValue = 0;
        genSo.FindProperty("generateOnStart").boolValue = true;
        genSo.ApplyModifiedProperties();

        MainMenuBackdrop view = backdrop.GetComponent<MainMenuBackdrop>();
        if (view == null) view = Undo.AddComponent<MainMenuBackdrop>(backdrop);
        string angleNote = "기본값 유지";
        SerializedObject viewSo = new SerializedObject(view);
        if (sourceCamera != null)
        {
            viewSo.FindProperty("cameraEuler").vector3Value = sourceCamera.transform.eulerAngles;
            angleNote = sourceCamera.transform.eulerAngles.ToString("0.0");
        }
        configure?.Invoke(viewSo);
        viewSo.ApplyModifiedProperties();

        if (openedHere) EditorSceneManager.CloseScene(gameplayScene, true);
        EditorSceneManager.MarkSceneDirty(menuScene);
        Selection.activeGameObject = backdrop;
        Debug.Log($"[MainMenuBackdrop] {BackdropName} 배치 — 전투 씬 맵 생성기 설정 복사, 카메라 각도 {angleNote}. 씬 저장 후 실행해 확인");
    }
}
