# 저장 데이터 사고 기록 — 2026-10-28 04:2x

## 무엇이 일어났나
- HUD 수정 중 스크립트를 컴파일했는데, 그 시점에 에디터가 Play 상태였고(목장 동물 코드에서 NullReferenceException 다수), 이후 불러온 저장에서 **건물 배치(buildingPlacements/types/x/z)와 도로(roads)가 비어 있습니다**. 재생 중 컴파일(도메인 리로드)로 게임이 빈 건물 목록을 저장한 것으로 추정합니다(확정은 아님).
- 남아 있는 것: 골드 99,967,870, 주민 18명, Day 424, 펫 돌봄, progression 안의 호수·공원·동물원 로트(id 2b187e1f…, 6cdca6ef…, 74b25700…)와 목장 로트(id 6a90519e…, 가축 4마리), 펫샵 실내.
- 사라진 것: 목장·공원·호수·동물원 **건물 위치**와 도로(있었다면). 따라서 해당 로트의 가축·물고기·방문 기록이 지금은 연결되지 않은 상태입니다.

## 복구 자료
- 사고 직후 현재 저장 백업: C:/UnityProjects/PetTown3D_SaveBackup_20261028/
- 과거 저장 백업: C:/UnityProjects/PetTown3D_*Backup*/ (가장 최근 PetTown3D_FarmCropsBackup_20261027/save-before.json, Day 387). 이 백업에는 펫샵 + 호수(id 2b187e1f…) 1개만 있고, 목장·공원·동물원은 이후에 지어져 위치 기록이 없습니다.

## 교훈 (작업자 공통)
- 컴파일/리프레시 전에 반드시 에디터가 Play가 아닌지 확인할 것. Play 중 코드 수정 금지.
- 사용자 저장을 건드릴 수 있는 작업(Play 검증 포함) 전에 PlayerPrefs `PetTown3D_Save`를 파일로 백업할 것.
