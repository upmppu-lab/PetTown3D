> 최신 후속 변경: `MOBILE_COMPACT_ROWS_APPLIED_20261026.md`(한 줄 목록), `OPERATIONS_SYSTEMS_APPLIED_20261026.md`(운영/놀이). 아래는 이전 단계 기록입니다.

> 최종 추가 요청 반영: 큰 빈 그림 영역을 더 줄였습니다. 주민 행82, 2열 카드144 논리 높이, 작은54 높이 썸네일, 4열 카테고리 탭으로 재배치했습니다. 1080×1920 실제 Game View에서 가구6개가 완전히 보이며 TMP 넘침0건을 확인했습니다. 상세창은 내용이 짧으면 높이를 줄입니다. 펫/캐릭터/증축도 그림과 간격을 압축했습니다. 아래 초기 검증 이미지보다 `compact-*-final.png`가 최종 결과입니다.

# 모바일 세로형 팝업 전환 — 2026-10-26

## 원인과 변경

기존 목록은 6열×2행과 오른쪽 상세 영역을 가로 화면 비율로 고정했습니다. 헤더 아이콘은 높이47인 영역에서 위아래 여백을 벗어나는 배치였습니다. 패널/카드/버튼 원본 Sprite의 border가 모두0이어서 `Image.Type.Sliced`를 지정해도 테두리를 포함한 전체 이미지가 늘어났습니다.

- 모든 현재 게임 메뉴의 팝업을 중앙의 세로형 창으로 전환했습니다. 화면이 가로여도 팝업 자체는 세로 비율을 유지합니다. 게임 화면 방향이나 카메라 조작은 강제로 바꾸지 않았습니다.
- 공통 독립 Canvas의 기준은430×800이며 높이에 맞춰 스케일합니다. `Screen.safeArea` 안에서 창 너비/높이를 제한하고 상단/하단 여백을 둡니다.
- 제목·아이콘·닫기 버튼을 헤더 안에 배치했습니다. 목록/상세 이동용 뒤로 버튼도 헤더 안에 있습니다.
- 주민·주택·관리·가방·설정·기록은 한 열의 세로 목록, 건물·가구·분양처럼 이미지가 중심인 목록은2열 카드입니다. 실제 목록을 위아래로 스크롤하고 RectMask2D로 화면 경계를 자릅니다.
- 목록을 누르면 같은 세로 창의 별도 상세 화면으로 이동합니다. 뒤로 돌아오면 검색/카테고리/정렬/스크롤 위치를 유지합니다. 상세 내용은 세로 스크롤, 실행 버튼은 하단 고정입니다.
- 검색·카테고리·정렬·실제 썸네일과 기존 선택/확정 콜백을 유지했습니다. 닫은 창의 확인이 중복 실행되지 않도록 했습니다.
- 펫 상태/돌봄, 증축, 캐릭터 꾸미기도 같은 세로형 창으로 전환했습니다. 기존 돌봄·음식 구매·사진·보호 설정·이름 변경·증축 검사/결제·헤어/의상 선택·미리보기 회전/확대·세부 위치/회전 보정/저장은 기존 API를 사용합니다.
- 기존 Jua 글씨체와 UI 이미지/아이콘을 사용합니다. 원본 에셋의 Sprite border나 이미지 파일을 변경하지 않고, 테두리가 있는 런타임 Sprite 사본을 생성합니다. 원본 border0 유지 확인.
- 독립 Canvas가 소유 컴포넌트 비활성화/삭제 후 남지 않도록 수명주기를 연결했습니다.
- 작은 버튼의 최소 터치 영역은44 논리 단위로 맞췄습니다. 원본에 없는 회전 기호는 지원되는 문자로 바꿨고, 캐릭터 미리보기에는 안쪽 클리핑 영역을 추가했습니다.

## 변경 파일

신규 C#:
- `Scripts/PetTownMobileWindow.cs`: 세로형 공통 창, 안전 영역, 헤더, 독립 Canvas, 세로 스크롤 생성과 정리.
- `Scripts/PetTownMobileCharacterWindow.cs`: 기존 캐릭터 편집 API의 모바일 뷰, 스크롤/미리보기 입력 어댑터.

수정 C#:
- `Scripts/PetTownUITheme.cs`: 원본 Sprite를 보존하는 런타임 테두리 분할 사본.
- `Scripts/PetTownCozyCatalogView.cs`: 한 열/2열 세로 스크롤, 목록/상세 전환, 뒤로 복귀, 기존 콜백 유지.
- `Scripts/PetTownPetCareWindow.cs`: 세로형 펫 상태와 돌봄 화면. 상태/보상/재고 규칙은 교체하지 않았습니다.
- `Scripts/PetTownExpansionWindow.cs`: 세로 증축 방향/비용/가능 여부. 기존 월드 프리뷰와 검증/확정 흐름 유지.
- `Scripts/PetTownCustomizationUI.cs`: 새 모바일 캐릭터 뷰에 연결하고 기존 편집기 진입점과 입력 차단 유지. 이전 fallback 구현은 제거하지 않았습니다.

`PetTownGame.cs`, `PetTownCatalogWindow.cs`, `PetTownCharacterPreview.cs`, 이동/애니메이션/NPC/저장/지형/강 관련 코드는 이번 작업에서 변경하지 않았습니다. 신규 C#의 `.meta`는 Unity가 생성했습니다.

## 실제 Unity 검증

- Unity 컴파일 성공, 최종 Console 오류0건.
- 실제 Game View1080×1920,1080×2400,3840×2160을 확인했습니다. 최종에는 원래 선택된 Game View 해상도로 복구했습니다. 세로 검증용 Game View 프리셋2개는 선택 가능한 상태로 남겼습니다.
- 세로/가로 양쪽에서 창이 세로 비율이며 헤더 요소의 패널 밖 배치0건을 확인했습니다.
- 실제 주민18명의 목록을 맨 아래까지 스크롤하고 마지막 주민을 선택했습니다. 이름 검색으로1개 항목, 상세에서 뒤로 복귀/검색 초기화 후18개 복원 확인.
- 가구36개, 휴식 필터4개, 정렬 모드 전환, 실제 가구 상세, 실제 꾸미기 버튼→기존 Furniture 배치 모드 연결 확인. 테스트 가구를 결제/설치하지 않았습니다.
- 실제 건물/가구/펫샵 관리/가방/주민/펫/설정 메뉴7개 경로를 호출해 모두 세로형이며 TMP 글자 넘침0건 확인했습니다. 긴 세로 화면의 펫·캐릭터 창도 글자 넘침0건 확인.
- 펫 돌봄8개 버튼이 있는 하단까지 스크롤했습니다. 펫/증축/캐릭터와 격리 확인 화면에서44 미만 버튼0개 확인.
- 상세 확인 버튼의 실제 EventSystem/GraphicRaycaster 결과가 `Confirm`임을 확인했습니다.
- 캐릭터 미리보기 회전/확대 API와 증축4방향 선택/닫기 확인. 외형을 테스트 목적으로 변경하거나 증축 결제하지 않았습니다.
- 격리 UI 검증에서 중복 확인 콜백1회, 소유 컴포넌트 비활성화 시 창 닫기 확인. 검증 객체 제거.
- 수정 전/후 저장 JSON에서 건물/가구/증축/도로/제거한 자연물/펫 재고/누적 판매/인구/캐릭터 외형 필드가 동일했습니다. QA 초반의 정상 Play 진행으로 날짜·유지비·실패한 고객 요청은 갱신됐습니다. 과거 배치로 되돌리지 않았습니다.
- 실제 게임 씬 SHA256은 수정 전과 동일: `3F2E32DB0C279F5A3D1180AF1D928496EABFBB7720EFEAD037FCC28E236522D9`.
- Time.timeScale=1 복구, Play 종료. Git 커밋/push 없음.

## 최종 밀도 조정 스크린샷

- `Captures/MobileWindows/compact-furniture-final.png`: 작은 썸네일, 2열×3행이 완전히 보이는 실제36개 가구 목록.
- `Captures/MobileWindows/compact-residents-final.png`: 작은 아이콘을 쓴 실제18명 주민 목록.
- `Captures/MobileWindows/compact-character-final.png`: 작은 캐릭터 미리보기 옆 회전/확대 버튼.
- `Captures/MobileWindows/compact-pet-care-verified.png`: 상태/사료/돌봄 간격을 줄인 펫 정보.
- `Captures/MobileWindows/compact-expansion-final.png`: 작은 증축 그림과 비용/안전 검사.

최종 추가 검증:1080×2400에서도 가구 검색2개/초기화36개, TMP 넘침0,44 미만 버튼0. 최종 펫 정보에서도 넘침0/44 미만 버튼0. 짧은 가구 상세창 높이500, 목록 복귀 정상. 기존 가로 해상도3840×2160 복구 후에도 세로형 패널과 헤더 밖 요소0 확인.

- `Captures/MobileWindows/compact-furniture-tall-verified.png`
- `Captures/MobileWindows/compact-residents-landscape-verified.png`

## 초기 세로형 전환 검증 스크린샷

- `Captures/MobileWindows/resident-portrait-ready.png`
- `Captures/MobileWindows/furniture-portrait-ready.png`
- `Captures/MobileWindows/furniture-detail-portrait-ready.png`
- `Captures/MobileWindows/pet-care-portrait-ready.png`
- `Captures/MobileWindows/pet-care-actions-portrait-ready.png`
- `Captures/MobileWindows/character-portrait-ready.png`
- `Captures/MobileWindows/expansion-portrait-ready.png`
- `Captures/MobileWindows/furniture-tall-portrait.png`
- `Captures/MobileWindows/pet-care-tall-portrait.png`
- `Captures/MobileWindows/character-tall-portrait.png`
- `Captures/MobileWindows/resident-landscape-ready.png`

복구 사본: `C:/UnityProjects/PetTown3D_MobileWindowsBackup_20261026` (수정 전 코드/Git 상태/저장 JSON/씬 해시, 검증 기록).

## 검증하지 못한 부분

Android/iOS 실기기의 손가락 제스처/키보드/노치·기기별 안전 영역, 배포 빌드는 검증하지 않았습니다. Unity Editor의 Game View 크기를 바꾸고 실제 UI 렌더링·이벤트/콜백을 검증했습니다. 새로운 시스템 기능(직원/배송/미용 등)은 이번 UI 변경에 포함하지 않았습니다.
