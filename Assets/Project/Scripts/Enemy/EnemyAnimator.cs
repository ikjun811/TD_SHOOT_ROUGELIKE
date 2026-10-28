using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 적 모델 애니메이션 구동 (모델 교체 P2) — EnemyPrefabGenerator가 모델과 함께 부착·연결
//   컨트롤러(생성기가 만듦): Locomotion(Speed 블렌드: 대기 → 걷기 → 달리기) + Attack0..N + Hit + Death, 루트 모션 없음
//     directional(슈터)이면 Locomotion = 2D(MoveX·MoveZ = 루트 기준 이동 방향) — 플레이어를 보며 뒷걸음·옆걸음
//   전환은 트리거 대신 CrossFade로 직접 (트리거가 남아 나중에 엉뚱하게 재생되는 문제 없음)
//   EnemyBase가 공격·피격·사망 순간 호출, 종류별 행동(슈터 사격·봄버 예고)은 PlayAction으로
//   사망: 쓰러지는 동작 → deathLingerTime 뒤 디졸브(EnemyDissolve: 타들어 가듯 사라짐 + 검은 연기, 경계 일반 주황·엘리트 보라) → 제거
//     (이전: 바닥으로 가라앉음 → 비잔혹 처치 연출로 교체, 사용자 2026-10-29) EnemyBase가 판정·이동을 먼저 끔
public class EnemyAnimator : MonoBehaviour
{
    public Animator animator;
    [Tooltip("공격 상태 개수 (Attack0..N-1 중 무작위)")]
    public int attackCount = 1;
    public bool hasHitState = true;
    [Tooltip("true면 Speed 대신 MoveX·MoveZ(루트 기준 속도)")]
    public bool directional;
    [Tooltip("이동 속도 → Speed 파라미터 변화 부드러움")]
    public float speedDamp = 0.1f;
    [Tooltip("false면 사망 시 쓰러지는 동작 없이 바로 제거 (봄버: 폭발로 사라짐)")]
    public bool keepCorpse = true;
    [Tooltip("쓰러진 뒤 남아 있는 시간")]
    public float deathLingerTime = 2f;
    public float dissolveTime = 1f;
    [ColorUsage(false, true)] public Color dissolveEdgeColor = new Color(3f, 0.9f, 0.25f);       // 불씨 (HDR → 블룸)
    [ColorUsage(false, true)] public Color eliteDissolveEdgeColor = new Color(2f, 0.7f, 3.2f);   // 엘리트 보라
    [Tooltip("피격 동작 최소 간격 (연사에 계속 끊기지 않게)")]
    public float hitInterval = 0.6f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

    private NavMeshAgent agent;
    private float scale = 1f;
    private float nextHitTime;
    private bool dead;
    private float speedOverride = -1f; // >= 0이면 agent 속도 대신 이 속도 (차저 돌진: agent.Move라 agent.velocity에 안 잡힘)

    public bool HasModel => animator != null;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator != null) animator.applyRootMotion = false;
    }

    private void Start()
    {
        // 브루트·엘리트처럼 커진 적은 같은 속도라도 보폭이 커 보이므로 크기로 나눠 블렌드
        scale = Mathf.Max(0.1f, transform.lossyScale.y);
    }

    private void Update()
    {
        if (animator == null || dead) return;
        Vector3 velocity = agent != null && agent.enabled ? agent.velocity / scale : Vector3.zero;
        if (speedOverride >= 0f) velocity = transform.forward * (speedOverride / scale);
        if (directional)
        {
            Vector3 local = transform.InverseTransformDirection(velocity);
            animator.SetFloat(MoveXHash, local.x, speedDamp, Time.deltaTime);
            animator.SetFloat(MoveZHash, local.z, speedDamp, Time.deltaTime);
        }
        else animator.SetFloat(SpeedHash, velocity.magnitude, speedDamp, Time.deltaTime);
    }

    // 이동 속도 직접 지정 (음수 = 해제)
    public void SetSpeedOverride(float speed)
    {
        speedOverride = speed;
    }

    // 동작 재생 배율 (차저 돌진 중 달리기를 빠르게)
    public void SetPlaybackSpeed(float multiplier)
    {
        if (animator != null && !dead) animator.speed = multiplier;
    }

    public void PlayAttack()
    {
        if (animator == null || dead || attackCount <= 0) return;
        animator.CrossFadeInFixedTime("Attack" + Random.Range(0, attackCount), 0.08f);
    }

    // 피격: 이동 중일 때만 (공격·사망 동작은 끊지 않음)
    public void PlayHit()
    {
        if (animator == null || dead || !hasHitState || speedOverride >= 0f || Time.time < nextHitTime) return; // 돌진 중에는 움찔하지 않음
        if (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != LocomotionHash) return;
        nextHitTime = Time.time + hitInterval;
        animator.CrossFadeInFixedTime("Hit", 0.05f);
    }

    // 종류별 행동용 (상태 이름 직접 지정, 없으면 무시) — 기본 레이어, 없으면 상체 레이어(UpperBody)에서 찾음
    public void PlayAction(string stateName, float fade = 0.08f)
    {
        if (animator == null || dead) return;
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int hash = Animator.StringToHash(animator.GetLayerName(layer) + "." + stateName); // 전체 경로 해시
            if (!animator.HasState(layer, hash)) continue;
            animator.CrossFadeInFixedTime(hash, fade, layer);
            return;
        }
    }

    // 사망 동작 재생 후 가라앉아 제거될 때까지 걸리는 시간 반환 (EnemyBase가 Destroy 지연에 사용)
    public float PlayDeath()
    {
        if (animator == null) return 0.1f;
        dead = true;
        animator.speed = 1f;
        if (!keepCorpse)
        {
            animator.gameObject.SetActive(false);
            return 0.1f;
        }
        animator.CrossFadeInFixedTime("Death", 0.1f);
        StartCoroutine(DissolveAfterDelay());
        return deathLingerTime + dissolveTime + 0.05f;
    }

    private IEnumerator DissolveAfterDelay()
    {
        yield return new WaitForSeconds(deathLingerTime);
        EnemyBase enemy = GetComponent<EnemyBase>();
        bool elite = enemy != null && enemy.IsElite;
        EnemyDissolve.Begin(animator.gameObject, elite ? eliteDissolveEdgeColor : dissolveEdgeColor, dissolveTime);
    }
}
