using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 씬 캔버스 크기 맞춤 통일 (2026-11-03) — 코드로 만드는 캔버스는 UICanvasScaling.Apply를 이미 사용
//   열린 씬 전체(꺼진 오브젝트 포함)의 화면 캔버스(Screen Space) CanvasScaler를 1920x1080 Expand로.
//   제외: 월드 캔버스, 다른 캔버스 안의 캔버스(최상위 캔버스 배율을 따름), JU 프리팹 안의 캔버스(JU 비수정)
//   Undo 가능, 씬 저장은 직접. 대상: GameplayScene(MaintenanceCanvas·CombatHUDCanvas)·MainMenu·ResultScene
public static class CanvasScalerTools
{
    [MenuItem("Tools/TD Shoot/Unify Canvas Scalers In Open Scenes")]
    private static void UnifyCanvasScalers()
    {
        StringBuilder log = new StringBuilder();
        int changed = 0, skipped = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            int before = changed;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                {
                    Canvas canvas = scaler.GetComponent<Canvas>();
                    string path = $"{scene.name}/{GetPath(scaler.transform)}";

                    if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;
                    if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>(true) != null) continue;

                    // 프리팹 인스턴스면 원본 프리팹 에셋 경로 (중첩 프리팹은 가장 안쪽 원본)
                    CanvasScaler source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(scaler);
                    string prefabPath = source != null ? AssetDatabase.GetAssetPath(source) : null;
                    if (!string.IsNullOrEmpty(prefabPath) && prefabPath.Contains("Julhiecio"))
                    {
                        skipped++;
                        log.AppendLine($"  건너뜀 (JU 프리팹): {path}");
                        continue;
                    }

                    if (UICanvasScaling.IsApplied(scaler)) continue;

                    string beforeText = scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                        ? $"{scaler.referenceResolution.x}x{scaler.referenceResolution.y} {scaler.screenMatchMode} {scaler.matchWidthOrHeight}"
                        : $"{scaler.uiScaleMode}";
                    Undo.RecordObject(scaler, "Unify Canvas Scalers");
                    UICanvasScaling.Apply(scaler);
                    EditorUtility.SetDirty(scaler);
                    changed++;
                    log.AppendLine($"  변경: {path} ({beforeText} → 1920x1080 Expand)");
                }
            }

            if (changed > before) EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[Unify Canvas Scalers] 변경 {changed}개, JU 프리팹 건너뜀 {skipped}개 — 씬 저장은 직접\n{log}");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
