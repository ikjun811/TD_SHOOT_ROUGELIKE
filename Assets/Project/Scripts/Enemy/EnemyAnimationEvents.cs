using UnityEngine;

// 적 모델 애니메이션 이벤트 수신 — EnemyPrefabGenerator가 모델(Animator가 있는 오브젝트)에 부착
//   에셋 클립에 들어 있는 이벤트에 받는 쪽이 없으면 "AnimationEvent ... has no receiver" 경고가 남
//   FootStep: SciFi 우주 군인(슈터) 걷기·달리기 클립 — 지금은 아무것도 안 함 (발소리 에셋을 구하면 여기서 재생)
public class EnemyAnimationEvents : MonoBehaviour
{
    public void FootStep()
    {
    }
}
