// 라운드 → 깊이 표시 (게임 제목 DEPTH ZERO, 사용자 채택 2026-10-30)
//   컨셉: 괴물이 침입한 지하 시설에서 지상(깊이 0)까지 한 층씩 탈출. 1라운드 = DEPTH 15 … 최종 라운드 = DEPTH 1, 클리어 = DEPTH 0
//   내부 계산(라운드 번호·기록 저장)은 그대로, 화면 표시만 깊이로
public static class DepthDisplay
{
    // maxRound를 모르면(0 이하) WaveManager 값, 그것도 없으면 15
    public static int FromRound(int round, int maxRound = 0)
    {
        if (maxRound <= 0) maxRound = WaveManager.Instance != null ? WaveManager.Instance.MaxRound : 15;
        return UnityEngine.Mathf.Max(0, maxRound - round + 1);
    }

    public static string Label(int depth) => depth <= 0 ? "DEPTH ZERO" : $"DEPTH {depth}";
}
