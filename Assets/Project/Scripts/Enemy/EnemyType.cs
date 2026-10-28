// 적 종류 (GDD 8-6 #18) — WaveManager가 라운드별 가중치로 고름
public enum EnemyType
{
    Runner,  // 근접 추적, 빠름·체력 낮음 (기본)
    Shooter, // 거리 유지 원거리
    Brute,   // 느린 근접, 고체력·대형·넉백 저항
    Bomber,  // 접근 후 자폭, 처치 시에도 폭발
    Charger  // 예고 후 직선 돌진 (에셋 확보 후 구현)
}
