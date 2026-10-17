# TD_SHOOT_ROUGELIKE

## 프로젝트
- Unity 6 (6000.5.5f1), URP 3D, 졸업작품
- 탑다운 3D 슈팅 로그라이크 + 탈출 루프 + 테트리스 인벤토리
- 전체 기획: docs/GDD.md (기준 문서, 코드와 다르면 알려줄 것)

## 구조
- 직접 만든 코드: Assets/Project/Scripts (Core, Enemy, Inventory, Player, ScriptableObjects, UI)
- 서드파티: Assets/Julhiecio TPS Controller (이동/사격/체력 기반). **JU 코드는 수정하지 말 것** — 필요한 기능은 Assets/Project 쪽 래퍼·이벤트 컴포넌트로 구현 (기존에 직접 수정한 대시·조준 코드는 현 상태 유지)
- 메인 씬: Assets/Scenes/GameplayScene.unity

## 작업 규칙
- .unity, .prefab, .meta 파일은 직접 수정하지 말 것
- 한 번에 한 가지만 수정하고, 수정 전에 계획을 먼저 설명할 것
- 수정한 파일과 Unity 에디터에서 사람이 해야 할 작업(컴포넌트 연결 등)을 마지막에 알려줄 것
- 기존 구조(싱글톤 매니저)를 유지하고 불필요한 리팩터링은 하지 말 것
- 한글로 답변할 것

## 진행 상황 (2026-10-17 기준, 상세는 docs/GDD.md 8장)
- 이전에 Google AI Studio로 Phase 3까지 개발함 (정비 UI 완료)
- Phase 4 진행 중
  - 완료: 4-1 무기 데이터 5종 재정의 / 4-2a 시작 무기(UMP) 지급 + 장착 슬롯↔JU 무기 동기화 / 4-2b 장착 무기 0개면 NEXT ROUND 차단 / 4-2c SO 수치(연사·탄창·피해) 적용 + 전 무기 무한 예비탄
  - 완료: 머티리얼 누적 수정, 금고 버그 2건 수정 (그리드 모듈·장착 무기 출처 복귀)
  - 완료: 총 끼임 방지를 Walls 레이어 전용으로 (PlayerStatAndWeaponSync.Awake). 벽만 Walls, 바닥은 Default 유지(플레이어 What Is Ground가 Default만 봄). 플레이어에 NavMesh Modifier(Remove)로 베이크 제외
- 다음 작업 (순서대로)
  1. Phase 4 남은 작업: 모듈 스탯 전체 적용 — 4-3a 공격력·연사(완료, 확인됨) / 4-3b 수류탄 쿨감(코드 연결 완료, GrenadeSkill이 씬에 없어 미확인) / 4-3c 치명타(완료, 확인됨 — 탄 템플릿에 CriticalBullet 부착, 배율 2배 임시) / 4-3d 장전 속도(완료, 확인됨 — 장전 중에만 Animator.speed 상향)
     - 무기별 장전 시간(구현, 확인 대기): 장전 중 애니메이터 속도 = (장전 클립 길이 / reloadTime) x (1 + 장전 모듈%). 차별화 적용 P226 1.2 / Dual 1.8 / UMP 1.8 / Benelli 2.5 / M82 3.0 (임시값). 장전 방식 모듈은 이 값을 기준으로 설계 (GDD 8-4 #4, #8)
     - 스탯 모듈 32종(8스탯×4레어도) 생성 완료: Tools > TD Shoot > Generate Stat Modules, ItemDropManager에 등록됨
     - 툴팁 모듈 스탯 한글·% 표시, 점유 칸 수를 실제 채워진 칸으로 (구현, 확인 대기). 레어도·무기군은 아직 영어 enum 표시
  2. 소규모 버그: 라운드 번호 증가(완료, 확인됨 — ExtractionZone 탈출 성공 시 WaveManager.AdvanceRound) / 천장 로직(완료, 확인됨 — 3연속 미드랍 후 다음 처치 확정, 천장 발동 2회까지만, 천장 드랍은 감쇄 미적용: 사용자 결정)
  3. 전투 피드백 UI (GDD 8-5 #3): 체력바(완료, 확인됨 — WorldHealthBar: 플레이어는 머리 위만·항상 표시, 적은 피격 후 3초만 표시·EnemyBase가 자동 부착) / 피해량 이펙트(완료, 확인됨 — DamageNumberManager 자동 생성, IHealth.OnAnyDamaged 구독, 치명타는 CriticalBullet이 RegisterCriticalHit으로 알림) / 장탄수 표시·재장전 이펙트(구현, 확인 대기 — WorldAmmoRing: 캐릭터 화면 오른쪽 원형 링, 사격 시 감소·재장전 시 장전 애니메이션 진행도로 차오름, 플레이어에 에디터 부착)
     - 장탄 링(완료, 확인됨) / 카메라 룩어헤드(완료, 확인됨 — CameraLookAhead, 최대 4m)
     - 카메라: 직교(Orthographic) 카메라, 25° 기울기. 보이는 범위는 Distance가 아니라 카메라 상태의 FOV/10 = Orthographic Size (현재 FOV 70 → Size 7)
     - 적 스폰: 카메라 화면 안 위치는 재추첨(WaveManager.IsVisibleOnScreen, 최대 10회) (완료, 문제없음 확인)
  4. 탈출 지점 가이드 (구현, 확인 대기): ExtractionIndicator를 발밑 바닥 화살표로 재작성, 자동 생성. 플레이어 주위 1.3m에서 탈출 구역 방향을 가리킴, 구역 안·사망 시 숨김
     - 탈출 가이드 화살표(완료, 확인됨 — 꼬리 없는 삼각형, notchDepth로 밑변 파임 조절)
  5. 수류탄 스킬 (완료, 확인됨 — GrenadeSkill 재작성 + GrenadeProjectile, 상세 GDD 8-1). 충전 수는 장탄 링 오른쪽 세로 막대로 표시
  6. 후보: 엘리트 무기 드랍 방식(GDD 8-6 #5) / 모듈 개편
- 레이어: 6번 "Walls" (벽 전용). 총 끼임 방지가 벽에만 반응하고 적에게는 반응하지 않음 확인 완료. 주의: 플레이어·적 레이어는 바꾸지 말 것 (JU가 9·14·15번으로 몸통·뼈 구분, Default로 바뀌면 이동 불가)
- 기획 점검 (GDD 8-4): #12 총 라운드 수 / #13 무기 드랍 방식 / #14 메타 프로그레션(영구 강화) — 결정: 기획만, 개발은 일정 여유 있을 때만 (없으면 포기)
- 확인 보류: 금고 버그 수정분 재확인 (그리드 모듈 NEXT ROUND 유지, 장착 무기 복귀)
- 주의: 씬의 플레이어 무기 오브젝트를 지우거나 끄지 말 것 (JU는 시작 시 오른손에 활성 상태로 있는 무기만 인식)
- 폰트: MALGUN SDF(동적)를 Multi Atlas + TMP 전역 Fallback으로 등록해 한글 깨짐 해결됨. 새 텍스트도 한글 자동 표시
- 에디터 미처리 항목: Q키 바인딩 제거(JU 입력 에셋), (선택) MaintenanceUI warningText 연결