using UnityEngine;
using UnityEngine.UI;

// 화면 UI 캔버스 크기 맞춤 공용 설정 (2026-11-03, 창 모드·다른 화면 비율에서 UI가 잘리고 겹치던 문제)
//   기준 1920x1080 + Expand: 1920x1080 영역 전체가 어떤 비율에서도 화면 안에 들어오도록 줄이고, 남는 공간은 한 방향 여백.
//   → 캔버스 공간이 항상 1920x1080 이상이라 반대편 모서리에 고정한 요소끼리 가까워지지 않음
//   1920x1080(16:9)에서는 배율 1로 이전과 같게 보임
//   씬 캔버스: Tools > TD Shoot > Unify Canvas Scalers In Open Scenes
public static class UICanvasScaling
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    public static void Apply(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    public static bool IsApplied(CanvasScaler scaler)
    {
        return scaler != null
            && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
            && scaler.referenceResolution == ReferenceResolution
            && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand;
    }
}
