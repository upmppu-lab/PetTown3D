# 창 이동과 반투명 벽 — 2026-10-26

- 공통 모바일 창의 제목 표시줄을 마우스 또는 터치로 끌어 이동합니다. 내용 스크롤과 닫기/뒤로 버튼은 기존 입력을 유지합니다.
- 창을 화면 가장자리로 밀어도 닫기 버튼과 다시 끌 수 있는 제목 부분은 안전 영역에 남습니다. 창 재정렬/닫았다 다시 열기에서도 해당 실행 중 이동 위치를 유지합니다. 앱 재시작 간 위치 저장은 하지 않습니다.
- 낮은 유리벽 요청은 취소된 대로 추가하지 않았습니다. 기존 카메라 방향 벽을 숨기는 대신 불투명도20%로 표시합니다. 반대편 벽은 불투명하고, 지붕을 닫으면 원래 벽 재질로 돌아갑니다.
- 실제 벽 충돌과 1.9m 출입구는 유지합니다. 원본 공유 재질을 수정하지 않고 실행 중 복제 재질을 사용합니다.

확인: 실제 EventSystem 마우스·터치 이동, 두번째 터치 무시, 극단 위치에서 닫기/잡기 영역 유지, 강제 레이아웃과 재열기 위치 유지, 닫기 버튼 드래그 방지, 본문 ScrollRect 입력과 닫기 콜백 통과.
벽 확인: 반투명89/불투명87/강제 숨김0 렌더러, 벽 충돌6개 유지. 출입구 통과 가능/바로 옆 벽 통과 불가. 지붕 닫힘 때 반투명0.

화면: Captures/DragFadeWalls/bag-dragged-mouse.png, bag-moved-aside-touch-clamped.png, camera-facing-walls-translucent.png.
수정: PetTownMobileWindow.cs, PetTownWindowDragHandle.cs, PetTownCozyInterior.cs 및 신규 .meta.
백업/시험 기록: C:/UnityProjects/PetTown3D_DragFadeWallsBackup_20261026.
