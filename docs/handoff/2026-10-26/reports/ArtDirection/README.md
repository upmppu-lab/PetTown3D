# PetTown3D 새 아트 디렉션 — 첫 UI 시안

2026-10-25. 전체 재제작 프로젝트 중 **HUD + 건물 목록**의 사용자 검토 단계다. 전체 그래픽 재제작 완료 보고가 아니다.

## 목표와 기존 화면 차이

목표는 크림색 종이, 따뜻한 갈색 헤더, 금색 얇은 테두리, 부드러운 그림자, 큰 입체 아이콘, 이미지 중심 카드와 큰 상세 미리보기다. 기존 화면은 회색 패널·초록 사각 버튼·문자 중심 목록이며 건물 이미지가 대부분 없었다.

신규 기본 팔레트: 크림/아이보리 패널, 월넛 헤더, 에메랄드 선택 및 확정, 코랄 닫기, 짙은 갈색 글자. 기존 회색 패널 Sprite를 다시 칠하지 않고 전용 아홉 겹침/슬라이스용 Sprite를 제작했다.

## 첫 시안 결과

- `Scenes/FirstUIReview.unity`: 실제 게임과 별도의 3D 테스트 씬. PetTownGame 없음. PlayerPrefs 쓰기 없음.
- `Prefabs/CozyHudAndBuildingCatalog.prefab`: 신규 UI 프리팹. 원래 Canvas/UI에 아직 적용하지 않았다.
- `Art/`: 투명 입체 아이콘 Atlas 12종, 새 패널/헤더/카드/선택/버튼/그림자/초상 프레임 Sprite 9종, 별도 한국어 TMP 폰트, 별도 색보정, 부드러운 색 변화의 Meadow 셰이더.
- HUD: 건설/꾸미기/가방/펫샵 관리/캐릭터/주민/펫/설정의 8개 아이콘 메뉴. 실제 저장의 Day·주민·명성·골드·매장 레벨·펫 재고 표시. 이미지의 다이아/체력/경험치 수치를 임의로 만들지 않았다.
- 건물 목록: 현재 게임에서 건설 가능한 8종의 이름·가격·면적을 `ConstructionCatalogEntries()`에서 직접 읽음. 전부 새 3D 외형 프리팹과 그 프리팹의 실제 카메라 렌더 이미지가 있음. AI 그림을 건물 썸네일로 대신하지 않았다.
- 실제 3D 모듈 조립: 집, 주민 주택, 펫 카페, 펫 병원, 공원, 호수, 동물원, 흙길. 카페는 낮은 건물+야외 테이블/의자로 구분. 640×560 투명 썸네일은 전체 Renderer bounds의 모서리 여덟 점을 카메라 좌표로 변환해 화면 안에 들어오도록 계산.
- 검색·분류·정렬·선택·상세 이미지·목록 닫기/재열기, 별도 씬의 배치 시각 미리보기. 구매/결제/저장 기능을 시안에서 실행하지 않는다.

## 확보한 에셋과 출처

실제 인터넷 검색·다운로드·압축 해제·Unity Import를 수행했다.

1. Kenney Fantasy Town Kit 2.0, 공식 페이지: https://kenney.nl/assets/fantasy-town-kit
   - 공식 페이지와 ZIP 내부 License.txt의 CC0, personal/educational/commercial 사용 허용을 확인.
   - 직접 다운로드: https://kenney.nl/media/pages/assets/fantasy-town-kit/efe948d309-1754222374/kenney_fantasy-town-kit_2.0.zip
   - 다운로드: `C:/Users/seung/Downloads/PetTown3D_ArtDirection/`
   - Import: `SourceModels/FantasyTown/`. 라이선스 파일 포함.
2. 기존 설치 Kenney UI Pack 2.0의 공식 CC0 확인: https://kenney.nl/assets/ui-pack
   - 원본은 참고/보존. 이번 패널 chrome는 전용 신규 Sprite다.
3. 아이콘: 내장 image_gen으로 신규 제작한 투명 4×3 Atlas. 프롬프트는 `Art/IconAtlas-prompt.txt`. CLI/API 키 경로 미사용.
4. 배경/장식: 이미 설치된 ithappy Fantasy_FREE tree_001 및 Stylized Nature MegaKit의 꽃/수풀/바위, 기존 가구 wrapper를 새 인스턴스에서 참조. 원본 모델·재질을 덮어쓰지 않고 이 폴더에 별도 재질을 생성.

## 실제 검증

- Unity C# 컴파일 및 별도 씬 Play.
- 검색 `공원` → 1개, 주거 분류 → 2개.
- 가격순 → 흙길이 첫 항목. 정렬 버튼/하단 안내의 표기 변경.
- 전체 8개 항목의 Thumbnail/Model 누락 없음.
- Game View 스크린샷 1600×1000: `Captures/ArtDirection/UI-Review-01-Catalog.png`, `UI-Review-02-HUD.png` (프로젝트 루트 기준).
- 저장 문자열이 작업 전 `Save_before.txt`와 정확히 일치함을 확인.
- 기본 CanvasScaler Expand를 사용해 넓은 화면에서도 설계 영역의 수직 잘림을 방지.

## 목표에 아직 못 미치는 부분 / 다음 단계

- 현재 건물의 모델링·지붕/창틀/표지/화단 디테일은 목표의 손으로 다듬은 고밀도 스타일보다 단순하다. 이미지가 있는 것만으로 최종 건물 아트 완료로 보지 않는다.
- 배경은 색상/조명 확인용 별도 디오라마. 최종 마을의 강·산책로·화단·자연물 배치 재제작은 아직 적용하지 않았다.
- 펫샵 내부, 가구 목록/실제 가구 썸네일, 가방, 펫샵 관리, 증축 방향 UI는 다음 작업이다.
- 건설 외 메뉴는 시안의 아이콘/외형만 마련했으며 클릭하면 이후 제작 범위를 안내한다. 실제 관리/캐릭터/가방 화면에 연결했다고 주장하지 않는다.
- 초상화의 일러스트는 디자인 검토용 아이콘이다. 원래 3D 캐릭터 외형/이동/애니메이션은 변경하지 않았다.
- 실제 R키·모바일 터치·이번 시안의 독립 실행 빌드 미검증.
- 시안 확인 전 원래 마을의 UI와 외형을 교체하지 않는다. 승인 후 이 새 UI를 기존 건설/결제/검사/저장 진입점에 연결한다.

## 코드 / 복구 지점

신규 코드: PetTownArtPreviewData.cs, PetTownArtPreviewUI.cs, Editor/PetTownArtPreviewBuilder.cs, Art/PetTownArtMeadow.shader. 나머지 새 데이터/프리팹/렌더/재질/라이선스는 모두 이 폴더에 있다.

작업 전 스크립트 전체·원본 씬·Git 상태·저장 문자열: `C:/UnityProjects/PetTown3D_ArtDirectionBackup_20261025/`. 기존 미커밋/미추적 파일을 초기화하거나 임의 커밋하지 않았다.

## 최종 추가 확인
- Import된 FBX: 167개. 원본 ZIP 내부 CC0 라이선스 보존.
- 원래 Game View 3840×2160에서도 카탈로그 상단/하단이 화면 안에 있음을 확인. 실제 스크린샷: Captures/ArtDirection/UI-Review-03-Wide.png.
- 테스트용 1600×1000 Game View 설정 추가 후 원래 선택으로 복원.
- 최종 Console 오류 0건. 이번 새 시안의 배포 빌드는 수행하지 않음.
- 작업 전과 비교한 기존 Scripts/*.cs 변경 0개, 원본 게임 씬 SHA256 일치. preservation-check.json 참조.
