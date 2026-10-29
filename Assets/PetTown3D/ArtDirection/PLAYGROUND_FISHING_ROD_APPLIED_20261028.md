# 놀이터 설치 + 낚싯대 적용 — 2026-10-28

## 1. 놀이터 (Playground Low Poly)
- **위치**: 공원(Park) 북서쪽 모서리. 미끄럼틀 구조물(SM_Playground_05), 그네 2인용(SM_Swings_03), 시소(SM_Swings_01), 모래놀이터(SM_Sandbox_02)를 배치했습니다. 코드는 `PetTownCommunityViewPlayground.cs`(`BuildParkPlayground`)이며 `BuildPark()` 끝에서 호출됩니다.
- **URP 변환**: 팩의 머티리얼은 Built-in `Standard`라 URP에서 마젠타가 됩니다. 새 머티리얼 `ArtDirection/Prefabs/Playground/PlaygroundLit.mat`(URP/Lit + 팩의 T_Generic_colors)과 모래용 `PlaygroundSandLit.mat`(모래색 틴트)를 만들어 런타임에 모든 슬롯을 교체합니다. 원본 팩 에셋은 수정하지 않았습니다.
- **충돌/네비**: 조각마다 기존 `Block()`(BoxCollider + 카빙 NavMeshObstacle)을 붙였습니다. 모델은 `models`(NavMesh 제외) 아래라 NavMesh 굽기에 포함되지 않습니다.
- **Mesh Read/Write**: 사용한 FBX 4개를 `isReadable=true`로 변경했습니다(콘솔 NavMesh read-access 에러 방지).
- **기존 공원 요소 조정(최소)**: 북서쪽 나무를 남서쪽 모서리(-6.45, -6.4)로 이동, 북쪽 산울타리를 6그루→3그루(동쪽 절반)로 축소, 북서쪽 잔디 덤불 제거. 방문 자리 4곳·입구·보행로는 그대로입니다.
- 라이브러리 연결: `Resources/Environment/Community/Library.asset`에 `playground[4]`, `playgroundMaterial`, `playgroundSandMaterial`, `fishingRod` 필드 추가.
- 저장 데이터 변경 없음(런타임 생성이라 기존 공원도 자동 반영).

## 2. 낚싯대 (simple_fishing_rod.glb)
- `ArtDirection/Prefabs/Fishing/FishingRod.prefab` 생성: 손잡이 끝을 원점, 끝을 +Z, 길이 1로 정규화. 머티리얼 `FishingRodLit.mat`(URP/Lit, 베이스컬러 텍스처). glTFast 셰이더그래프 머티리얼을 URP/Lit로 교체해 빌드에서도 안전합니다.
- 코드 `PetTownFishingRod.cs`: 호수 낚시 자리(슬롯 0·1) 방문의 **낚시 단계(stage 2)** 동안 주민의 `RightHand` 뼈대에 낚싯대를 붙이고, 호수 쪽을 바라보게 하며, 낚싯줄(LineRenderer)과 빨간 찌를 수면에 띄웁니다. 방문이 갱신을 멈추면 0.6초 뒤 자동으로 정리됩니다(취소/완료/주민 삭제 모두 안전).
- 훅: `PetTownGame.Community.cs`의 `UpdateCommunityVisit`에 한 줄 추가(상태·보상 로직은 불변).

## 검증
- 컴파일 오류 0, Console error 0 (남은 로그: `missing pet prefab: pets/dog_1·dog_2·ferrets_black` 3건은 이번 작업 전부터 있던 기존 로그).
- 격리 공원 시각 확인(마젠타 없음, 모래 색상 수정 후 재확인). 격리 호수에서 주민 2명(Villager_01/03) 손에 낚싯대 부착·방향·줄·찌 확인.
- NavMesh 경로: 공원 0°/90° 회전 각각 입구→휴식 자리 4곳 **8/8 PathComplete**, 휴식 자리가 놀이터 블록에 들어가는 경우 0.
- 사용자 저장 미변경(격리 객체만 사용, 검사 객체 잔여 0, 씬 dirty=false). Git 커밋/푸시 없음.
- 백업: C:/UnityProjects/PetTown3D_PlaygroundRodBackup_20261028

## 확인하지 못한 것
- 사용자 마을에는 아직 공원/호수가 없어 **실제 게임 안에서 주민이 걸어가 낚시하는 전체 흐름은 눈으로 보지 못했습니다**(Edit 모드에서 주민 프리팹을 세워 낚싯대만 확인). 캐스팅/챔질 같은 낚시 **애니메이션은 없고**, 주민은 기본 서 있는 포즈에서 낚싯대를 든 모습입니다.
- 주민이 놀이기구를 타는 동작은 없습니다(장식·충돌 배치만).
- 실기기/모바일 성능은 미검증.
