# 펫 정보창 배치 및 글꼴 — 2026-10-26

이 문서는 앞서 완료한 펫 정보창 수정의 검증 기록입니다. 이후 메뉴 시인성·접지·잔디 작업은 READABILITY_GROUND_GRASS_APPLIED_20261026.md에 별도로 기록했습니다.

- 펫 프로필은 내부 뷰포트에 맞춰 비율을 유지하고, 투명 여백만 표시 UV에서 제외해 가운데 배치합니다. 원본 이미지·내보내기 데이터는 수정하지 않습니다.
- Lv 표시는 검은 글자·노란 외곽으로 프로필 테두리 좌측 상단에 걸칩니다.
- 행동 버튼은 아이콘과 글자 영역을 나누고, 글자를 버튼 안에 배치했습니다. 사료 구매 버튼도 패널 안에 배치했습니다.
- 공통 버튼·창 제목의 굵기를 지정했습니다. 기존 흰색 본문·회색 헤더 테마를 유지합니다.
- 글꼴은 이전 JavaScript 게임의 C:/git15/assets/style.css에 명시된 Noto Sans KR 대체 글꼴을 사용합니다. 휴대폰 스크린샷의 실제 시스템 글꼴과 정확히 같다고 확인한 것은 아닙니다. 이전 Jua 파일은 보존했습니다.
- 정적 Regular·Bold 폰트의 출처는 https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/KR 이며 OFL 라이선스·SOURCE.json을 Resources/Fonts/NotoSansKR에 보관했습니다.

## 변경 범위

PetTownPetCareWindow.cs, 신규 PetTownUIArtwork.cs, PetTownUITheme.cs, PetTownMobileWindow.cs, ReviewData.asset, NotoSansKR 폰트 및 동적 TMP 폰트 자산. 기존 폰트·원본 프로필 이미지는 삭제하지 않았습니다.

## 검증

- 실제 강아지 두 모델·페럿 프로필 중심 오차 약 0.000015 UI 단위 이하.
- Lv1 및 별도 격리 컨텍스트의 Lv10·14자 긴 이름·미용 완료 상태 확인.
- 720×1280, 1080×1920, 1080×2400, 1280×720에서 버튼 글자 실제 정점 이탈 0건. 행동 버튼 글자의 최소 하단 여백 약 13.9 UI 단위.
- 격리 콜백 검증: 일반 돌봄 6회, 놀기 1회, 구매 1회, 보호 전환 1회, 돌봄 저장 콜백 6회. 실제 펫의 이름·레벨을 테스트 값으로 저장하지 않았습니다.
- 실제 화면: Captures/PetCareLayout/pet-info-final-1080x1920-1.png, pet-info-ferret-centered.png.
- isolated-long-name-lv10으로 시작하는 화면은 격리 스트레스 테스트이며 실제 사용자 펫 상태를 의미하지 않습니다.
- 원본 복구 지점: C:/UnityProjects/PetTown3D_PetCareLayoutBackup_20261026.
