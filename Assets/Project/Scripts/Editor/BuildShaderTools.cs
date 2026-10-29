using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 빌드 점검 (2026-10-29): 실행 중 코드로 만드는 재질(Shader.Find + 키워드)은 빌드에서 셰이더·변형이 빠질 수 있음
//   빌드는 프로젝트 재질이 쓰는 셰이더·키워드 조합만 남기므로, 같은 설정의 기준 재질을 Resources에 두어 함께 포함시킴
//   Prepare Build Shader References: Assets/Project/Resources/BuildShaderRefs/ 에 기준 재질 생성(있으면 덮어씀) + 빌드 씬 목록 로그
//     - Particles/Lit 반투명 (AtmosphereLighting 바닥 안개 — 프로젝트에 이 조합 재질이 없으면 빌드에서 빠져 안개가 Sprites/Default로 대체됨)
//     - Lit + _EMISSION (BspArenaGenerator 격자 바닥, DroppedItemVisual 모듈 블록)
//     - Lit + _NORMALMAP (BspArenaGenerator 텍스처 바닥)
//   TDShoot/* 셰이더는 Resources/Shaders에 있어 자동 포함, Sprites/Default는 기본 포함 목록에 있음
public static class BuildShaderTools
{
    private const string Folder = "Assets/Project/Resources/BuildShaderRefs";

    [MenuItem("Tools/TD Shoot/Prepare Build Shader References")]
    public static void Prepare()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        int created = 0;
        Shader particlesLit = Shader.Find("Universal Render Pipeline/Particles/Lit");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        if (particlesLit != null)
        {
            // AtmosphereLighting.GetFogMaterial과 같은 설정
            Material fog = new Material(particlesLit);
            fog.SetFloat("_Surface", 1f);
            fog.SetFloat("_Blend", 0f);
            fog.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            fog.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            fog.SetFloat("_ZWrite", 0f);
            fog.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fog.SetOverrideTag("RenderType", "Transparent");
            fog.renderQueue = (int)RenderQueue.Transparent;
            Save(fog, "Ref_ParticlesLit_Transparent");
            created++;
        }
        else Debug.LogWarning("[BuildShaderTools] Universal Render Pipeline/Particles/Lit 셰이더를 찾지 못했습니다.");

        if (lit != null)
        {
            Material emission = new Material(lit);
            emission.EnableKeyword("_EMISSION");
            emission.SetColor("_EmissionColor", Color.white);
            emission.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Save(emission, "Ref_Lit_Emission");

            Material normal = new Material(lit);
            normal.EnableKeyword("_NORMALMAP");
            Save(normal, "Ref_Lit_NormalMap");
            created += 2;
        }
        else Debug.LogWarning("[BuildShaderTools] Universal Render Pipeline/Lit 셰이더를 찾지 못했습니다.");

        AssetDatabase.SaveAssets();

        // 빌드 씬 목록 (메인 → 전투 → 결과 순서 확인용)
        System.Text.StringBuilder scenes = new System.Text.StringBuilder();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            scenes.Append(scene.enabled ? "  [O] " : "  [ ] ").AppendLine(scene.path);
        }
        Debug.Log($"[BuildShaderTools] 기준 재질 {created}개 → {Folder}\n빌드 씬 목록:\n{scenes}");
    }

    private static void Save(Material material, string name)
    {
        material.name = name;
        string path = $"{Folder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            existing.renderQueue = material.renderQueue;
            existing.SetOverrideTag("RenderType", material.GetTag("RenderType", false));
            existing.globalIlluminationFlags = material.globalIlluminationFlags;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            return;
        }
        AssetDatabase.CreateAsset(material, path);
    }
}
