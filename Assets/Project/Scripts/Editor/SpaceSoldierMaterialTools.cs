using System.IO;
using UnityEditor;
using UnityEngine;

// 플레이어 모델(SciFi_Space_Soldier) 재질을 URP/Lit으로 변환 (C1)
//   원본 셰이더(Sci-Fi Space Station/PBR Basic)는 Built-in 전용이라 URP에서 분홍색 → 원본은 두고 URP/Lit 사본을 만듦
//   변형 A = 흰색 슈트 (색 마스크 없이 원래 텍스처 그대로, B 파랑·C 빨강은 마스크로 칠한 것) → A의 텍스처를 그대로 연결
//   결과: Assets/Project/Characters/SpaceSoldier/Materials/M_Player_Head·Torso·Legs.mat (다시 실행하면 덮어씀)
public static class SpaceSoldierMaterialTools
{
    private const string SourceFolder = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Materials";
    private const string OutputFolder = "Assets/Project/Characters/SpaceSoldier/Materials";
    private static readonly string[] Parts = { "Head", "Torso", "Legs" };
    private const string RifleSourcePath = "Assets/SciFi_Space_Soldier/Resources/Weapons/Rifle/Materials/M_Weapon_Rifle_A.mat";
    public const string RifleOutputPath = OutputFolder + "/M_Enemy_Rifle.mat";

    [MenuItem("Tools/TD Shoot/Convert Space Soldier Materials")]
    public static void Convert()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            EditorUtility.DisplayDialog("Space Soldier", "URP/Lit 셰이더를 찾지 못했습니다.", "확인");
            return;
        }

        EnsureFolder(OutputFolder);

        // 몸 3부위 + 소총 (슈터 적용, P3 — 같은 원본 셰이더라 같은 방식으로 변환)
        System.Collections.Generic.List<(string source, string output)> jobs = new System.Collections.Generic.List<(string, string)>();
        foreach (string part in Parts) jobs.Add(($"{SourceFolder}/M_Soldier_A_{part}.mat", $"{OutputFolder}/M_Player_{part}.mat"));
        jobs.Add((RifleSourcePath, RifleOutputPath));

        int done = 0;
        foreach ((string sourcePath, string matPath) in jobs)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"[SpaceSoldier] {sourcePath} 없음 — 건너뜀");
                continue;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else mat.shader = lit;

            // 알베도
            mat.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            mat.SetColor("_BaseColor", source.GetColor("_Color"));

            // 노멀
            Texture normal = source.GetTexture("_BumpMap");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", source.GetFloat("_BumpScale"));
            SetKeyword(mat, "_NORMALMAP", normal != null);

            // 메탈릭(R)·광택(A): 원본과 같은 채널 → URP는 맵이 있으면 메탈릭 = 맵 R, 매끄러움 = 맵 A x _Smoothness
            Texture metallic = source.GetTexture("_MetallicGlossMap");
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            mat.SetFloat("_Smoothness", Mathf.Clamp01(source.GetFloat("_Glossiness")));
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            SetKeyword(mat, "_METALLICSPECGLOSSMAP", metallic != null);

            // 발광 (원본: 맵 x 색 x 배율)
            Texture emission = source.GetTexture("_EmissionMap");
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", emission != null ? source.GetColor("_EmissionColor") * source.GetFloat("_EmissionFactor") : Color.black);
            SetKeyword(mat, "_EMISSION", emission != null);
            mat.globalIlluminationFlags = emission != null ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            EditorUtility.SetDirty(mat);
            done++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SpaceSoldier] 재질 {done}개 변환 (변형 A 흰색) → {OutputFolder}");
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(OutputFolder);
        if (folder != null) EditorGUIUtility.PingObject(folder);
    }

    private static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
