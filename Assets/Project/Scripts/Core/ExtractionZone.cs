using UnityEngine;

// 지정 탈출 구역 체류 판정 및 일시정지 카운트다운 제어 클래스
public class ExtractionZone : MonoBehaviour
{
    [Header("Extraction Settings")]
    [SerializeField] private float requiredExtractionTime = 60f; // 필요 버티기 시간 (60초)
    [SerializeField] private float currentExtractionProgress = 0f;

    private bool isPlayerInsideZone = false;
    private bool isExtractionComplete = false;

    public float RemainingTime => Mathf.Max(0f, requiredExtractionTime - currentExtractionProgress);
    public bool IsPlayerInside => isPlayerInsideZone;
    public float ProgressPercent => Mathf.Clamp01(currentExtractionProgress / requiredExtractionTime);

    private void Update()
    {
        if (isExtractionComplete) return;

        // 플레이어가 구역 내부에 있을 때만 카운트다운 차감 연산 ⭐
        if (isPlayerInsideZone)
        {
            currentExtractionProgress += Time.deltaTime;

            if (currentExtractionProgress >= requiredExtractionTime)
            {
                isExtractionComplete = true;
                OnExtractionSuccess();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInsideZone = true;
            Debug.Log("ExtractionZone: Player entered zone. Extraction timer active.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInsideZone = false;
            Debug.Log("ExtractionZone: Player left zone. Extraction timer paused.");
        }
    }

    // 60초 탈출 버티기 성공 시
    private void OnExtractionSuccess()
    {
        Debug.Log("ExtractionZone: Extraction successful. Proceeding to Maintenance.");

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.GetType().GetMethod("CompleteRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(WaveManager.Instance, null);
        }
    }
}