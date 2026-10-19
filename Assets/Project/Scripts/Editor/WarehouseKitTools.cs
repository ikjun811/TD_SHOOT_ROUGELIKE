using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// SciFi Warehouse Kit 맵 적용 준비 (W2)
//   Measure Warehouse Kit Sizes: Prefabs 폴더의 모든 프리팹 크기·기준점(pivot) 위치를 메시 기준으로 계산해
//   프로젝트/Logs/WarehouseKitSizes.txt에 저장 (Assets 밖이라 .meta 없음) + 콘솔에 요약
public static class WarehouseKitTools
{
    private const string PrefabFolder = "Assets/SciFi Warehouse Kit/Prefabs";

    [MenuItem("Tools/TD Shoot/Measure Warehouse Kit Sizes")]
    public static void MeasureSizes()
    {
        Measure(PrefabFolder, "WarehouseKitSizes.txt");
    }

    // Project 창에서 선택한 폴더의 프리팹 측정 (다른 에셋 점검용) → Logs/PrefabSizes_폴더이름.txt
    [MenuItem("Tools/TD Shoot/Measure Prefab Sizes In Selected Folder")]
    public static void MeasureSelectedFolder()
    {
        string folder = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : "";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Measure Prefab Sizes", "Project 창에서 폴더를 선택하세요.", "확인");
            return;
        }
        Measure(folder, $"PrefabSizes_{Path.GetFileName(folder)}.txt");
    }

    private static void Measure(string prefabFolder, string fileName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# path | size (x,y,z) | min (x,y,z) | max (x,y,z) | renderers | colliders | root scale");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 메시 바운드를 프리팹 루트 기준 좌표로 합침 (루트 위치·회전은 무시, 루트 스케일은 포함)
            Matrix4x4 rootInverse = Matrix4x4.TRS(prefab.transform.position, prefab.transform.rotation, Vector3.one).inverse;
            bool has = false;
            Bounds total = new Bounds();
            int rendererCount = 0;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                rendererCount++;
                Matrix4x4 m = rootInverse * mf.transform.localToWorldMatrix;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!has) { total = new Bounds(p, Vector3.zero); has = true; }
                    else total.Encapsulate(p);
                }
            }

            int colliderCount = prefab.GetComponentsInChildren<Collider>(true).Length;
            string name = path.Substring(prefabFolder.Length + 1);
            if (!has)
            {
                sb.AppendLine($"{name} | (no mesh) | | | 0 | {colliderCount} | {F(prefab.transform.localScale)}");
                continue;
            }
            sb.AppendLine($"{name} | {F(total.size)} | {F(total.min)} | {F(total.max)} | {rendererCount} | {colliderCount} | {F(prefab.transform.localScale)}");
        }

        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, fileName);
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[PrefabSizes] {guids.Length}개 측정 → {file}\n{sb}");
    }

    private static string F(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
}
