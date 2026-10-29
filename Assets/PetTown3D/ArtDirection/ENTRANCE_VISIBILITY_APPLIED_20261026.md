# 출입구 시인성 — 2026-10-26

카메라 쪽 벽의 불투명도를20%에서32%로 높였습니다. 출입구 양쪽 기둥과 위쪽 문틀은 진한 갈색(.23,.115,.055)의 전용 복제 재질로 만들고, 카메라 쪽에서도 불투명도95%로 유지합니다. 기둥은10cm에서14cm, 윗틀은11cm에서16cm로 조금 두껍게 했습니다. 출입구 내부 폭1.9m와 벽의 실제 충돌은 그대로입니다.

실제 Play에서 문틀3개 모두 알파.95, 문 구멍 레이 통과, 바로 옆 벽 레이 차단, 씬dirty=false를 확인했습니다. 실제720×1280 화면 `Captures/Entrance/dark-door-frame.png`를 확인했습니다. 원본 timber 재질은 바꾸지 않았습니다.

수정: Scripts/PetTownCozyInterior.cs. 복구: C:/UnityProjects/PetTown3D_EntranceVisibilityBackup_20261026.
