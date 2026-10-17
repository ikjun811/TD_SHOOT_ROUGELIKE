using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 지정 탈출 구역 체류 판정, 위치 재배치 및 탈출 성공 연출 클래스
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

    // 다음 라운드 시작 시 탈출 구역 무작위 재배치 및 타이머 리셋
    public void RelocateAndResetZone()
    {
        currentExtractionProgress = 0f;
        isExtractionComplete = false;
        isPlayerInsideZone = false;

        // 맵 무작위 좌표 연산 및 NavMesh 보정
        Vector2 randomPoint = Random.insideUnitCircle.normalized * Random.Range(10f, 35f);
        Vector3 newPos = new Vector3(randomPoint.x, 0.02f, randomPoint.y);

        if (NavMesh.SamplePosition(newPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            transform.position = newPos;
        }

        Debug.Log($"ExtractionZone: Relocated zone to new position {transform.position}.");
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

        // 최종 라운드 탈출 = 게임 클리어 (정비 대신 결과 화면)
        bool isGameClear = WaveManager.Instance != null && WaveManager.Instance.CurrentRound >= WaveManager.Instance.MaxRound;

        // 탈출 성공 시에만 라운드 +1
        if (WaveManager.Instance != null && !isGameClear)
        {
            WaveManager.Instance.AdvanceRound();
        }

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.CollectAllRemainingDropsOnField();
        }

        yield return new WaitForSeconds(2.5f);

        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
        }

        if (isGameClear && RunResultManager.Instance != null)
        {
            RunResultManager.Instance.ShowResult(true);
            yield break;
        }

        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
    }
}