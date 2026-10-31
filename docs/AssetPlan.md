# 에셋·이펙트 적용 계획 (2026-10-29 갱신)

에셋을 구해 왔을 때 어디에 넣는지, 지금 무엇이 임시인지 정리한 문서.
원칙: **코드로 먼저 동작하게 만들고, 에셋은 칸에 끼우기만** 하면 되도록 한다. JU 코드는 수정하지 않는다 (JU 프리팹의 값 교체는 에디터에서).

표기 — 칸 있음: 에셋만 넣으면 됨 / 칸 필요: 코드 작업 필요 / 보유: 이미 있는 에셋으로 가능 / JU: JU 프리팹 값 교체(에디터)

---

## 0. 완료된 것

| 항목 | 사용 에셋 |
|---|---|
| 플레이어 | Horrors(pack) 군인 (꼭두각시 방식) |
| 적 5종 | 러너 Crazy·브루트 Fat·봄버 Prisoner·차저 Mechanic (Horrors(pack)), 슈터 SciFi 우주 군인 — 애니메이션 연결 완료 |
| 맵 바닥·장애물·외곽 벽 | SciFi_TopDown_SpaceStation (짙은 육각 바닥, 내부 벽·기둥·상자·통, 상자 더미) |
| UI 스프라이트 | PixelUISciFiFree |
| 코드 연출 | 후처리·톤매핑, 피격 번쩍임, 히트 스톱, 카메라 흔들림, 빛기둥, 획득 알림, 상자 열림, 경고 띠, 탈출 슬로모션, 위기 화면, 연속 처치, 대시 잔상, 분위기·목표물 조명 |

## 1. 꼭 구해야 하는 것 (우선순위 순)

| 순위 | 항목 | 이유 | 넣을 곳 | 상태 |
|---|---|---|---|---|
| 1 | **배경음 6곡** (메인·전투·최종 라운드·정비·클리어·실패) | 지금 게임 전체가 무음(총소리 등 JU 기본음만) | `Resources/SoundLibrary` 배경음 칸 | 칸 있음, 재생 연결(S2)은 에셋 오면 바로 |
| 2 | **핵심 효과음** (아래 표 ★) | 타격감의 절반이 소리 | `SoundLibrary` 효과음 칸 | 칸 있음, 연결(S3) |
| 3 | **한·영 통일 폰트** (한글 지원, 숫자·영문이 게임스러운 것) | 지금 맑은 고딕 + 영문 픽셀 폰트 혼용 → 완성도 | TMP 폰트 에셋 생성 후 교체 | 결정 필요 |
| 4 | **드랍 아이템 모델** (모듈 = 칩·카트리지류, 무기 = 무기 상자 또는 총 모델) | 지금 레어도 색 도형 — 빛기둥(C1)도 이것에 맞춰 다시 손보기로 함 | ItemDropManager `Dropped Item Prefab` | 칸 있음 |
| 5 | **VFX 팩 1개** (URP, 위에서 봐도 잘 보이는 것) | 폭발·적 사망·총구 화염 품질 | 아래 2장 칸들 | 칸 있음 / 일부 칸 필요 |

### 효과음 목록 (SoundLibrary 칸 이름)

| 분류 | ★ 핵심 | 나머지 |
|---|---|---|
| 플레이어 | playerHurt, grenadeExplosion | playerDeath, playerLowHealth, dash, grenadeThrow, grenadeReady, lifesteal |
| 적 | enemyHit, enemyDeath | enemyCritHit, eliteDeath, enemyAttack, eliteIncoming, execute, (적 발소리 — EnemyAnimationEvents.FootStep 자리 있음) |
| 진행 | itemPickup, roundStart | crateOpening, crateOpened, itemDrop, itemDropLegendary, extractionEnter, extractionSuccess, gameClear, gameOver |
| UI | uiClick | uiHover, uiModulePickUp, uiModulePlace, uiModuleRotate, uiInvalid, uiEquipWeapon, uiPanelOpen |
| JU 기본 (교체 선택) | — | 총소리·장전·빈 탄창(JU 무기 프리팹), 탄착음(JU 탄 HitParticlesList) |

임시 후보: SciFi Warehouse Kit `Demo/Audio/Music` — upbeatfuture(전투), synthtrack(메인), siren.wav(엘리트 경고)

규격: 배경음 wav/ogg(mp3는 반복 시 무음 틈), 효과음 wav·앞 무음 없이·0.1~1초, 3D 소리(적 피격·사망·폭발)는 모노, 같은 칸에 비슷한 파일 2~4개면 무작위 재생

## 2. VFX — 넣을 곳과 추천

| 항목 | 현재 | 넣을 곳 | 추천 |
|---|---|---|---|
| 폭발 공용 (수류탄·착탄·재장전·대시 폭탄·위기 반격·봄버) | JU Explosion Particle | GrenadeSkill `Explosion VFX` 한 칸 | 위에서 보기 좋은 원형 충격파 + 불꽃 |
| **적 사망** | 없음 (피 연출 B4는 잔인해 꺼 둠) | EnemyBase `Death VFX` 칸 | 연기·먼지·검은 입자 (비잔혹) — 아래 3장 디졸브와 함께 |
| 총구 화염 (플레이어) | JU MuzzleFlash | JU 무기 프리팹 | 그대로 가능 |
| **슈터 총구 화염** | 없음 (탄만 나감) | 칸 필요 | 짧은 섬광 |
| 탄착 (벽·바닥·적) | JU 기본 | JU 탄 프리팹 | 금속 스파크 계열 (우주정거장 바닥과 어울림) |
| 차저 돌진 | 바닥 경로 선만 | 칸 필요 | 먼지 꼬리 + 벽 충돌 충격파 |
| 흡혈 회복·수류탄 충전 완료 | 없음 | 칸 필요 | 작은 초록 입자 / 반짝임 |
| 탈출 구역 | 구체·링 + 초록 빛 | 칸 필요 | 아래 3장 |

규격: **URP용**(Built-in 파티클 재질은 분홍 → 변환), 프리팹 하나 = 효과 하나·반복 꺼짐(삭제는 우리 코드), 탑다운 직교 카메라에서 잘 보이는 퍼지는 원형, HDR 색이면 블룸으로 빛남

## 3. 에셋 없이 코드로 추천하는 연출 (다음 후보)

| 연출 | 내용 | 가치 |
|---|---|---|
| **적 사망 디졸브** | 쓰러진 시체가 가라앉는 대신 가장자리부터 타들어 가듯 사라짐 (셰이더) + 검은 연기 조금 — B4 피 대신 비잔혹 처치 연출 | 높음 (처치마다 보임) |
| **탈출 구역 연출** | 바닥에 큰 링 + 진행률만큼 차오르는 호, 위로 오르는 입자, 성공 순간 빛기둥 | 높음 (라운드 목표) |
| 슈터 총구 섬광 | 발사 순간 짧은 빛 + 작은 섬광 (코드 파티클) | 중간 |
| 차저 돌진 먼지·충돌 충격파 | 돌진 중 발밑 먼지, 벽 충돌 시 고리 + 카메라 흔들림 | 중간 |
| 아이템 흡수 궤적 | 빨려 오는 아이템에 레어도 색 꼬리 | 낮음 |
| 엘리트 바닥 오라 | 엘리트 발밑에 천천히 도는 보라 고리 | 낮음 (보라 빛이 이미 있음) |

## 4. 지금 보유 에셋으로 바로 가능한 것

| 항목 | 현재 | 보유 에셋 |
|---|---|---|
| 수류탄 모델 | 구체 | Horrors(pack) `Character_Soldier/Prefab/Granade` → GrenadeSkill `Grenade Model` |
| 보급 상자 모델 | 노란 큐브 | 우주정거장 상자는 장애물로 쓰므로 구분 필요 — Warehouse Kit 상자 또는 우주정거장 상자 + 노란 발광 |
| 메인 화면 3D 배경 | 단색 | 우주정거장 바닥·벽 + 플레이어 군인 모델(대기 동작) + 조명 |
| 탈출 구역 꾸밈 | 구체·링 | 우주정거장 FloorVent·Light 소품 + 3장 코드 연출 |

## 구할 곳 (라이선스 확인 후 크레딧 기록)

- Kenney (kenney.nl) — CC0, UI 효과음·파티클 텍스처
- Unity Asset Store 무료 — 사운드·이펙트 팩 (URP 지원 확인)
- Sonniss GDC Game Audio Bundle — 대용량 무료 효과음 (상업 이용 가능)
- freesound.org — 파일마다 라이선스 다름 (CC0 / CC-BY는 출처 표기)
- 폰트: 눈누(noonnu.cc)에서 상업 이용 가능 한글 폰트 → TMP 폰트 에셋 생성

## 크레딧 (사용 중인 외부 에셋)

| 에셋 | 용도 |
|---|---|
| Julhiecio TPS Controller | 이동·사격·체력 기반 |
| Horrors(pack) | 플레이어 모델, 적 4종(러너·브루트·봄버·차저) |
| SciFi_Space_Soldier | 슈터 적 모델 |
| SciFi_TopDown_SpaceStation | 맵 바닥·장애물·외곽 벽 |
| PixelUISciFiFree | UI 스프라이트 |
| Vefects Flipbook VFX Bundle Lite URP | 폭발 이펙트·일부 효과음 |
| 갈무리11, Pretendard (SIL OFL) | 제목·본문 폰트 |
| 배경음 Pixabay (eisenvater, brvhrtz, fnx_sound, KK Sample), Freesound 752647 (timbre) | 전투·최종층·결과·튜토리얼 배경음 — Freesound 곡 라이선스, 메인 화면 곡(bgmMainMenu) 출처 확인 필요 |
| 수류탄 충전 알림음 | 코드로 합성 (Assets/Project/Audio/SFX) |
| (검토 후 미사용) SciFi Warehouse Kit, RPG_FPS_game_assets_industrial, Free LowPoly SciFi Pack | 이전 맵 외형 — 쓰지 않으면 빌드 전 정리 |
