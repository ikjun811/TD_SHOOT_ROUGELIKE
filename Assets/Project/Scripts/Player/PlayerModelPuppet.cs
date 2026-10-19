using UnityEngine;

// 플레이어 외형 모델 (C2, 꼭두각시 방식): JU 캐릭터(뼈대·무기·IK·래그돌)는 그대로 두고 몸 메시만 숨긴 뒤,
// 이 모델(Humanoid)이 매 프레임 JU 뼈대의 자세를 따라 함 (HumanPoseHandler, 뼈대 축·비율이 달라도 Humanoid 기준으로 변환)
// 무기는 JU 손에 붙어 있으므로 마지막에 이 모델의 팔을 2본 IK로 JU 손 위치·방향에 맞춤 → 총을 쥔 모습 (다리도 같은 방식으로 JU 발에 맞춤)
// 플레이어 아래 자식(모델 루트, 로컬 0)에 붙임. Tools > TD Shoot > Add Player Model Puppet이 생성
// JU의 LateUpdate(조준 회전 등) 이후에 실행되도록 실행 순서를 늦춤
[DefaultExecutionOrder(10000)]
public class PlayerModelPuppet : MonoBehaviour
{
    [Tooltip("따라 할 JU 캐릭터 Animator (비우면 부모에서 찾음)")]
    [SerializeField] private Animator source;
    [Tooltip("JU 캐릭터와 키를 맞춤 (끄면 모델 원래 크기)")]
    [SerializeField] private bool matchSourceHeight = true;
    [Tooltip("키 맞춤 후 추가 배율")]
    [SerializeField, Range(0.7f, 1.5f)] private float extraScale = 1f;
    [Tooltip("JU 캐릭터 몸 메시 숨기기")]
    [SerializeField] private bool hideSourceBody = true;
    [Tooltip("팔을 JU 손 위치에 맞춤 (총을 쥔 모습)")]
    [SerializeField] private bool matchHands = true;
    [Tooltip("다리를 JU 발 위치에 맞춤 (보폭·발 간격을 JU와 같게)")]
    [SerializeField] private bool matchFeet = true;

    private HumanPoseHandler sourceHandler;
    private HumanPoseHandler puppetHandler;
    private HumanPose pose;
    private Arm leftArm, rightArm, leftLeg, rightLeg; // 다리도 같은 3관절 구조 (hand = 발)
    private Body body;

    private class Body
    {
        public Transform hips, top, leftLeg, rightLeg;
        public Transform sourceHips, sourceTop, sourceLeftLeg, sourceRightLeg;
        public bool Valid => hips != null && top != null && leftLeg != null && rightLeg != null
                             && sourceHips != null && sourceTop != null && sourceLeftLeg != null && sourceRightLeg != null;
    }
    private bool handOffsetsReady;

    private class Arm
    {
        public Transform upper, lower, hand, sourceHand;
        public Quaternion handOffset = Quaternion.identity;
        public bool Valid => upper != null && lower != null && hand != null && sourceHand != null;
    }

    private void Awake()
    {
        if (source == null && transform.parent != null) source = transform.parent.GetComponentInParent<Animator>();
        Animator puppetAnimator = GetComponent<Animator>();
        if (source == null || !source.isHuman || puppetAnimator == null || puppetAnimator.avatar == null || !puppetAnimator.avatar.isHuman)
        {
            Debug.LogWarning("[PlayerModelPuppet] JU Animator 또는 이 모델의 Humanoid Avatar가 없음 — 비활성화", this);
            enabled = false;
            return;
        }

        if (matchSourceHeight && puppetAnimator.humanScale > 0.0001f)
        {
            float scale = source.humanScale / puppetAnimator.humanScale * extraScale;
            transform.localScale = Vector3.one * scale;
        }

        body = new Body
        {
            hips = puppetAnimator.GetBoneTransform(HumanBodyBones.Hips),
            top = TopBone(puppetAnimator),
            leftLeg = puppetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
            rightLeg = puppetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
            sourceHips = source.GetBoneTransform(HumanBodyBones.Hips),
            sourceTop = TopBone(source),
            sourceLeftLeg = source.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
            sourceRightLeg = source.GetBoneTransform(HumanBodyBones.RightUpperLeg)
        };

        leftArm = CreateArm(puppetAnimator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        rightArm = CreateArm(puppetAnimator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        leftLeg = CreateArm(puppetAnimator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        rightLeg = CreateArm(puppetAnimator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

        // 이 모델은 자체 애니메이션 없이 자세만 받음
        puppetAnimator.enabled = false;
        // JU 몸 메시를 숨겨도 애니메이션은 계속 (보이지 않으면 갱신을 멈추는 설정 방지)
        source.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        sourceHandler = new HumanPoseHandler(source.avatar, source.transform);
        puppetHandler = new HumanPoseHandler(puppetAnimator.avatar, transform);

        if (hideSourceBody)
        {
            // JU 몸 메시 = 캐릭터 바로 아래의 SkinnedMeshRenderer (무기는 손 아래 MeshRenderer라 해당 없음)
            foreach (Transform child in source.transform)
            {
                if (child == transform) continue;
                SkinnedMeshRenderer body = child.GetComponent<SkinnedMeshRenderer>();
                if (body != null) body.enabled = false;
            }
        }
    }

    // 몸통 위쪽 기준 뼈: 가슴 → 척추 → 머리 순으로 있는 것
    private static Transform TopBone(Animator animator)
    {
        Transform t = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (t == null) t = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (t == null) t = animator.GetBoneTransform(HumanBodyBones.Head);
        return t;
    }

    private Arm CreateArm(Animator puppetAnimator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
    {
        return new Arm
        {
            upper = puppetAnimator.GetBoneTransform(upper),
            lower = puppetAnimator.GetBoneTransform(lower),
            hand = puppetAnimator.GetBoneTransform(hand),
            sourceHand = source.GetBoneTransform(hand)
        };
    }

    private void LateUpdate()
    {
        sourceHandler.GetHumanPose(ref pose);
        puppetHandler.SetHumanPose(ref pose);
        AlignBody();

        // 첫 프레임: 자세를 맞춘 상태에서 손·발 뼈의 방향 차이 기록 (뼈 축 방향이 달라서)
        if (!handOffsetsReady)
        {
            foreach (Arm limb in new[] { leftArm, rightArm, leftLeg, rightLeg })
            {
                if (limb.Valid) limb.handOffset = Quaternion.Inverse(limb.sourceHand.rotation) * limb.hand.rotation;
            }
            handOffsetsReady = true;
        }

        // 다리: 발을 JU 발 위치에 (다리 기본 자세·골반 폭 차이로 벌어져 어기적거리던 문제)
        if (matchFeet)
        {
            SolveArm(leftLeg);
            SolveArm(rightLeg);
        }
        if (matchHands)
        {
            SolveArm(leftArm);
            SolveArm(rightArm);
        }
    }

    // 몸통 위치·방향은 HumanPose 값(bodyPosition/Rotation) 대신 뼈로 직접 맞춤
    // (그 값이 월드 기준으로 넘어와 모델이 떨어진 곳에서 시점을 따라 돌던 문제)
    // 방향 = 골반→가슴(위) + 왼다리→오른다리(오른쪽)로 만든 몸통 축, 위치 = 골반 위치
    private void AlignBody()
    {
        if (!body.Valid) return;
        Quaternion sourceFrame = BodyFrame(body.sourceHips, body.sourceTop, body.sourceLeftLeg, body.sourceRightLeg);
        Quaternion puppetFrame = BodyFrame(body.hips, body.top, body.leftLeg, body.rightLeg);
        body.hips.rotation = sourceFrame * Quaternion.Inverse(puppetFrame) * body.hips.rotation;
        body.hips.position = body.sourceHips.position;
    }

    private static Quaternion BodyFrame(Transform hips, Transform top, Transform leftLeg, Transform rightLeg)
    {
        Vector3 up = (top.position - hips.position).normalized;
        Vector3 right = (rightLeg.position - leftLeg.position).normalized;
        Vector3 forward = Vector3.Cross(right, up);
        if (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;
        return Quaternion.LookRotation(forward.normalized, up);
    }

    private static void SolveArm(Arm arm)
    {
        if (!arm.Valid) return;
        TwoBoneIK(arm.upper, arm.lower, arm.hand, arm.sourceHand.position);
        arm.hand.rotation = arm.sourceHand.rotation * arm.handOffset;
    }

    // 해석적 2본 IK: 어깨-팔꿈치-손목 삼각형의 각도를 목표 거리에 맞추고, 팔 전체를 목표 쪽으로 돌림 (팔꿈치 굽는 방향은 현재 자세 유지)
    private static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 target)
    {
        Vector3 pa = a.position, pb = b.position, pc = c.position;
        float lab = (pb - pa).magnitude;
        float lcb = (pc - pb).magnitude;
        if (lab < 0.0001f || lcb < 0.0001f) return;
        float lat = Mathf.Clamp((target - pa).magnitude, 0.001f, lab + lcb - 0.001f);

        float acAb0 = Angle(pc - pa, pb - pa);
        float baBc0 = Angle(pa - pb, pc - pb);
        float acAt0 = Angle(pc - pa, target - pa);
        float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axis0 = Vector3.Cross(pc - pa, pb - pa);
        if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(pc - pa, a.up);
        axis0.Normalize();
        Vector3 axis1 = Vector3.Cross(pc - pa, target - pa);
        if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0;
        axis1.Normalize();

        Quaternion aRot = a.rotation, bRot = b.rotation;
        Quaternion r0 = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, Quaternion.Inverse(aRot) * axis0);
        Quaternion r1 = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, Quaternion.Inverse(bRot) * axis0);
        Quaternion r2 = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, Quaternion.Inverse(aRot) * axis1);
        a.localRotation = a.localRotation * (r0 * r2);
        b.localRotation = b.localRotation * r1;
    }

    private static float Angle(Vector3 from, Vector3 to)
    {
        return Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f));
    }

    private void OnDestroy()
    {
        sourceHandler?.Dispose();
        puppetHandler?.Dispose();
    }
}
