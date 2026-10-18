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

## 진행 상황 (2026-10-18 기준, 상세는 docs/GDD.md 8장)
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
  6. 보급 상자 (진행 순서 A 무기 레어도 변형본(완료, 확인됨 — Tools > TD Shoot > Generate Weapon Rarity Variants) → B 상자 본체(완료, 확인됨 — SupplyCrateManager 자동 생성·스테이지당 2~3개(BSP 맵은 구역당 1개)·라운드 시작 시 재생성, SupplyCrate 3초 머무르기·진행도 유지·상자 위 진행 막대, ItemDropManager.DropCrateWeapon 중복 억제 0.3) → C 노란 가이드 화살표(완료, 확인됨 — ExtractionIndicator에 GuideTarget 추가, 상자용 화살표 자동 생성: 노랑·반경 1.9m·크기 0.7, 가장 가까운 미개봉 상자, 열기 범위 안이면 숨김) → D 일반 적 모듈만 드랍(완료, 확인됨 — TryDropItem 무기 50% 분기 제거)) — 완료 (GDD 8-6 #13~15, 총 15라운드·드랍량·레어도 표·엘리트 스폰·적 라운드 배율·필드 적 수는 8-4 #15 채택안)
  6-1. 엘리트 (완료): E1 구분·능력치(완료, 확인됨 — EnemyBase.baseHealth로 50 하드코딩 제거, MarkAsElite: 체력×5·공격×1.5·속도×0.9·크기×1.3·보라색·체력바 항상 표시, WaveManager가 엘리트 목록 생성 시 지정. 수정: 공격 판정 수평 거리, 체력바 높이 매 프레임 콜라이더 꼭대기 기준) → E2 스폰 규칙(완료, 확인됨 — WaveManager.Update: 시간 기반+처치 기반 따로 셈, 유예 20초, 엘리트 동시 최대 별도(eliteAlive), 보류(pendingElites), 탈출 연출·사망 중 보류, StageHUDUI.ShowTimedNotice "ELITE INCOMING" 2초. 기존 5킬 1마리 제거) → E3 상위 레어도 모듈 드랍(완료, 확인됨 — TryDropItem(isElite): 드랍 시 50% +1단계, 그중 30% +1단계 더)
     - 적 처리 비용 정리(완료, 적 추적 확인됨): EnemyBase 피격·공격·처치 로그 제거, 경로 재계산 0.2초 간격(플레이어 1.5m 이동 시 즉시), 라운드 리플렉션 → CurrentRound. 오브젝트 풀링은 현 규모(최대 31마리, 초당 약 1마리)에서 불필요로 판단 — 프로파일러에서 Instantiate/GC 스파이크가 보이면 재검토
     - 정비 중 첫 클릭 격발음 수정(완료): 정비 진입 시 JUCharacterController 비활성, 다음 라운드 시작 시 마우스 버튼을 뗀 뒤 활성 (PlayerStatAndWeaponSync.SetPlayerControlEnabled)
     - 사망·부활 후 사격 불가 수정(완료, 확인됨): JU 부활 시 애니메이터 재시작으로 무기 자세가 풀림 → 사망했으면 다음 동기화 때 같은 무기도 해제 후 재장착(needsReequip), 장전 상태도 정리
     - 쌍권총 장전 중 교체 시 사격 불가 수정(구현, 확인 대기): JU 쌍권총은 좌우 번갈아 쏘기라 한 손이 0발이면 멈춤 → 한 손 장전 완료 순간 다른 손도 채우고 반대손 장전 트리거 취소(장전 1모션 = reloadTime, UI와 사격 가능 시점 일치) (PlayerStatAndWeaponSync.SyncDualWeaponReload)
  7. 15라운드 수치 코드 반영 (완료, 확인됨 — maxRound 15·최종 라운드 탈출 시 라운드 증가 중지, 레어도 표(ItemDropManager.rarityTable), 드랍률 30%, 적 라운드 배율(StageDifficultyManager), 라운드별 동시 최대·스폰 간격(WaveManager). 15라운드 탈출 시 GAME CLEAR 로그 후 15라운드 반복(클리어 화면 구현 전까지 정상). 엘리트 별도 카운트는 엘리트 구현 때)
  8. 한 판 완결 (추천 순서: B 클리어·게임 오버 → C BSP 맵 → A 모듈 개편)
     - B1 결과 화면(완료, 확인됨 — 임시 코드 UI. 추후 결과 전용 씬으로 분리 결정(GDD 8-6 #10), 작업은 UI 디자인 단계에서 — RunResultManager: 전투 시간·처치 수(일반/엘리트) 기록, 클리어/게임 오버 화면 코드 생성, 다시 시작·메인 화면 버튼. GameplayBootstrap: 씬 로드마다 자동 매니저 생성(다시 시작 대응), 전투 씬에서만, timeScale 1 복구. 빌드 설정에 GameplayScene 추가 필요)
     - B2 메인 화면 씬: UI 디자인 단계로 연기 (결과 씬과 함께)
     - C BSP 아레나 (진행 중, 설계: 미로형 방·복도 대신 "넓은 공간 + 장애물". BSP 구역 + 분할선 레인 6m(모든 공간 연결 보장) + 구역 안 장애물(기둥·직선 벽·ㄱ자 벽, 구역당 0~2개, 간격 3m) + 구역 중앙 8x8 빈 공간(배치용). 90x90m, 라운드마다 새 맵, 사망 재도전도 새 맵)
       - M1 생성기(완료, 확인됨 — 장애물 구역당 0~2개·간격 3m·벽 4~7m 확정. Last Generated Seed로 시드 확인. Scripts/Map/BspArenaGenerator.cs, 컴포넌트 우클릭 Generate/Clear로 에디터 미리 보기, 씬 저장 안 됨, 시드 지원, 기즈모) → **M2** 런타임 NavMesh + GameplayScene 적용·시작 위치(구현, 확인 대기 — Awake에서 생성, GeneratedArena에 NavMeshSurface(자식·물리 콜라이더) 런타임 베이크, 벽·장애물 NavMeshModifier Not Walkable, 플레이어를 무작위 구역 중앙으로 이동·PlayerStartPosition 공개. 기존 맵 map/PlayGround는 비활성화만, 쓸 일 없으면 추후 삭제. 탈출 구역 시작 겹침 수정(구현, 확인 대기 — ExtractionZone.Start에서 첫 라운드도 재배치, 플레이어와 최소 25m·최대 20회 재추첨, 실패 시 가장 먼 후보)) → M3 (M3a 라운드마다 재생성(완료, 확인됨 — 항상 새 맵으로 진행 결정. MaintenanceManager.ResetAndStartNextRound에서 생성기 있으면 Generate, 없으면 기존 (0,1,0). 탈출 성공·사망 재도전 모두 이 경로. Clear는 플레이 중 SetActive(false) 후 Destroy로 이전 NavMesh 즉시 제거) → M3b 상자 구역 배치(완료, 확인됨 — 상자 2~3개 무작위, 구역 중앙 ±2m·구역당 최대 1개, 플레이어 시작·탈출 구역이 있는 구역 제외, 부족하면 거리 조건만 완화. BspArenaGenerator.GetZoneIndexAt 추가) → M3c 탈출 구역 구역 배치(구현, 확인 대기 — ExtractionZone.PickZoneCenter: 플레이어 시작 구역 제외, 플레이어와 25m 이상인 구역 중앙 무작위, 없으면 가장 먼 구역. 생성기 없으면 기존 반경 방식. 참고: 탈출 구역 반지름 10m라 이웃 구역까지 걸침 → 크기 조정·상자와 최소 거리 확대는 M4에서 검토)) → M4 다듬기·프리팹 교체
- 나중에 수정: "ELITE INCOMING" 배너 위치·색 (현재 공용 알림 배너 사용)
- 레이어: 6번 "Walls" (벽 전용). 총 끼임 방지가 벽에만 반응하고 적에게는 반응하지 않음 확인 완료. 주의: 플레이어·적 레이어는 바꾸지 말 것 (JU가 9·14·15번으로 몸통·뼈 구분, Default로 바뀌면 이동 불가)
- 기획 점검 (GDD 8-4): #12 총 라운드 수(→ 15라운드 결정) / #13 무기 드랍 방식(→ 보급 상자로 결정, 8-6 #13) / #14 메타 프로그레션(영구 강화) — 결정: 기획만, 개발은 일정 여유 있을 때만 (없으면 포기)
- 확인 보류: 금고 버그 수정분 재확인 (그리드 모듈 NEXT ROUND 유지, 장착 무기 복귀)
- 주의: 씬의 플레이어 무기 오브젝트를 지우거나 끄지 말 것 (JU는 시작 시 오른손에 활성 상태로 있는 무기만 인식)
- 폰트: MALGUN SDF(동적)를 Multi Atlas + TMP 전역 Fallback으로 등록해 한글 깨짐 해결됨. 새 텍스트도 한글 자동 표시
- 에디터 미처리 항목: Q키 바인딩 제거(JU 입력 에셋), (선택) MaintenanceUI warningText 연결