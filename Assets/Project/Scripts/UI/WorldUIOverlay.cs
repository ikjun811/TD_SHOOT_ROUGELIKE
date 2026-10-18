using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// 월드 공간 UI(체력바·장탄 링 등)를 벽 등 3D 오브젝트에 가려지지 않게 항상 위에 그림
// 기본 UI 재질의 깊이 검사(unity_GUIZTestMode)를 Always로 바꾼 재질을 적용
public static class WorldUIOverlay
{
    private static readonly int ZTestModeId = Shader.PropertyToID("unity_GUIZTestMode");
    private static Material imageMaterial;

    // root 아래 모든 Image·TMP 텍스트에 적용 (UI 생성이 끝난 뒤 호출)
    public static void Apply(GameObject root)
    {
        if (imageMaterial == null)
        {
            imageMaterial = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "WorldUI_AlwaysOnTop" };
            imageMaterial.SetInt(ZTestModeId, (int)CompareFunction.Always);
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_Text text)
            {
                // 폰트 재질은 텍스트마다 인스턴스로 복제해 설정
                text.fontMaterial.SetInt(ZTestModeId, (int)CompareFunction.Always);
            }
            else
            {
                graphic.material = imageMaterial;
            }
        }
    }
}
