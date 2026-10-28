# 에셋·이펙트 적용 계획 (2026-10-28 점검)

에셋을 구해 왔을 때 어디에 넣는지, 지금 무엇이 임시인지 정리한 문서.
원칙: **코드로 먼저 동작하게 만들고, 에셋은 칸에 끼우기만** 하면 되도록 한다. JU 코드는 수정하지 않는다 (JU 프리팹의 값 교체는 에디터에서).

표기 — 칸 있음: 에셋만 넣으면 됨 / 칸 필요: 코드 작업 필요 / JU: JU 프리팹·에셋 값 교체(에디터)

---

## 1. 사운드

| 항목 | 현재 | 넣을 곳 | 상태 |
|---|---|---|---|
| 배경음 6종 (메인·전투·최종 라운드·정비·클리어·실패) | 없음 | `Resources/SoundLibrary` 배경음 칸 | 칸 있음, 재생 연결은 S2 |
| 효과음 약 33종 (플레이어·적·진행·UI) | 없음 | `SoundLibrary` 효과음 칸 | 칸 있음, 재생 연결은 S3 |
| 총소리·장전·빈 탄창 | JU 기본 | JU 무기 프리팹(Guns)의 오디오 칸 | JU |
| 발소리 | JU 기본 | JU 발소리 설정 (Audio/Footstep) | JU |
| 탄착음 (표면별) | JU 기본 | JU 탄 프리팹 `HitParticlesList`의 AudioClips | JU |

임시 후보: SciFi Warehouse Kit `Demo/Audio/Music` — upbeatfuture(전투), synthtrack(메인). siren.wav는 엘리트 등장 경고음 후보.

규격 권장
- 배경음: wav 또는 ogg (mp3는 반복할 때 앞뒤에 무음 틈이 생김), 반복 지점이 자연스러운 곡
- 효과음: wav, 앞쪽 무음 없이 바로 소리 시작 (반응이 늦게 느껴짐), 0.1~1초
- 3D로 재생할 소리(적 피격·사망·폭발)는 모노 권장
- 같은 칸에 비슷한 파일 2~4개 → 무작위 재생으로 반복감 감소

## 2. VFX (파티클)

| 항목 | 현재 | 넣을 곳 | 상태 |
|---|---|---|---|
| 폭발 공용 (수류탄·착탄 폭발·재장전 폭발·대시 폭탄·위기 반격) | JU Explosion Particle | 씬 GrenadeSkill `Explosion VFX` 한 칸 — 모든 폭발이 크기만 바꿔 재사용 | 칸 있음 |
| 적 사망 | 없음 (0.1초 뒤 사라짐) | 적 프리팹 EnemyBase `Death VFX` | 칸 있음 (B4에서 코드 기본 연출 → 에셋 있으면 교체) |
| 엘리트 사망 | 없음 | 일반 사망과 분리 필요 | 칸 필요 (B4에서 추가) |
| 총구 화염 | JU MuzzleFlash | JU 무기 프리팹 `MuzzleFlashParticlePrefab` | JU |
| 탄착 (벽·바닥·적) | JU Default/Small/Human Skin Bullet Hit | JU 탄 프리팹 `HitParticlesList` (표면 태그별) | JU |
| 탄피 | JU Bullet Shell Particle | JU 무기 프리팹 | JU |
| 치명타 적중 | 없음 (피해 숫자만) | — | 칸 필요 (선택) |
| 드랍 빛기둥 (C1) | — | 코드로 생성, 텍스처·재질 칸 둘 예정 | C1에서 |
| 상자 열림 (C4) | — | 코드로 생성, 이펙트 칸 둘 예정 | C4에서 |
| 엘리트 등장 예고 (D1) | — | 바닥 원 코드 생성, 데칼 텍스처 칸 둘 예정 | D1에서 |
| 흡혈 회복·수류탄 충전 완료 | 없음 | — | 칸 필요 (선택) |

규격 권장
- **URP용**일 것. Built-in 전용 파티클 재질은 분홍색이 됨 → Edit > Rendering > Materials > Convert 또는 URP Particles/Unlit로 교체
- 파티클 프리팹 하나 = 효과 하나. 삭제는 우리 코드가 처리 (폭발은 일정 시간 뒤 Destroy) → 반복(Looping) 꺼져 있을 것
- 탑다운 직교 카메라라 **위에서 봤을 때** 잘 보이는 것 (옆에서 보는 불기둥형보다 퍼지는 원형)
- 블룸(A1)이 켜져 있어 밝은 색(HDR) 파티클이 잘 빛남

## 3. 3D 모델

| 항목 | 현재 | 넣을 곳 | 상태 |
|---|---|---|---|
| 플레이어 | SciFi Space Soldier (완료) | — | 완료 |
| 적 | 캡슐 (Enemy_Dummy 프리팹 5개) | 적 프리팹 외형 교체 | 칸 필요 — 아래 참고 |
| 엘리트 | 일반 적 x1.3 + 보라색 | 같은 모델 사용 또는 별도 모델 | 결정 필요 |
| 보급 상자 | 노란 큐브 | SupplyCrateManager `Crate Model` | 칸 있음 |
| 수류탄 | 구체 | GrenadeSkill `Grenade Model` | 칸 있음 |
| 드랍 아이템 | 레어도 색 도형 + 조명 | ItemDropManager `Dropped Item Prefab` (DroppedItem의 Item Renderer·Item Light) | 칸 있음 |
| 탈출 구역 | 구체·링 | ExtractionZone 외형 | 칸 필요 (꾸밈은 맵 외형 작업 때) |
| 맵 (바닥·외곽 벽·장애물) | Warehouse·산업 에셋 적용 중 | BspArenaGenerator 프리팹 칸 | 칸 있음, W3c~e 확인 대기 |
| 메인 화면 배경 | 단색 | MainMenu 씬 | 씬 배치 (사용자) |

적 모델 주의
- 지금 적은 애니메이터 없이 NavMeshAgent로만 움직임 → 모델을 넣으면 **걷기·공격·사망 애니메이션 연결 코드**가 필요 (적 종류 확정 후 작업)
- 실루엣(SilhouetteRenderer)·피격 번쩍임(EnemyHitFlash)·엘리트 색은 SkinnedMeshRenderer도 자동 대응 → 모델만 바꿔도 동작
- 엘리트 보라색은 `_BaseColor`·`_Color` 색 곱 방식 → 텍스처가 밝은 모델일수록 잘 보임
- URP Lit 재질이어야 함 (Built-in이면 플레이어 때처럼 변환)

## 4. UI

| 항목 | 현재 | 상태 |
|---|---|---|
| 한·영 통일 폰트 | 맑은 고딕 SDF + 영문 픽셀 폰트 혼용 | 결정 필요 (TMP 폰트 에셋 생성) |
| HUD 아이콘 (수류탄·대시 등) | 없음 (막대·링) | 선택 |
| "ELITE INCOMING" 배너 | 공용 알림 배너 | 나중에 수정 목록에 있음 |
| 피격·체력 낮음 화면 효과 (A2) | — | 코드 (에셋 불필요, 비네트 사용) |

## 5. 코드 연출 (에셋 불필요) — CLAUDE.md 15번

A1 후처리(완료) → **B1 피격 번쩍임(구현)** → B2 히트 스톱(치명타·엘리트 처치) → B5 카메라 흔들림 → C1 드랍 빛기둥 + C3 획득 알림 → D1 엘리트 예고 → D2 라운드 배너 · D3 탈출 슬로모션 → A2 · B4 · C4 · D4 · D5

---

## 구할 때 우선순위

1. **적 모델 + 애니메이션** — 화면에서 가장 많이 보이는데 아직 캡슐. 애니메이션(걷기·공격·사망) 포함된 것
2. **전투 배경음 + 효과음 핵심 6개** (enemyHit·enemyDeath·grenadeExplosion·itemPickup·playerHurt·uiClick)
3. **폭발 VFX 1개** — 칸 하나로 폭발 5종 전부 바뀜
4. 적 사망 VFX, 탄착 VFX
5. 보급 상자·수류탄 모델
6. 나머지 효과음, 폰트

## 구할 곳 (무료, 라이선스 확인 후 졸업작품 크레딧에 표기)

- Kenney (kenney.nl) — CC0, UI 효과음·파티클 텍스처
- Unity Asset Store 무료 에셋 — 이펙트·사운드 팩 (URP 지원 여부 확인)
- Sonniss GDC Game Audio Bundle — 대용량 무료 효과음 (상업 이용 가능)
- freesound.org — 파일마다 라이선스 다름 (CC0 / CC-BY는 출처 표기)
- 사용한 에셋은 이 문서 아래 "크레딧" 표에 바로 기록해 두면 마감 때 편함

## 크레딧 (사용 중인 외부 에셋)

| 에셋 | 용도 |
|---|---|
| Julhiecio TPS Controller | 이동·사격·체력 기반 |
| SciFi_Space_Soldier | 플레이어 모델 |
| SciFi Warehouse Kit | 맵 바닥·외곽 벽 |
| RPG_FPS_game_assets_industrial | 맵 장애물 |
| PixelUISciFiFree | UI 스프라이트 |
