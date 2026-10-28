using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 지정 탈출 구역 체류 판정, 위치 재배치 및 탈출 성공 연출 클래스
public class ExtractionZone : MonoBehaviour
{
    [Header("Extraction Settings")]
    [Tooltip("일반 라운드 탈출 시간 (GDD 40초)")]
    [SerializeField] private float requiredExtractionTime = 40f;
    [Tooltip("최종 라운드 탈출 시간 배율 (보스 대신 버티기 연장, GDD 8-6 #18)")]
    [SerializeField] private float finalRoundTimeMultiplier = 1.5f;
    [SerializeField] private float currentExtractionProgress = 0f;

    [Header("Placement")]
    [Tooltip("플레이어와 이보다 가까운 위치에는 배치하지 않음 (시작과 동시에 탈출 방지)")]
    [SerializeField] private float minDistanceFromPlayer = 25f;
    [SerializeField] private int maxPlacementAttempts = 20;

    [Header("Success Effect (D3: 슬로모션 → 암전, 실제 시간 기준)")]
    [Tooltip("탈출 성공 순간 게임 속도 배율 (0보다 커야 함 — 0은 정비 정지로 취급됨)")]
    [Range(0.05f, 1f)] [SerializeField] private float successSlowScale = 0.3f;
    [SerializeField] private float successSlowTime = 1.6f;
    [SerializeField] private float successFadeTime = 0.6f;
    [Tooltip("정비·결과 화면이 열린 뒤 다시 밝아지는 시간")]
    [SerializeField] private float successFadeInTime = 0.4f;

    private bool isPlayerInsideZone = false;
    private bool isExtractionComplete = false;
    private bool isSlowMotionActive = false;
    private float defaultFixedDeltaTime = 0.02f;

    // 슬로모션 도중 씬이 바뀌면 물리 간격 복구 (timeScale은 GameSession이 씬 전환 시 1로)
    private void OnDestroy()
    {
        if (isSlowMotionActive) Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    // 이번 라운드 탈출 시간 (최종 라운드면 배율 적용)
    public float RequiredTime => requiredExtractionTime * (IsFinalRound ? finalRoundTimeMultiplier : 1f);
    private static bool IsFinalRound => WaveManager.Instance != null && WaveManager.Instance.CurrentRound >= WaveManager.Instance.MaxRound;

    // 탈출 완료 후에는 가득 고정 (성공 순간 라운드가 올라 최종 라운드 배율이 바뀌어도 게이지가 줄지 않게)
    public float RemainingTime => isExtractionComplete ? 0f : Mathf.Max(0f, RequiredTime - currentExtractionProgress);
    public bool IsPlayerInside => isPlayerInsideZone;
    public float ProgressPercent => isExtractionComplete ? 1f : Mathf.Clamp01(currentExtractionProgress / RequiredTime);

    // 트리거가 JU 바닥·계단 검사(WhatIsGround = Default, Queries Hit Triggers 켜짐)에 잡혀 가장자리에서 걸리지 않도록 Ignore Raycast 레이어로
    // 트리거 진입·이탈 판정은 그대로 동작
    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    // 첫 라운드도 무작위 배치 (BspArenaGenerator가 Awake에서 플레이어를 옮긴 뒤라 거리 검사가 맞게 적용됨)
    private void Start()
    {
        RelocateAndResetZone();
    }

    private void Update()
    {
        if (isExtractionComplete) return;

        if (isPlayerInsideZone)
        {
            currentExtractionProgress += Time.deltaTime;

            if (currentExtractionProgress >= RequiredTime)
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

        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

        BspArenaGenerator arena = FindAnyObjectByType<BspArenaGenerator>();
        if (arena != null && arena.Leaves.Count > 0)
        {
            transform.position = PickZoneCenter(arena, player) + Vector3.up * 0.02f;
            Debug.Log($"ExtractionZone: Relocated zone to zone center {transform.position}.");
            return;
        }

        // 기존 고정 맵: 맵 무작위 좌표 연산 및 NavMesh 보정, 플레이어와 너무 가까우면 다시 뽑음 (전부 실패 시 가장 먼 후보)
        Vector3 bestPos = transform.position;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 randomPoint = Random.insideUnitCircle.normalized * Random.Range(10f, 35f);
            Vector3 newPos = new Vector3(randomPoint.x, 0.02f, randomPoint.y);

            if (NavMesh.SamplePosition(newPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                newPos = hit.position;
            }

            float distance = player != null ? HorizontalDistance(newPos, player.position) : float.MaxValue;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestPos = newPos;
            }
            if (distance >= minDistanceFromPlayer) break;
        }

        transform.position = bestPos;

        Debug.Log($"ExtractionZone: Relocated zone to new position {transform.position}.");
    }

    // BSP 맵: 플레이어 시작 구역을 뺀 구역 중 플레이어와 minDistanceFromPlayer 이상 떨어진 구역 중앙을 무작위 선택 (없으면 가장 먼 구역)
    private Vector3 PickZoneCenter(BspArenaGenerator arena, Transform player)
    {
        List<Vector3> centers = arena.GetZoneCenters();
        int playerZone = player != null ? arena.GetZoneIndexAt(player.position) : -1;

        List<Vector3> candidates = new List<Vector3>();
        Vector3 farthest = centers[0];
        float farthestDistance = -1f;

        for (int i = 0; i < centers.Count; i++)
        {
            if (i == playerZone) continue;

            float distance = player != null ? HorizontalDistance(centers[i], player.position) : float.MaxValue;
            if (distance >= minDistanceFromPlayer) candidates.Add(centers[i]);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthest = centers[i];
            }
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : farthest;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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
        WaveManager.EndRoundAndDespawn();

        // 최종 라운드 탈출 = 게임 클리어 (정비 대신 결과 화면)
        bool isGameClear = WaveManager.Instance != null && WaveManager.Instance.CurrentRound >= WaveManager.Instance.MaxRound;

        // 경고 띠 배너 (슬로모션·암전 동안 유지, 라운드 증가 전 번호)
        int clearedRound = WaveManager.Instance != null ? WaveManager.Instance.CurrentRound : 0;
        AlertBanner.ShowExtractionSuccess(clearedRound, isGameClear, successSlowTime + successFadeTime);

        // 탈출 성공 시에만 라운드 +1
        if (WaveManager.Instance != null && !isGameClear)
        {
            WaveManager.Instance.AdvanceRound();
        }

        // 필드 아이템 흡수 — 비행은 실제 시간 기준(DroppedItem)이라 슬로모션 중에도 평소 속도로 빨려옴
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.CollectAllRemainingDropsOnField();
        }

        AudioManager.PlaySfx(AudioManager.Library.extractionSuccess);

        // D3 슬로모션 → 암전 (물리 간격도 같은 비율로 줄여 움직임이 끊기지 않게)
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        isSlowMotionActive = true;
        Time.timeScale = successSlowScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * successSlowScale;

        yield return new WaitForSecondsRealtime(successSlowTime);
        ScreenFader.FadeTo(1f, successFadeTime);
        yield return new WaitForSecondsRealtime(successFadeTime);

        // 정비·결과 진입 전 원래 속도로 복구 (정비는 이어서 0으로 정지)
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        isSlowMotionActive = false;

        if (StageHUDUI.Instance != null)
        {
            StageHUDUI.Instance.HideNoticeBanner();
        }

        if (isGameClear && RunResultManager.Instance != null)
        {
            RunResultManager.Instance.ShowResult(true);
            ScreenFader.FadeTo(0f, successFadeInTime); // 결과 씬으로 넘어가면 함께 사라짐, 코드 결과 화면이면 밝아짐
            yield break;
        }

        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.EnterMaintenanceStage();
        }
        ScreenFader.FadeTo(0f, successFadeInTime);
    }
}