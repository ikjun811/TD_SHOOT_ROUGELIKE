// 지금 처리 중인 피해가 폭발(수류탄·착탄 폭발·재장전 폭발·대시 폭탄·위기 반격)인지 표시 — 폭발 피해 반복문을 Begin/End로 감쌈
//   IHealth.OnAnyDamaged를 듣는 쪽이 탄 적중과 폭발을 구분할 때 사용 (단일 권총 수류탄 엔진: 권총 탄 적중만 충전, W3 2026-10-31)
//   피해 처리는 같은 프레임·같은 스레드에서 순서대로 일어나므로 정적 카운터로 충분 (폭발 안의 폭발도 중첩 계산)
public static class ExplosionDamageScope
{
    private static int depth;

    public static bool Active => depth > 0;

    public static void Begin() => depth++;

    public static void End()
    {
        if (depth > 0) depth--;
    }
}
