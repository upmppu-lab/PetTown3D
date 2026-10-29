# 모바일 목록 밀도 재조정 — 2026-10-26

사용자가 제공한 두 번째 스크린샷을 기준으로, 이미지보다 칸이 컸던 2열 카드를 한 줄씩 읽는 목록으로 바꿨습니다.

## 변경 파일

- `Scripts/PetTownCozyCatalogView.cs`: 모든 공통 카탈로그를 1열 행으로 통일. 건물/가구는 높이70, 썸네일52×52, 이름/크기, 오른쪽90×44 가격 버튼. 주민/직원/기록처럼 정보가 많은 항목은 높이82의 행과 보기 버튼. 가격 버튼도 기존 상세/확인 화면으로 연결하며 즉시 결제하지 않습니다.
- 기존 검색/분류/정렬/스크롤/상세/돌아가기/구매·배치 콜백은 유지했습니다. 빈 공간을 늘리는 2열×144 카드 경로를 제거했습니다.

## 실제 Unity 검증

- 컴파일 오류0.
- 1080×1920에서 가구36개 중6행이 온전히 표시됩니다. 6개 이름 모두 TMP 넘침 없음.
- 가격 버튼→상세→목록 복귀, `쿠션` 검색6개, 좌석 분류9개, 전체36개 복귀, 정렬 동작 확인.
- 건물8개: TMP 넘침0,44 미만 버튼0, 실제 스크롤 가능.
- 1080×2400:36개 전체 행의 TMP 넘침0, 텍스트 사각형의 행 바깥 이탈0.
- 기존 가로 해상도3840×2160 복구 후 TMP 넘침0. 세로형 창 유지.
- Android/iOS 실기기 터치·키보드·노치는 검증하지 않았습니다.

## 실제 Game View 캡처

- `Captures/MobileWindows/compact-single-rows-furniture.png`
- `Captures/MobileWindows/compact-single-rows-buildings.png`
- `Captures/MobileWindows/compact-single-rows-tall.png`
- `Captures/MobileWindows/compact-single-rows-landscape.png`

복구 사본: `C:/UnityProjects/PetTown3D_CompactRowsBackup_20261026`.
이 문서가 이전 `MOBILE_WINDOWS_APPLIED_20261026.md`의 2열 목록 설명을 대체합니다.
