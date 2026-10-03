using System.Collections;
using UnityEngine;

// 지정 탈출 구역 체류 판정, 적 디스폰 및 탈출 성공 연출 클래스
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

        if (isPlayerInsideZone)
        {
            currentExtractionProgress += Time.deltaTime;

            if (currentExtractionProgress >= requiredExtractionTime)
            {
                isExtractionComplete = true;
                StartCoroutine(ExtractionSuccessSequence());
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

    // ⭐ 탈출 성공 연출 시퀀스 코루틴
    private IEnumerator ExtractionSuccessSequence()
    {
        // 1. 화면 중앙 탈출 성공 통보 메시지 노출
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.ShowNoticeBanner("EXTRACTION SUCCESSFUL", Color.green);
        }

        // 2. 맵의 모든 적들 즉시 디스폰 소멸
        WaveManager.DespawnAllActiveEnemies();

        // 3. 필드 아이템 자석 수거
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.CollectAllRemainingDropsOnField();
        }

        // 4. 2.5초 대기 연출
        yield return new WaitForSeconds(2.5f);

        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
        }

        // 5. 정비 단계 진입
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
    }
}