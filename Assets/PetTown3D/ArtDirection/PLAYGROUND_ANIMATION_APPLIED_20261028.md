# 놀이터 동작 적용 (2026-10-28)

공원 방문 주민이 놀이기구를 실제로 이용합니다 (15초 이용 단계 동안).

## 방문 자리 (공원 6자리)
0 그네 왼쪽 / 1 그네 오른쪽 / 2 시소 높은쪽 / 3 시소 낮은쪽 / 4 미끄럼틀 / 5 모래놀이. 빈 자리 중 무작위 배정. 호수·동물원은 기존 4자리 그대로. 저장 데이터 변경 없음(slot 범위만 0~5로 확장).

## 동작
- 그네: 좌석·사슬 메시를 분리해 실제로 흔들리고, 주민은 앉아 사슬을 잡고 앞으로 갈 때 다리를 뻗고 뒤로 갈 때 접음.
- 시소: 판이 기울며 오르내림, 두 명이 마주 앉으면 서로 반대로 움직임.
- 미끄럼틀: 옆길로 돌아가 사다리 오르기 → 앉기 → 내려오기 → 일어서기 (7.5초 주기, 2회).
- 모래놀이: 쪼그려 앉아 번갈아 모래를 퍼냄.
- 취소/종료 시 0.4초 안에 원래 자세로 복귀, 컴포넌트 자동 제거.

## 파일
신규: PetTownPlaygroundRig.cs, PetTownPlaygroundPlay.cs
수정: PetTownCommunityViewPlayground.cs, PetTownCommunityView.cs(VisitLocal), PetTownCommunityState.cs(slot 범위), PetTownGame.Community.cs(슬롯 무작위, 훅)
백업: C:\UnityProjects\PetTown3D_PlaygroundAnimBackup_20261028

## 확인
컴파일 오류 0. 격리 공원에서 그네/시소/미끄럼틀(오르기·내리기)/모래놀이 자세를 스크린샷으로 확인(Assets/Screenshots/qa_*.png, 지워도 됨).
미확인: 실제 Play에서 길찾기(각 자리 도달 가능 여부), 걷는 중 다리 모양, 주민 5종 중 1~2종만 확인.
