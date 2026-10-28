using JU;
using UnityEngine;
using UnityEngine.AI;

// 차저 (GDD 8-6 #18 ⑤, 모델 교체 P5) — 돌진 경로 예고선 → 직선 돌진 (EnemyBase 기본 추적·근접 공격은 생성기가 끔)
//   추적: 플레이어 쪽으로 이동 → triggerRange 안에서 보이고(Walls에 안 가림) 쿨이 끝나면 예고
//   예고(windupTime): 멈춰 붉게 깜빡이며 바닥에 돌진 경로(폭 laneWidth, 벽까지 최대 chargeDistance) 표시·안쪽이 차오름
//     앞부분은 플레이어를 따라 조준, 마지막 lockTime 동안 방향 고정 (피할 틈)
//   돌진: 고정 방향으로 chargeSpeed, 최대 chargeDistance — 플레이어에 닿으면 피해(AttackDamage) 1회 + 공격 동작 후 멈춤,
//     벽·NavMesh 끝에 막히면 stunTime 동안 기절 (공격 기회), 아니면 recoverTime 쉬고 다시 추적
//   이동은 agent.Move (NavMesh 위로만 → 맵 밖으로 나가지 않음)
[RequireComponent(typeof(EnemyBase))]
public class EnemyCharger : MonoBehaviour
{
    public float triggerRange = 9f;
    public float chargeCooldown = 3.5f;
    public float windupTime = 0.8f;
    public float lockTime = 0.3f;
    public float chargeSpeed = 14f;
    public float chargeDistance = 12f;
    public float hitRadius = 1.1f;          // 플레이어와 수평 거리 (돌진 중)
    public float stunTime = 1.2f;
    public float recoverTime = 0.8f;
    public float repathInterval = 0.2f;
    public float chargeAnimSpeed = 1.8f;    // 돌진 중 달리기 동작 재생 배율

    public float laneWidth = 1.2f;
    public Color laneColor = new Color(1f, 0.2f, 0.15f, 0.25f);
    public Color laneFillColor = new Color(1f, 0.25f, 0.15f, 0.6f);

    private enum State { Chase, Windup, Charge, Recover }

    private EnemyBase enemy;
    private NavMeshAgent agent;
    private EnemyHitFlash hitFlash;
    private Transform player;
    private JUHealth playerHealth;
    private int wallMask;

    private State state = State.Chase;
    private float stateEndTime;
    private float nextChargeTime;
    private float nextRepathTime;
    private float nextBlinkTime;
    private Vector3 chargeDir;
    private float chargedDistance;
    private float laneLength;

    private GameObject laneRoot;
    private LineRenderer lane;
    private LineRenderer laneFill;
    private static Material laneMaterial;

    private void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        agent = GetComponent<NavMeshAgent>();
        wallMask = LayerMask.GetMask("Walls");
    }

    private void Start()
    {
        hitFlash = GetComponent<EnemyHitFlash>(); // EnemyBase.Start가 부착
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<JUHealth>();
        }
        nextChargeTime = Time.time + Random.Range(0.5f, 1.5f);
        CreateLane();
    }

    private void OnDestroy()
    {
        if (laneRoot != null) Destroy(laneRoot);
    }

    private void Update()
    {
        if (enemy.IsDead || player == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            SetLaneVisible(false);
            EndChargeAnimation();
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        bool playerAlive = playerHealth != null && !playerHealth.IsDead;

        switch (state)
        {
            case State.Chase:
                UpdateChase(toPlayer, playerAlive);
                break;
            case State.Windup:
                UpdateWindup(toPlayer);
                break;
            case State.Charge:
                UpdateCharge(playerAlive);
                break;
            case State.Recover:
                if (Time.time >= stateEndTime)
                {
                    state = State.Chase;
                    agent.updateRotation = true;
                }
                break;
        }
    }

    private void UpdateChase(Vector3 toPlayer, bool playerAlive)
    {
        agent.isStopped = false;
        agent.updateRotation = true;
        if (Time.time >= nextRepathTime)
        {
            agent.SetDestination(player.position);
            nextRepathTime = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
        }

        if (playerAlive && Time.time >= nextChargeTime && toPlayer.sqrMagnitude <= triggerRange * triggerRange && HasLineOfSight())
        {
            state = State.Windup;
            stateEndTime = Time.time + windupTime;
            nextBlinkTime = 0f;
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            chargeDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
        }
    }

    private void UpdateWindup(Vector3 toPlayer)
    {
        float remaining = stateEndTime - Time.time;

        // 고정 전까지 플레이어를 따라 조준
        if (remaining > lockTime && toPlayer.sqrMagnitude > 0.0001f) chargeDir = toPlayer.normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(chargeDir, Vector3.up), 720f * Time.deltaTime);

        if (hitFlash != null && Time.time >= nextBlinkTime)
        {
            hitFlash.FlashAlert(0.06f);
            nextBlinkTime = Time.time + 0.12f;
        }

        laneLength = MeasureLane();
        UpdateLane(1f - Mathf.Clamp01(remaining / windupTime));

        if (remaining <= 0f)
        {
            state = State.Charge;
            chargedDistance = 0f;
            SetLaneVisible(false);
            if (enemy.ModelAnimator != null)
            {
                enemy.ModelAnimator.SetSpeedOverride(chargeSpeed);
                enemy.ModelAnimator.SetPlaybackSpeed(chargeAnimSpeed);
            }
        }
    }

    private void UpdateCharge(bool playerAlive)
    {
        float step = chargeSpeed * Time.deltaTime;
        Vector3 start = transform.position;

        // 벽에 막힘 → 기절
        if (wallMask != 0 && Physics.SphereCast(start + Vector3.up, 0.4f, chargeDir, out _, step + 0.3f, wallMask, QueryTriggerInteraction.Ignore))
        {
            EndCharge(stunTime);
            return;
        }

        agent.Move(chargeDir * step);
        Vector3 moved = transform.position - start;
        moved.y = 0f;
        chargedDistance += moved.magnitude;

        // 플레이어 적중 (돌진당 1회) → 공격 동작 후 멈춤
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (playerAlive && toPlayer.sqrMagnitude <= hitRadius * hitRadius)
        {
            playerHealth.DoDamage(enemy.AttackDamage);
            EndCharge(recoverTime);
            if (enemy.ModelAnimator != null) enemy.ModelAnimator.PlayAttack();
            return;
        }

        // NavMesh 끝에 막힘(거의 못 움직임) → 기절, 거리 다 감 → 회복
        if (moved.magnitude < step * 0.3f) EndCharge(stunTime);
        else if (chargedDistance >= chargeDistance) EndCharge(recoverTime);
    }

    private void EndCharge(float pause)
    {
        state = State.Recover;
        stateEndTime = Time.time + pause;
        nextChargeTime = stateEndTime + chargeCooldown;
        EndChargeAnimation();
    }

    private void EndChargeAnimation()
    {
        if (enemy.ModelAnimator == null) return;
        enemy.ModelAnimator.SetSpeedOverride(-1f);
        enemy.ModelAnimator.SetPlaybackSpeed(1f);
    }

    private bool HasLineOfSight()
    {
        if (wallMask == 0) return true;
        Vector3 from = transform.position;
        from.y = 1f;
        Vector3 to = player.position;
        to.y = 1f;
        return !Physics.Linecast(from, to, wallMask, QueryTriggerInteraction.Ignore);
    }

    // 돌진 경로 길이: 벽까지 또는 최대 거리
    private float MeasureLane()
    {
        Vector3 from = transform.position;
        from.y = 1f;
        if (wallMask != 0 && Physics.SphereCast(from, 0.4f, chargeDir, out RaycastHit hit, chargeDistance, wallMask, QueryTriggerInteraction.Ignore))
        {
            return hit.distance;
        }
        return chargeDistance;
    }

    #region 경로 표시

    private void CreateLane()
    {
        laneRoot = new GameObject("ChargerLane");
        laneRoot.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 선 폭이 바닥에 눕도록 (TransformZ 정렬)
        lane = CreateLaneLine("Lane", laneColor);
        laneFill = CreateLaneLine("Fill", laneFillColor);
        SetLaneVisible(false);
    }

    private LineRenderer CreateLaneLine(string objName, Color color)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(laneRoot.transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.positionCount = 2;
        line.widthMultiplier = laneWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (laneMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) laneMaterial = new Material(shader) { name = "ChargerLane", hideFlags = HideFlags.DontSave };
        }
        if (laneMaterial != null) line.sharedMaterial = laneMaterial;
        line.startColor = line.endColor = color;
        return line;
    }

    // progress 0 → 1: 바깥 경로는 전체, 안쪽 채움이 앞으로 자람
    private void UpdateLane(float progress)
    {
        if (lane == null) return;
        Vector3 start = transform.position;
        start.y = 0.06f;
        Vector3 end = start + chargeDir * laneLength;
        lane.SetPosition(0, start);
        lane.SetPosition(1, end);
        laneFill.SetPosition(0, start + Vector3.up * 0.01f);
        laneFill.SetPosition(1, start + Vector3.up * 0.01f + chargeDir * (laneLength * progress));
        SetLaneVisible(true);
    }

    private void SetLaneVisible(bool visible)
    {
        if (lane == null) return;
        if (lane.enabled != visible) lane.enabled = visible;
        if (laneFill.enabled != visible) laneFill.enabled = visible;
    }

    #endregion
}
