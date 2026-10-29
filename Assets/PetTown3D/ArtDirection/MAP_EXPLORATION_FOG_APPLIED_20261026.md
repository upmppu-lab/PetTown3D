# 맵 확장과 탐색 안개 적용 — 2026-10-26

- 맵을 220×220m에서 440×440m로 확장했습니다. 지형 간격은 기존 2m이며 기존 건물·가구·도로 좌표는 유지됩니다. 강과 이동 가능한 지형도 확장됩니다.
- 거리 안개 시작/완료 거리를 36/80m에서 50/120m로 늦췄습니다. 강 건너 가림은 8~50m 구간에서 최대 55%입니다.
- 미탐색 구역에는 최소 30%의 옅은 안개가 남습니다. 카메라 이동은 탐색 기록을 바꾸지 않습니다.
- 실제 플레이어 주변 24m와 완만한 10m 가장자리, 소유 건물의 실제 면적과 주변 12m를 탐색합니다. 기존 건물도 구형 저장을 불러올 때 반영됩니다. 건물 확장과 새 배치도 반영합니다.
- 탐색값은 증가만 합니다. 떠나거나 철거해도 다시 닫히지 않으며 기존 메인 저장의 progression.exploration에 저장합니다. 아직 방문하지 않은 곳은 계속 미탐색입니다.
- 2m 고정 셀, 220×220 R8 마스크(48,400바이트)를 사용합니다. 저장 문자열 약64.5KB. 맵 크기가 바뀌어도 세계 좌표로 기존 탐색 셀을 보존합니다.
- 탐색 안개와 별개로 아주 먼 거리에는 최대10%의 대기 흐림이 남고, 맵 끝은 경계 가림을 유지합니다.
- 완전히 가린 자연물 렌더러와 풀 패치를 표시하지 않습니다. 이미 탐색한 지역은 거리에 따른 강제 풀 패치 제외 대상에서 빼고, 기존 시야 판정·개체 밀도 제한을 유지합니다. 안개를 옅게 하면 가시 영역이 늘어나므로 이전의 짙은 안개 측정치와 동일한 성능을 보장하지 않습니다.

## 확인

- 컴파일 오류0, 안개 셰이더 진단0. 지형 정점48,841개, 실제 경계440×440m.
- 확장 지역 네 곳 (150,0), (-150,40), (0,180), (0,-180): 지면 충돌, NavMesh 샘플, 플레이어에서 완전 경로 모두 통과.
- 탐색 상태: 최초 공개, 중복 공개 멱등성, 영구 유지, JSON 저장 왕복, 맵 확장 재배치, 손상 데이터 복구, 기존 저장 호환 통과.
- 실제 런타임 연결: 카메라만 이동하면 기록 불변/미탐색안개30%; 플레이어 이동·건물 배치 주변 공개, 이동/철거 후 유지, 저장 왕복 통과. 해당 이동·배치 시험은 분리된 탐색 사본에서 진행해 실제 저장에 가상 탐색/건물을 남기지 않았습니다.
- 실제 저장 후 Play 재시작: 탐색 마스크 동일, 플레이어 위치 안개0%, 미방문 위치 미탐색 유지.
- 기존 저장 types/x/z/shopX/shopZ/shopPlacement/buildingPlacements/interiors/roads/clearedNature 모두 동일.
- 안정화된 줌75 실제 측정: 풀 캐시133,354개, 표시7,609개, 패치340개, 안개로 추가 숨긴 자연 렌더러64개. 720×1280 Editor 측정이며 실제 휴대폰 FPS/메모리는 측정하지 않았습니다.
- 플레이어 조작 코드와 원본 씬 해시가 작업 전과 동일합니다.

## 검토 화면

- `Captures/MapFog/lighter-exploration-fog-zoom75.png`
- `Captures/MapFog/lighter-exploration-fog-shop-settled.png` (풀 패치 생성 완료 후 화면)

수정: PetTownGame.cs, PetTownGame.MapFog.cs, PetTownProgressionState.cs, PetTownExplorationState.cs, PetTownDistanceFog.cs, PetTownDistanceFog.shader, PetTownGrassField.cs, PetTownForestLibrary.cs, ForestGround.shader 및 신규 파일의 .meta.
백업/시험 기록: C:/UnityProjects/PetTown3D_MapFogBackup_20261026, C:/UnityProjects/PetTown3D_ExplorationFogBackup_20261026.
