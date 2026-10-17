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
     - WeaponDataSO.reloadTime은 아직 미사용 (무기별 장전 시간은 JU 애니메이션 길이 그대로)
     - 스탯 모듈 32종(8스탯×4레어도) 생성 완료: Tools > TD Shoot > Generate Stat Modules, ItemDropManager에 등록됨
     - 툴팁: CriticalChance·SkillCooldownReduce가 %로 표시 안 됨, 스탯명이 영어 enum 그대로 (미수정)
  2. 소규모 버그: 라운드 번호 증가(구현, 확인 대기 — ExtractionZone 탈출 성공 시 WaveManager.AdvanceRound) / 천장 로직(다음, 상세는 GDD 8-2 미드랍 천장 버그)
- 확인 보류: 금고 버그 수정분 재확인 (그리드 모듈 NEXT ROUND 유지, 장착 무기 복귀)
- 주의: 씬의 플레이어 무기 오브젝트를 지우거나 끄지 말 것 (JU는 시작 시 오른손에 활성 상태로 있는 무기만 인식)
- 에디터 미처리 항목: Q키 바인딩 제거(JU 입력 에셋), 툴팁 Txt_Description 폰트를 MALGUN SDF로, (선택) MaintenanceUI warningText 연결