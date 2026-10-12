using System.Collections;
using UnityEngine;

// 지정 탈출 구역 체류 판정, 적 디스폰 및 탈출 성공 연출 클래스
public class ExtractionZone : MonoBehaviour
{
    [Header("Extraction Settings")]
    [SerializeField] private float requiredExtractionTime = 60f;
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

    // ⭐ 다음 라운드 시작 시 탈출 구역 타이머 리셋 함수
    public void ResetExtractionZone()
    {
        currentExtractionProgress = 0f;
        isExtractionComplete = false;
        isPlayerInsideZone = false;
        Debug.Log("ExtractionZone: Reset timer and state for next round.");
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

    private IEnumerator ExtractionSuccessSequence()
    {
        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.ShowNoticeBanner("EXTRACTION SUCCESSFUL", Color.green);
        }

        WaveManager.DespawnAllActiveEnemies();

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.CollectAllRemainingDropsOnField();
        }

        yield return new WaitForSeconds(2.5f);

        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
        }

        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
    }
}