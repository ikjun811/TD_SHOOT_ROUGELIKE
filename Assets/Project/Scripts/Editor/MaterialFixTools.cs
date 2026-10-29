using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2D 셰이더로 잘못 변환된 재질 고치기 (2026-10-29) — Horrors(pack)·SciFi Warehouse Kit 등 75개가
//   'Universal Render Pipeline/2D/Mesh2D-Lit-Default'(2D 렌더러 전용)로 되어 있어 3D 조명을 받지 않음 → 어둠 속에서 혼자 밝게 뜸
//   Fix 2D-Lit Materials To URP Lit: 프로젝트 전체에서 그 셰이더 재질을 찾아 URP Lit으로 바꾸고,
//   파일에 남아 있는 원래 Standard 값(_MainTex·_Color·_BumpMap·_MetallicGlossMap·_Glossiness/_GlossMapScale·_OcclusionMap·
//   _EmissionMap/_EmissionColor·_Mode 불투명/컷아웃/페이드/투명)을 읽어 옮김. 재질 파일 자체를 수정 (git으로 되돌리기 가능)
public static class MaterialFixTools
{
    private const string BrokenShader = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";
    private const string LitShader = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/TD Shoot/Fix 2D-Lit Materials To URP Lit")]
    public static void Fix()
    {
        Shader lit = Shader.Find(LitShader);
        if (lit == null)
        {
            EditorUtility.DisplayDialog("Material Fix", "URP Lit 셰이더를 찾지 못했습니다.", "확인");
            return;
        }

        List<Material> targets = new List<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null && mat.shader != null && mat.shader.name == BrokenShader) targets.Add(mat);
        }
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Material Fix", "2D 셰이더 재질이 없습니다.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("Material Fix",
                $"2D 전용 셰이더 재질 {targets.Count}개를 URP Lit으로 바꿉니다 (재질 파일 수정, git으로 되돌리기 가능). 진행할까요?",
                "진행", "취소")) return;

        int done = 0;
        foreach (Material mat in targets)
        {
            Convert(mat, lit);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MaterialFixTools] 재질 {done}개를 URP Lit으로 변환했습니다.");
    }

    private static void Convert(Material mat, Shader lit)
    {
        // 셰이더를 바꾸기 전에 파일에 남은 원래 값 읽기 (셰이더에 없는 속성은 Material API로 못 읽음)
        SerializedObject so = new SerializedObject(mat);
        Texture mainTex = ReadTexture(so, "_MainTex", out Vector2 mainScale, out Vector2 mainOffset);
        Texture bump = ReadTexture(so, "_BumpMap", out _, out _);
        Texture metallicMap = ReadTexture(so, "_MetallicGlossMap", out _, out _);
        Texture occlusion = ReadTexture(so, "_OcclusionMap", out _, out _);
        Texture emissionMap = ReadTexture(so, "_EmissionMap", out _, out _);
        Color color = ReadColor(so, "_Color", Color.white);
        Color emission = ReadColor(so, "_EmissionColor", Color.black);
        float metallic = ReadFloat(so, "_Metallic", 0f);
        float glossiness = ReadFloat(so, "_Glossiness", 0.5f);
        float glossScale = ReadFloat(so, "_GlossMapScale", 1f);
        float bumpScale = ReadFloat(so, "_BumpScale", 1f);
        float occlusionStrength = ReadFloat(so, "_OcclusionStrength", 1f);
        float cutoff = ReadFloat(so, "_Cutoff", 0.5f);
        float mode = ReadFloat(so, "_Mode", 0f); // Standard: 0 불투명, 1 컷아웃, 2 페이드, 3 투명

        Undo.RecordObject(mat, "Fix 2D-Lit Material");
        mat.shader = lit;
        mat.shaderKeywords = new string[0];

        mat.SetTexture("_BaseMap", mainTex);
        mat.SetTextureScale("_BaseMap", mainScale);
        mat.SetTextureOffset("_BaseMap", mainOffset);
        mat.SetColor("_BaseColor", color);

        mat.SetTexture("_BumpMap", bump);
        mat.SetFloat("_BumpScale", bumpScale);
        if (bump != null) mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_MetallicGlossMap", metallicMap);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_SmoothnessTextureChannel", 0f); // 메탈릭 맵 알파 (Standard 기본과 같음)
        mat.SetFloat("_Smoothness", metallicMap != null ? glossScale : glossiness);
        if (metallicMap != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");

        mat.SetTexture("_OcclusionMap", occlusion);
        mat.SetFloat("_OcclusionStrength", occlusionStrength);
        if (occlusion != null) mat.EnableKeyword("_OCCLUSIONMAP");

        bool hasEmission = emission.maxColorComponent > 0.001f;
        mat.SetTexture("_EmissionMap", emissionMap);
        mat.SetColor("_EmissionColor", hasEmission ? emission : Color.black);
        if (hasEmission)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        // 표면 방식
        mat.SetFloat("_Cutoff", cutoff);
        if (mode >= 2f)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            bool clip = mode >= 1f;
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_AlphaClip", clip ? 1f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);
            if (clip) mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
            mat.renderQueue = clip ? (int)RenderQueue.AlphaTest : -1;
        }
        EditorUtility.SetDirty(mat);
    }

    #region 저장된 값 읽기

    private static SerializedProperty FindEntry(SerializedObject so, string arrayPath, string name)
    {
        SerializedProperty array = so.FindProperty(arrayPath);
        if (array == null || !array.isArray) return null;
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name) return entry.FindPropertyRelative("second");
        }
        return null;
    }

    private static Texture ReadTexture(SerializedObject so, string name, out Vector2 scale, out Vector2 offset)
    {
        scale = Vector2.one;
        offset = Vector2.zero;
        SerializedProperty value = FindEntry(so, "m_SavedProperties.m_TexEnvs", name);
        if (value == null) return null;
        scale = value.FindPropertyRelative("m_Scale").vector2Value;
        offset = value.FindPropertyRelative("m_Offset").vector2Value;
        return value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
    }

    private static Color ReadColor(SerializedObject so, string name, Color fallback)
    {
        SerializedProperty value = FindEntry(so, "m_SavedProperties.m_Colors", name);
        return value != null ? value.colorValue : fallback;
    }

    private static float ReadFloat(SerializedObject so, string name, float fallback)
    {
        SerializedProperty value = FindEntry(so, "m_SavedProperties.m_Floats", name);
        return value != null ? value.floatValue : fallback;
    }

    #endregion
}
