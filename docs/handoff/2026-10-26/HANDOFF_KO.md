**PetTown3D 개발 인수인계 — 2026-10-26 기준**

이 문서를 기준으로 기존 Unity 프로젝트의 개발을 이어가세요. 프로젝트를 새로 만들거나 초기화하지 마세요.

이 문서는 현재 소스의 읽기 전용 확인과 과거 개발·검증 기록을 종합한 것입니다. 문서 작성 과정에서 Unity Play, 재컴파일, 빌드, 저장 데이터 변경은 수행하지 않았습니다. 따라서 아래의 실행 검증 결과는 **해당 개발 단계에서 기록된 결과**이며, 지금 새로 전체 회귀 테스트를 통과했다는 뜻은 아닙니다.

**1. 프로젝트 위치·구조·개발 환경**

- 집 데스크톱 프로젝트: `C:\UnityProjects\PetTown3D`
- Unity 버전: **6000.6.6f1**
- 실제 게임 씬: `Assets/PetTown3D/Scenes/PetTown3D_CharacterTest.unity`
- 렌더링: URP 기반
- URP 설정: `Assets/PetTown3D/Settings/PetTown3D_URP.asset`
- 주요 패키지:
  - Universal RP `17.6.0`
  - AI Navigation `2.0.16`
  - Input System `1.20.0`
  - uGUI `2.6.0`
  - glTFast `6.20.0`
  - Unity MCP: `com.coplaydev.unity-mcp`
- HDRP 등 다른 패키지도 설치되어 있으나, 설치 여부와 실제 게임에서 사용하는 렌더링 파이프라인을 혼동하지 마세요.

주요 폴더:

```text
PetTown3D/
├─ Assets/
│  ├─ PetTown3D/
│  │  ├─ Scenes/
│  │  │  └─ PetTown3D_CharacterTest.unity
│  │  ├─ Scripts/
│  │  ├─ Editor/
│  │  ├─ Characters/Master/
│  │  ├─ Resources/
│  │  │  ├─ MasterPlayer.prefab
│  │  │  ├─ MeshyPlayer.prefab
│  │  │  ├─ Modular/
│  │  │  ├─ Furniture/
│  │  │  ├─ MedievalHouses/
│  │  │  ├─ Villagers/
│  │  │  ├─ pets/
│  │  │  ├─ AquariumFish/
│  │  │  ├─ HairStyles/
│  │  │  ├─ ClothingStyles/
│  │  │  ├─ Fonts/
│  │  │  ├─ Materials/
│  │  │  ├─ Environment/ApprovedNature/
│  │  │  └─ UI/Cozy/
│  │  ├─ ArtDirection/
│  │  │  ├─ Art/
│  │  │  ├─ Editor/
│  │  │  ├─ Prefabs/
│  │  │  ├─ Scenes/
│  │  │  ├─ SourceModels/
│  │  │  ├─ CozyFurniture/
│  │  │  ├─ MobileStyle/
│  │  │  ├─ NatureReview/
│  │  │  └─ Thumbnails/
│  │  ├─ EnvironmentTests/
│  │  └─ Settings/
│  └─ 외부 에셋 원본 폴더들
├─ Packages/
├─ ProjectSettings/
└─ Captures/
```

중심 코드는 다음과 같습니다.

| 파일 | 역할 |
|---|---|
| `Scripts/PetTownGame.cs` | 월드 생성, 건설, 가구, 도로, NPC·동물, 카메라, 경제, 저장 연결 |
| `Scripts/PetTownGame.CozyPresentation.cs` | 새로운 HUD·카탈로그·관리 화면과 실제 게임 기능 연결 |
| `Scripts/PetTownGame.Progression.cs` | 플레이어·펫 성장, 개체 분양, 사료, 행복도 연결 |
| `Scripts/PetTownProgressionState.cs` | 버전이 있는 진행 데이터 |
| `Scripts/PetTownPlayerControls.cs` | 플레이어 이동·조이스틱·클릭 이동·충돌 |
| `Scripts/PetTownWorldSafety.cs` | 공통 월드 안전 판정 |

마을·건물·Canvas 상당 부분은 **Play 시작 시 런타임에 생성**됩니다. 편집 모드에서 오브젝트나 UI가 보이지 않는다는 이유만으로 미구현이라고 판단하지 마세요.

현재 `ProjectSettings/EditorBuildSettings.asset`의 `m_Scenes`는 빈 배열입니다. 과거 Windows 빌드는 씬 경로를 명시해 실행했습니다. 새로운 컴퓨터에서 기본 Build 버튼만 누르면 같은 빌드 구성이 자동으로 재현된다고 가정하지 마세요.

**이전 시 가장 중요한 주의사항**

확인한 Git HEAD는 `d92b1b8`이며 2026-10-23 커밋입니다. 이후 변경된 코드와 새 에셋이 **미커밋·미추적 상태로 많이 남아 있습니다.**

- Git clone/pull만으로 10월 26일 최신 상태를 확보했다고 판단하지 마세요.
- 실제 작업 폴더의 `Assets`와 모든 `.meta`, `Packages`, `ProjectSettings`를 빠짐없이 이전해야 합니다.
- 루트의 `Assets.zip`, `Packages.zip` 등이 현재 작업 폴더와 동일한 최신본이라는 보장은 없습니다.
- PlayerPrefs 저장 데이터는 프로젝트 폴더와 별개이므로 별도 이관이 필요합니다.
- 이번 인수인계 작성에서는 커밋·푸시·복사·저장 데이터 추출을 실행하지 않았습니다.

**2. 기존 요구사항과 중요한 결정 사항**

최종 목표는 **심즈처럼 꾸미는 재미와 아노처럼 정돈된 건물·도로 배치를 결합한 밝은 모바일 3D 마을 게임**입니다.

사용자 요구사항의 변화 순서를 반영해야 합니다.

- 처음에는 도로 끝 갈라짐, 입구 연결, 텍스처 늘어짐을 수정하도록 요청했습니다.
- 이후 건설 격자, 4방향 회전, 도로 정렬, 안전 검사, 도로 저장을 추가했습니다.
- 가구도 건물처럼 격자·4방향 회전·실제 모델 미리보기를 사용하도록 요청했습니다.
- UI·펫샵 실내·자연환경은 단순 색 변경이 아닌 **새로운 디자인으로 전면 제작**하도록 요청했습니다.
- 새 디자인은 별도 시안에서 실제 Game View로 확인한 뒤 본 게임에 연결하는 방식을 채택했습니다.
- 목표 이미지의 가격·레벨·수량은 예시입니다. 실제 데이터가 없는 수치를 꾸며 넣으면 안 됩니다.
- 건물·가구 목록에는 실제 3D 프리팹을 렌더링한 썸네일과 큰 상세 미리보기를 우선 사용합니다.
- 기본 제공 가구 11종과 전체 구매 카탈로그는 별개입니다. 카탈로그를 11종·14종으로 제한하지 않습니다.
- 나무·잔디는 실제 에셋을 사용해야 합니다. 평평한 녹색 바닥이나 단순 도형만으로 자연환경을 완성했다고 보고하지 않습니다.
- 원목 바닥은 불규칙한 판자 형태이며, 가구가 잘 보이도록 **밝은 원목 색**을 사용합니다.
- 실시간 그림자가 필요합니다.
- 모바일 화면을 가리지 않도록 작은 독립 원형 아이콘 버튼을 사용합니다.
- 하단 왼쪽: 건설·꾸미기·가방·펫샵 관리.
- 오른쪽 중앙: 캐릭터·주민·펫·설정.
- 상단은 작은 상태 칩 형태입니다.
- 안내 메시지는 처음에 요청했던 노란 글자·검은 외곽 스타일에서 다시 변경됐습니다. **최신 요구는 글자 길이에 맞춰 폭이 변하는 검은 안내창**입니다.
- 승인된 자연환경 시안은 나무가 너무 많다는 요청에 따라 185개에서 93개로 줄였습니다.
- 최신 자연환경 요구는 연한 잔디, 무성한 곳과 낮은 풀의 혼합, 불규칙한 흙 공터, 큰 반복 물결을 줄인 자연스러운 강과 강둑입니다.
- 기존 JavaScript 게임의 분양·성장·행복도 시스템을 참고해 Unity에 적용하도록 요청했습니다.

기존 가구·집 위치를 무조건 보존하라는 초기 조건은 이후 사용자가 **“현재 배치는 테스트용이므로 변해도 되고 복원할 필요 없다”**고 완화했습니다. 그렇다고 저장 호환성이나 정상 기능을 없애도 된다는 의미는 아닙니다.

일상적인 범위 내 작업은 반복 승인 없이 진행해 달라는 선호가 있습니다. 큰 선택이나 실제 실행 환경의 권한 요구는 별개입니다.

**이번 인수인계 요청 자체는 읽기 전용이며, 프로젝트 변경을 허용한 요청이 아닙니다.**

**3. 지금까지 구현된 기능**

현재 구현 범위는 다음과 같습니다.

- 절차적 3D 지형·강·강둑·자연물 생성.
- 펫샵, 집, 주민 주택, 카페, 병원, 공원, 호수, 동물원 건설.
- 건설 모드의 지형 추종 1m 격자와 위치 스냅.
- 신규 건물의 90° 단위 회전, 점유 영역·입구 표시.
- 도로를 향한 방향 추천과 건물 앞줄 정렬 보조.
- 건물·물·경사·입구·통로·기존 도로 높이 보호 검사.
- 자유 곡선 드래그 도로와 격자 직선 도로.
- 도로 중심선·폭·모드·등급·ID 저장과 복원.
- 가구 카탈로그, 실제 모델 미리보기, 0.5m 실내 격자, 4방향 회전.
- 가구 충돌·출입구·통로·지지면 검사.
- 신규 펫샵 기본 가구 11종.
- 실내 가구별 ID·위치·회전·점유 크기 저장.
- 펫샵의 전후좌우 실제 공간 증축.
- 증축 비용·자연물 정리·안전 검사·실패 복구·저장.
- 캐릭터 외형 변경 관련 시스템과 미리보기.
- 키보드·조이스틱·클릭 이동, 카메라 회전·이동·확대.
- 주민·고객·전시 동물의 기존 NavMesh 이동.
- 골드, 날짜, 주문, 판매 수, 명성, 매장 레벨, 입고와 기존 유지비·자동 저장.
- 새 모바일 HUD, 건물·가구 카탈로그, 관리 화면.
- 플레이어 레벨·경험치·레벨업 보상.
- 펫 고유 ID·이름·레벨·경험치·성장·컨디션·분양 보호.
- 개체 선택 분양과 중복 분양 방지, 분양 이력.
- 유료 사료 구매·재고·소비, 유효 돌봄 보상.
- 시설·서비스·혼잡을 반영한 마을 행복도.
- 실제 에셋 기반 나무·풀·꽃·관목·돌과 GPU 인스턴싱.
- 공유 마스크를 사용하는 잔디 밀도·높이·흙 공터 표현.
- 잔물결과 얕은 물·강둑 색 변화.
- 물 에셋의 자동 광고 팝업 비활성화.

카페·병원·동물원 등의 건물을 건설할 수 있다는 사실과, 해당 시설의 전체 영업 시스템이 구현됐다는 것은 구분해야 합니다.

**4. 건설·도로 시스템**

주요 파일:

```text
Scripts/PetTownBuildGrid.cs
Scripts/PetTownGridOverlay.cs
Scripts/PetTownBuildingDefinition.cs
Scripts/PetTownBuildingPlacement.cs
Scripts/PetTownPlacementPlanner.cs
Scripts/PetTownPlacementValidator.cs
Scripts/PetTownEntrancePreview.cs
Scripts/PetTownNavigationQueries.cs
Scripts/PetTownRoadPath.cs
Scripts/PetTownRoadConnection.cs
Scripts/PetTownRoadDrawing.cs
Scripts/PetTownWorldSafety.cs
```

건설 기본 규칙:

- 기본 격자 1m.
- 회전 0°·90°·180°·270°.
- `entranceBaseYaw`, 모델별 방향 보정, 모델 변형 정보를 관리합니다.
- 수동 회전은 자동 도로 방향 추천보다 우선합니다.
- 미리보기와 실제 설치가 같은 배치 정보를 사용합니다.
- 최종 설치 직전에 다시 검사합니다.
- 초록은 도로 연결된 설치 가능 상태입니다.
- 노랑은 도로가 없지만 설치할 수 있는 상태입니다.
- 빨강은 설치 불가입니다.
- 도로 연결은 건설의 필수 조건이 아닙니다.

현재 코드의 기본 건설 가격:

| 항목 | 골드 |
|---|---:|
| 최초 펫샵 | 0 |
| 집 | 600 |
| 주민 주택 | 250 |
| 펫 카페 | 1,500 |
| 펫 병원 | 1,800 |
| 공원 | 450 |
| 호수 | 700 |
| 동물원 | 2,400 |
| 도로 | 길이 1m당 20 |

도로:

- 자유 곡선 드래그를 유지합니다.
- 직선 모드는 큰 드래그 축을 기준으로 수평·수직 정렬합니다.
- 기존 도로 폭 2.2m와 지형 추종·둥근 끝처리·UV 생성 경로를 재사용합니다.
- 드래그 중 모드를 바꾸지 않도록 시작 시 모드를 고정합니다.
- 도로 교차·끝점 근접 연결을 인식하지만, 기존 메시 전체를 자동 병합하는 시스템은 아닙니다.
- 신규 건물의 앞줄 정렬은 가까운 축 방향 도로에 대해 최대 4m 범위에서 보조합니다.
- 도로 연결 기본 거리 `roadGap`은 2.5m입니다.
- 기존 도로 높이를 5cm 넘게 바꾸는 건설 평탄화는 거부합니다.
- `GetRoadConnection()`은 연결 여부, 입구, 도로 가장자리, 거리, 등급, 도로 ID를 제공합니다.
- 짧은 연결 보행로 자동 생성, 도로별 행복도 보너스, NPC 도로 우선 이동은 아직 구현 범위가 아닙니다.

옛 저장의 `kind=8` 항목에는 도로 중심선이 없습니다. 좌표는 보존하지만 이미 사라진 곡선 형태를 복원할 수는 없습니다. 임의의 경로를 만들어 “복원”했다고 처리하지 마세요.

**5. 에셋·프리팹 및 가구 구성**

실제 사용하거나 활용 기반으로 확보한 에셋:

- Barking_Dog 모듈: `Floor_01`, `Wall_Simple_01`, `Roof_01`.
- Kenney Building Kit.
- Kenney Furniture Kit.
- Kenney Mini Market.
- Kenney UI Pack.
- KayKit Furniture Bits FREE.
- Kenney Fantasy Town Kit 2.0: 실제 다운로드·Import한 기록이 있으며 건물 시안에 사용.
- Stylized Nature MegaKit Standard.
- `3D set of stylized nature - GHIBLI style`.
- Anime Nature: 실제 폴더명 `Anime Trees`.
- ithappy Fantasy_FREE 등 기존 자연물.
- Floreswa 물고기 모델: 수족관용 `fish01`, `fish02`, `fish03`.
- 기존 Medieval Houses, FenceKit, Rocks, Villagers 리소스.
- `MasterPlayer.prefab`, `MeshyPlayer.prefab`.
- `Resources/pets/`의 `dog_1.glb`, `dog_2.glb`, `ferrets_black.glb`, `fish_1.glb`.

에셋 원본을 직접 통일된 Scale로 변형하지 않고, 크기·피벗·바닥 접점·방향을 정규화한 wrapper prefab과 복제 재질을 사용합니다.

Kenney·KayKit·Quaternius 관련 CC0 확인 기록이 있습니다. 다른 에셋까지 모두 CC0라고 가정하면 안 됩니다. Furniture Pack 2017은 로컬 라이선스 문서를 찾지 못한 이력이 있습니다.

주요 프리팹·라이브러리:

```text
Resources/MasterPlayer.prefab
Resources/MeshyPlayer.prefab
Resources/Modular/
Resources/Furniture/
Resources/Furniture/Generated/DesktopComputer.prefab
Resources/Furniture/Generated/FeedShelf.prefab
Resources/UI/Cozy/Library.asset
Resources/Environment/ApprovedNature/
ArtDirection/Prefabs/CozyHudAndBuildingCatalog.prefab
ArtDirection/CozyFurniture/
```

**현재 `Resources/UI/Cozy/Library.asset`에는 36개 가구 항목이 있습니다.** 초기 보고서의 “14종”은 이전 단계 수치입니다.

현재 항목:

- 계산대, 펫 쿠션, 목재 진열대, 쿠션 의자, 원목 테이블, 초록 화분.
- 사료 선반, 장난감 진열대, RAT 케이지, 수족관.
- 강아지 쿠션, 고양이 쿠션, 새장, 고양이 펫타워.
- 컴퓨터, 사료 진열대.
- 푹신한 안락의자, 쿠션 소파, 작은 테이블, 낮은 테이블.
- 수납장, 장식 서랍장, 높은 원목 선반, 상품 장식 선반.
- 스탠드 조명, 테이블 조명.
- 둥근 선인장, 긴 선인장.
- 타원 러그, 줄무늬 러그, 탁상 액자.
- 모던 의자, 라운지 의자, 스툴, 책 묶음, 작은 쿠션.

36개 데이터 항목이 있다는 것과 모든 조합의 구매·배치·저장·시각 품질 검증이 끝났다는 것은 다릅니다.

**6. 펫샵 실내·가구·증축·저장 구조**

펫샵 기본 구조:

- 초기 크기 **15×15m**, 225m².
- 기본 모듈 3m.
- 실내 바닥 기준 높이 +0.12m.
- 앞·오른쪽이 열린 실내 보기 방식.
- 지붕 표시 전환 지원.
- 건물 전체 Scale을 키워 증축하는 구조가 아닙니다.

신규 펫샵 기본 가구 11종:

1. 계산대
2. 컴퓨터
3. 의자
4. 사료 진열대
5. 장난감 진열대
6. 강아지 쿠션
7. 고양이 펫타워
8. RAT 케이지
9. 새장
10. 수족관
11. 화분

밝은 원목 바닥의 시각적 판자 메시와 기존 물리 바닥은 분리합니다.

가구 관련 파일:

```text
Scripts/PetTownFurniturePlanner.cs
Scripts/PetTownFurnitureGrid.cs
Scripts/PetTownFurniturePlacement.cs
Scripts/PetTownInteriorState.cs
Scripts/PetTownCozyLibrary.cs
Scripts/PetTownCozyInterior.cs
Scripts/PetTownCozyFloor.cs
```

가구 배치:

- 0.5m 실내 격자.
- R키 또는 버튼으로 90° 회전.
- 실제 모델 및 점유 영역 미리보기.
- 벽·가구·입구·통로 검사.
- 신규 의자에 임의의 비뚤어진 yaw를 적용하지 않습니다.
- 컴퓨터는 계산대·테이블 위 지지면을 검사하며 바닥 설치는 거부합니다.
- 수족관 회전 시 내부 물고기의 이동 좌표도 함께 처리합니다.
- 미리보기에서 실제 NPC·재고·가구를 등록하지 않습니다.
- 기존 배치 가구의 이동·삭제 UI는 완료된 기능으로 간주하지 마세요.

소유·저장:

- `PetTownInteriorState`: 건물 ID, 종류, 실내 크기·중심·원래 크기·입구.
- `PetTownFurnitureSaveRecord`: 가구 ID, 종류, 전문 가구 여부, 기본 가구 여부, 로컬 위치·회전, 모델 회전, 점유 크기, 모델 높이.
- 가구 좌표는 건물 기준 로컬 좌표로 저장합니다.
- 해석하지 못한 레코드를 무조건 삭제하지 않고 보존하는 경로가 있습니다.

증축 관련 파일:

```text
Scripts/PetTownExpansionPlan.cs
Scripts/PetTownExpansionShell.cs
Scripts/PetTownExpansionWindow.cs
Scripts/PetTownNatureObstacle.cs
```

증축:

- 펫샵 관리 → 증축 → 로컬 왼쪽·오른쪽·앞쪽·뒤쪽.
- 기본 깊이 3m.
- 펫샵 단가 20골드/m².
- 15m 변을 3m 늘리면 45m², 비용 900골드.
- 설정에는 카페 24골드/m², 병원 28골드/m² 기반도 있습니다.
- 최대 길이 기본값 45m.
- 실제 사용자 UI는 펫샵부터 지원합니다.
- 매장 레벨 업그레이드와 공간 증축은 다른 기능입니다.
- 증축에 플레이어 레벨 제한을 임의로 붙이지 않습니다.
- 건물 루트 위치·회전·Scale, 기존 가구 ID·로컬 배치를 유지합니다.
- 비대칭 확장 때문에 실내 중심과 원래 영역을 따로 저장합니다.
- 추가 바닥·벽·지붕, 바닥 높이, 입구, 도로 연결 조회, NavMesh에 확장을 반영합니다.
- 자연물이 있으면 노랑으로 표시하고 확정을 막습니다. 건설의 “도로 없는 노랑”과 의미가 다릅니다.
- 자연물은 별도 정리 목록에서 선택한 것만 제거합니다.
- 도로·다른 건물·장식 가구를 자동 삭제하지 않습니다.
- 증축 진행 중 일반 저장을 보류합니다.
- 연결·안전 확인 후 결제하고 저장하며, 실패 시 셸·영역·지형·바닥 등록 등을 복구합니다.
- 건물 이전 기능은 미구현입니다.

메인 저장은 **PlayerPrefs의 JSON 문자열**입니다.

```text
PetTown3D_Save
PetTown3D_StartingCapital100M_v1
PetTown3D_PetCare_v1     ← 기존 돌봄 저장 키, 삭제하지 않음
```

`SaveData`의 주요 구성:

```text
기존:
  coins, population, sold, inventory
  hair, face, body, legs, acc
  rep, level, day
  dogs, cats, bunnies, done, missed
  shopX, shopZ
  types[], x[], z[]

확장:
  constructionVersion = 2
  shopPlacement
  buildingPlacements[]

  roadVersion = 1
  roads[]

  interiorVersion = 1
  interiors[]
  clearedNature[]

  progressionVersion = 1
  progression
```

기존 `level`은 매장 레벨입니다. 새 플레이어 레벨과 혼동하지 마세요.

시작 자본 1억 골드 관련 플래그가 있으므로 이관 중 플래그를 빠뜨리거나 초기화하면 자본 처리 결과가 달라질 수 있습니다.

로드 과정에서 마이그레이션 후 다시 저장하는 코드가 있습니다. 따라서 단순한 Play 실행도 저장 데이터에 영향을 줄 수 있습니다.

**7. 캐릭터·NPC·카메라·이동**

주요 캐릭터 파일:

```text
Characters/Master/PetTown_MasterCharacter.fbx
Resources/MasterPlayer.prefab
Scripts/PetTownPlayerControls.cs
Scripts/PetTownCharacterAppearance.cs
Scripts/PetTownCharacterPreview.cs
Scripts/PetTownCustomizationUI.cs
Scripts/PetTownHairSwitcher.cs
Scripts/PetTownClothingSwitcher.cs
Scripts/PetTownAutoSkin.cs
Scripts/PetTownGarmentPoseFit.cs
Scripts/CharacterAnimationEvents.cs
```

기존 캐릭터 외형·머리·의상 변경과 미리보기 구조를 보존해야 합니다. 모든 외형 조합의 완전한 검증 기록까지 확보된 것은 아닙니다.

이동:

- 키보드 Horizontal/Vertical 입력.
- Shift 달리기.
- 모바일 조이스틱.
- 클릭·터치 목적지 이동.
- 기본 걷기 2.5m/s, 달리기 5m/s.
- Animator `Speed` 파라미터 사용, Root Motion 비활성.
- NavMesh 경로가 있으면 활용.
- CapsuleCast 기반 충돌과 벽을 따라 미끄러지는 축별 이동.
- 지형뿐 아니라 건물 실내 바닥 높이를 고려.
- 커스터마이징 팝업 중 이동 입력 차단.
- 도로 건설과 클릭 이동의 입력 충돌 방지.

카메라:

- 주 제어는 `PetTownGame`에 모여 있습니다.
- 직교 카메라, 플레이어 추적.
- 우클릭 드래그 회전.
- Q/E 45° 회전.
- 중클릭 드래그로 자유 이동.
- 자유 이동 후 플레이어가 실제로 움직이면 추적 복귀.
- 휠·두 손가락 핀치 확대/축소.
- R은 건물 배치 중 건물 회전, 가구 배치 중 가구 회전, 일반 상태에서는 카메라 초기화.
- 여러 스크립트가 동시에 카메라 Transform을 갱신해 떨림이 생기지 않도록 유지합니다.

NPC·동물:

- 기존 `NavMeshSurface`, `NavMeshAgent`, 배회·고객 행동을 유지합니다.
- 가구·벽의 실제 bounds를 로컬 좌표로 변환한 carving 장애물을 사용합니다.
- 장식 메시와 이동 장애물을 구분합니다.
- 주택 로드 후 주민 생성, NavMesh 갱신 순서가 중요합니다.
- 인구 숫자와 실제 생성된 NPC 수를 동일하다고 가정하면 안 됩니다.
- 현재 전시 동물 3마리는 **판매 가능한 개체 재고와 별도**입니다.

과거 Play 중 재컴파일로 NPC·동물 목록, UI 이벤트, 플레이어 경로 객체가 유실되어 제자리걸음·버튼 무반응·null 오류가 발생했습니다. 당시 Play 재시작으로 정상 초기화를 확인했습니다.

**앞으로 C# 수정과 컴파일은 Play 중지 상태에서 진행하는 것을 원칙으로 합니다.**

**8. HUD·건물·가구 목록·펫 관리 UI**

주요 파일:

```text
Scripts/PetTownCatalogWindow.cs
Scripts/PetTownCozyCatalogView.cs
Scripts/PetTownCozyHudView.cs
Scripts/PetTownHud.cs
Scripts/PetTownUITheme.cs
Scripts/PetTownGame.CozyPresentation.cs
Scripts/PetTownPetCareWindow.cs
ArtDirection/PetTownArtPreviewData.cs
ArtDirection/PetTownArtPreviewUI.cs
ArtDirection/Editor/PetTownMobileStyleBuilder.cs
```

구현 내용:

- 한국어 폰트와 TMP 기반 새 UI.
- 크림·원목 계열 패널과 이미지 중심 카드.
- 모바일용 작은 원형 아이콘 HUD.
- 실제 골드·날짜·주민·명성·레벨·경험치·행복도 연결.
- 건물·가구 검색, 분류, 정렬, 선택 상세, 큰 미리보기.
- 건설 가능한 목록 8종: 최초 펫샵을 제외한 건물 7종과 도로.
- 실제 모델 기반 건물 썸네일.
- 가구 라이브러리 기반 프리팹·썸네일.
- 펫 상태·성장·레벨·사료·돌봄·보호 설정.
- 주민 화면의 행복도 산식·사장 성장·분양 이력.
- 가방에서 사료 구매.
- 팝업과 배치·카메라·조이스틱 입력의 충돌 방지.
- 긴 설명의 상세 영역 겹침 보완.
- 안내 메시지 길이에 따라 검은 배경 폭 조절.

기존 HUD·환경 스타일 전환 경로도 이전 단계에서 구현됐습니다. 최신 화면에서의 접근성과 동작은 재검증 대상입니다.

`PetTownGame.cs`에 오래된 OnGUI·HUD 코드가 남아 있으므로 파일에 코드가 있다는 이유만으로 활성 UI라고 판단하지 마세요.

일반 아이템 전체 인벤토리, 모든 설정 메뉴, 모든 주민 상세 데이터가 완성된 것은 아닙니다.

**9. 펫 성장·분양·행복도 시스템**

참고한 원본 JavaScript 프로젝트:

```text
C:/git15/assets/README_HANDOFF.md
C:/git15/assets/HISTORY.md
C:/git15/assets/game.js
C:/git15/assets/data.js
C:/git15/assets/traits.js
C:/git15/assets/town.js
```

원본 JavaScript 프로젝트는 수정하지 않았습니다. 다른 컴퓨터에서는 이 경로가 없을 수 있으므로 후속 이식에 필요하면 자료도 별도로 확보해야 합니다.

현재 구현:

- 플레이어 레벨 1~100.
- 레벨업 보상: 새 레벨 × 50골드.
- 원본 레벨별 XP 임계값의 반올림 결과를 1~100까지 비교한 기록.
- 펫 레벨 1~10.
- 펫 필요 XP: `20 + 15 × 현재 레벨`.
- 펫 고유 ID, 이름, 성장, 상태, 보호 설정, 고정 외형.
- 기존 종류별 재고를 성체 개체로 한 번 이관.
- 실제 이관 당시 강아지·고양이·토끼 재고는 모두 0이었고 임의 재고를 지급하지 않음.
- 새 입고는 기존 가격·수량을 유지하며 어린 개체 2마리 등록.
- 기본 성장 시간은 활성 플레이 180초, 컨디션에 따라 지연.
- 날짜가 지났다는 이유만으로 XP를 지급하지 않음.
- 앱 비활성·일시정지 중 새 성장·상태 변화 정지.
- 오프라인 시간 소급 계산 없음.

분양 흐름:

```text
고객 안내 클릭/E
→ 요청에 맞는 개체 선택
→ 최종 유효성 검사
→ 분양 확정
→ 개체 제거·재고·골드·판매 수·평판·XP·이력 저장
```

최종 검사:

- 종 일치.
- 성장 완료.
- 보호 여부.
- 요청 유효 시간과 토큰.
- 실제 개체 존재.
- 중복 분양 방지.

분양가는 기존 Unity 수익에 컨디션·펫 레벨 보정과 원본 판매 계수 0.8을 적용합니다. 원본 게임의 모든 성격·희귀도·VIP·미용 보정까지 이식된 것은 아닙니다.

사료:

- 강아지·고양이 40골드/개.
- 토끼 25골드/개.
- 페럿 35골드/개.
- 5개 묶음 구매.
- 새 진행 데이터의 초기 사료 10/10/6/6.
- 무료 무한 먹이 지급 중단.
- 유효한 먹이·목욕·놀이에 사장 XP2와 펫 XP2.
- 필요 없는 돌봄에 보상 없음.
- 행동별 8초 재사용 제한.

행복도:

- 기본 45.
- 시설 기여 최대 40.
- 서비스 기여 -20~15.
- 수용 인원 초과 혼잡 페널티 최대 30.
- 공원·호수·카페·병원과 분양 성공·실패 반영.
- 70 초과 시설 기여 완화.
- 야생 나무 전체를 플레이어 조경 점수로 세지 않음.

아직 자동 입주·이탈, 주민별 행복도, 주택 정원과 실제 NPC 수의 일치 처리는 연결하지 않았습니다.

**10. 자연환경·셰이더·최근 수정 파일**

최신 자연환경은 승인된 GHIBLI style·Anime Trees 에셋을 본 게임에 연결한 상태입니다.

- 독립 시안 나무 185 → 93개.
- 본 게임의 기존 나무 루트 49개는 유지.
- 기존 루트 위치·충돌체·제거 ID를 유지하고 외형 교체.
- 승인 라이브러리 우선 로드, 이전 라이브러리 fallback.
- 실제 풀·꽃 메시 GPU 인스턴싱.
- 카메라 프러스텀·거리 처리와 프레임당 생성 제한.
- 건물·도로·물 영역에서 식생 제외.
- 장식용 강가 돌에 불필요한 충돌체·NPC 장애물 추가하지 않음.

공유 분포 마스크:

```text
Resources/Environment/ApprovedNature/MeadowCoverage.png
512×512
R = 흙
G = 식물 밀도
B = 풀 높이
A = 색 변화
```

지면 재질과 CPU 식생 배치가 같은 마스크를 사용합니다. 흙 공터 중심의 기존 산발적 풀 외형도 숨깁니다.

강:

- 큰 네온형 반복 물결을 작은 저대비 잔물결로 변경.
- 기존 중심선 누적 거리 UV 유지.
- `_FlowSpeed = .025`.
- 얕은 물·깊은 물, 젖은 흙·모래·자갈 경계 표현.
- 기존 강 메시·삼각형·UV·물 판정·건설 금지 계산을 시각 변경 때문에 교체하지 않음.

주요 셰이더:

```text
Resources/Materials/PetTownRiverSurface.shader
Resources/Materials/PetTownRoadSurface.shader
Resources/Materials/PetTownBuildGrid.shader
Resources/Materials/PetTownPlacementPreview.shader
Resources/Environment/PetTownGroundNatural.shader
Resources/Environment/PetTownRiverNatural.shader
Resources/Environment/PetTownCozyStream.shader
Resources/Environment/PetTownCozyMeadow.shader
Resources/Environment/PetTownCozyOak.shader
Resources/Environment/PetTownAssetFoliage.shader
ArtDirection/NatureReview/ForestGround.shader
ArtDirection/NatureReview/LivingGrass.shader
```

조명은 부드러운 실시간 그림자, 거리 140m, 2캐스케이드, 4096 shadowmap 적용 기록이 있습니다. 모바일 성능 적합성은 미검증입니다.

10월 26일 주요 변경:

| 파일 | 변경 내용 |
|---|---|
| `PetTownProgressionState.cs` | 신규 진행 데이터 |
| `PetTownGame.Progression.cs` | 개체 분양·성장·사료·행복도 |
| `PetTownGame.cs` | 저장 확장과 입고·판매·날짜 훅 |
| `PetTownGame.CozyPresentation.cs` | 새 데이터와 메뉴 연결 |
| `PetTownPetCareState.cs` | 실제 XP·성장·보호 데이터 |
| `PetTownPetCareWindow.cs` | 사료·성장·보호·선택 개체 연결 |
| `PetTownCozyHudView.cs` | 실제 레벨·XP·행복도 |
| `PetTownCatalogWindow.cs` | 정보 상세 표시 개선 |
| `PetTownCozyCatalogView.cs` | 카드 아이콘·긴 설명 겹침 개선 |
| `PetTownForestLibrary.cs` | 승인 라이브러리·공유 마스크 |
| `PetTownGrassField.cs` | 밀도·높이 변화·공터 제외 |
| `PetTownEnvironmentStyle.cs` | 지면 마스크 연결·공터 시각 처리 |
| `PetTownCozyLandscape.cs` | 불규칙한 강가 돌 군집 |
| `PetTownApprovedNatureBuilder.cs` | 정규화 프리팹·복제 재질·마스크 생성 |
| `ForestGround.shader` | 잔디·흙 텍스처 혼합 |
| `PetTownCozyStream.shader` | 잔물결·얕은 물·강둑 색 경계 |

추가로 유지할 수정:

```text
Assets/Houidisoft technology/
One Click Add Water -Stylized Water Shader/
Editor/Water_ProUpgradeWindow.cs
```

위 Editor 스크립트의 `[InitializeOnLoad]`를 제거하여 자동 광고 창만 막았습니다. 수동 메뉴와 물 렌더링 기능은 보존했습니다.

**11. 검증 결과·해결된 오류·남은 작업·보호 조건**

기록된 검증 결과:

| 범위 | 확인된 결과 |
|---|---|
| 건물 회전 | 73개 기본 검사, 실제 배치와 저장 복원 |
| 도로 | 두 모드 생성·비용·버튼 이벤트, 사용자 도로 2개 전체 레코드 재실행 일치 |
| 가구 | 초기 단계 14개, 후속 단계 15개 기록·위치·회전 재실행 일치 |
| 증축 | 15×15 → 18×15m, 900골드 차감, 기존 가구·루트 유지, 복원 |
| 증축 계산 | 네 회전 방향별 반복 계산과 JSON 왕복 |
| 실패 복구 | 실패 주입 후 지형 해시·면적·가구 기록 복구 |
| 이동 | NPC·동물의 실제 위치 변화와 NavMesh 유효성 |
| 진행 데이터 | 23개 데이터 검사 통과 |
| 개체 분양 | 격리 객체에서 284골드 1회 지급, 재고 1 감소, 이력 1 증가, 중복 거절 |
| 사료·돌봄 | 5개/200골드 구매, 먹이 1 소비, 만복 시 불필요한 소비·보상 차단 |
| 행복도 | 격리 검증에서 공원 추가 시 44 → 56 |
| 식생 | 최신 검사 30,899개 위치 중 제외 조건 위반 0, 공터 중심 식물 0 |
| 수면 | 고정 영역 두 프레임 비교로 미세 흐름 확인 |
| 최신 Editor | 해당 단계의 컴파일·Play Console 오류 0 기록 |

2026-10-25 Windows 개발 빌드는 성공했습니다.

```text
C:\UnityProjects\PetTown3D_AdditionalVerification_20261025\
WindowsBuild\PetTown3D.exe
```

- 오류 0.
- 경고 1: Unity 온라인 서비스 프로젝트 ID 연결 안내.
- 초기 디스크 부족 실패 후 공간 확보하여 재빌드 성공.
- EXE와 함께 Data·DLL 등이 필요.
- **10월 26일 자연환경·진행 시스템 변경 이후의 최신 빌드 성공을 의미하지 않음.**
- 해당 배포 실행 파일의 실제 조작 테스트는 미실시.

해결된 문제:

- 강 셰이더 `center` 중복 선언으로 발생한 핑크 렌더링: 변수명 수정 및 실제 화면 확인.
- Play 중 재컴파일 후 NPC 제자리걸음·버튼 무반응·경로 null: Play 재시작으로 초기화 확인.
- TMP 누락 include 4개: 설치 패키지 Essential Resources에서 복원한 기록.
- 물 에셋 자동 광고 팝업: 자동 실행 속성만 제거.
- 긴 주민 설명의 UI 넘침: 최신 단계에서 보완.

남아 있거나 다시 확인해야 할 부분:

- Android/iOS 실기기 성능·터치·배포 빌드.
- 최신 코드 기준 Windows 재빌드와 실행 파일 플레이.
- 실제 R키 입력 전체 시나리오.
- 최신 36개 가구의 모든 배치·지원면·증축 조합.
- 모든 방향·지형에서 실제 반복 증축과 NavMesh 통합 검증.
- 대규모 마을·장시간 플레이·FPS·GPU·메모리.
- 고양이·토끼의 개별 실제 3D 전시 모델·초상화. 현재 종류 아이콘으로 표시하며 강아지 모델로 대체하지 않음.
- 전시 동물과 판매 개체의 향후 관계 설계.
- 자동 입주·이탈, 개인 행복도, 주택 정원과 실제 NPC 수 일치.
- 미용, 직원, 배송, 교배, 미니게임, 퀘스트, 도감.
- 카페·병원 전체 영업.
- 건물 이전, 기존 가구 이동·삭제 UI.
- 도로 우선 이동·도로 연결 보너스·연결 보행로 자동 생성.
- 기존 일부 펫 애니메이션 경고.
- `.blend` 파일의 Blender 미설치 Import 문제. FBX·OBJ 대체본이 있으므로 원본 삭제로 해결하지 말 것.
- 일부 외부 모델 Import 경고.
- 목표 이미지와의 아트 품질 차이. Console 오류 0만으로 디자인 완성 판정 금지.

반드시 유지할 기능·조건:

- 기존 캐릭터 이동·애니메이션·리그.
- NPC 행동·NavMesh 구조.
- 카메라 조작과 입력 차단.
- 자유 곡선 도로와 직선 모드.
- 건설·가구 구매·결제·안전 검사.
- 증축 시 가구 보존과 실패 복구.
- 기존 저장 필드·버전 호환성·미해석 레코드 보존.
- 개체 ID·외형·보호 설정과 중복 분양 방지.
- 원본 에셋·텍스처·프리팹·`.meta`.
- 강·지형의 게임 데이터와 충돌 규칙.
- 사용자 미커밋·미추적 변경.

`git reset --hard`, `git clean`, 일괄 과거 백업 복원, 저장 초기화를 임의로 실행하지 마세요. 오래된 보고서의 상태를 근거로 최신 기능을 제거하거나 되돌리지 마세요.

**12. 다음 작업 권장 순서와 참고 자료**

1. **최신 작업본 확보부터 확인**
   - Git HEAD만 비교하지 말고 실제 소스·에셋·`.meta`를 확인.
   - `Resources/UI/Cozy/Library.asset`의 36개 항목 확인.
   - `PetTownGame.Progression.cs`와 승인 자연환경 폴더 확인.
   - 필요한 PlayerPrefs 저장과 시작 자본 플래그 별도 이관.
   - 외부 백업·보고서·원본 JavaScript 참고 자료도 필요 범위만 함께 확보.

2. **변경 전 기준점 마련**
   - Git 상태, 핵심 파일, 씬, 저장 데이터를 백업.
   - 현재 Unity 버전·URP·MCP 연결 확인.
   - 편집 모드에서 컴파일·셰이더·Import 오류를 분리해 점검.
   - 기존 작업본을 과거 ZIP으로 덮어쓰지 않기.

3. **최신 상태의 핵심 회귀 검사**
   - 플레이어·카메라·NPC·전시 동물 이동.
   - HUD와 팝업 입력 차단.
   - 건물 회전·도로 정렬·설치 거부.
   - 도로 두 모드와 재실행 복원.
   - 가구 지원면·회전·저장.
   - 증축 성공·실패·복원.
   - 분양·사료·성장·행복도와 중복 지급 방지.

4. **모바일 UI와 성능 확인**
   - 실제 화면 비율, 터치 영역, 조이스틱 간섭, 긴 한글 설명.
   - 식생 인스턴싱, 그림자, 수면의 GPU 비용 측정.
   - 성능 조정은 승인된 자연환경의 핵심 외형을 무작정 제거하지 않는 방향으로 진행.

5. **진행 시스템의 다음 연결**
   - 주택 정원·저장 인구·실제 NPC 수부터 일치.
   - 이후 자동 입주·이탈과 주민별 행복도.
   - 고양이·토끼 실제 모델과 개체 표시 구조 보완.
   - 직원·퀘스트·미용 등은 독립 단계로 추가.

6. **최신 빌드와 인수 검증**
   - 본 게임 씬을 명시한 Windows 빌드.
   - 실행 파일에서 실제 입력과 저장 재시작 검증.
   - 모바일 모듈 확보 후 실기기 검증.
   - 변경 기능·실제 확인·미검증 범위를 구분해서 보고.

최신 상태를 판단할 때 우선 읽을 문서:

```text
Assets/PetTown3D/ArtDirection/SYSTEMS_APPLIED_20261026.md
Assets/PetTown3D/ArtDirection/MEADOW_STREAM_UPDATE_20261026.md
Assets/PetTown3D/ArtDirection/NatureReview/APPLIED.md
Assets/PetTown3D/ArtDirection/LEGACY_SYSTEM_PORT.md
```

`LEGACY_SYSTEM_PORT.md`의 오래된 표에는 “아직 이식 전” 문구가 남아 있습니다. 최신 상태는 `SYSTEMS_APPLIED_20261026.md`와 실제 코드가 우선입니다. `ArtDirection/README.md`도 첫 시안 당시 문서이므로 현재 전체 상태로 읽으면 안 됩니다.

기존 단계별 보고서:

```text
C:/UnityProjects/PetTown3D_RoadStages56Backup_20261025/완료보고서.md
C:/UnityProjects/PetTown3D_RemainingStagesBackup_20261025/완료보고서.md
C:/UnityProjects/PetTown3D_AdditionalVerification_20261025/추가검증.md
C:/UnityProjects/PetTown3D_IntegratedAnalysis_20261025/통합분석보고서.md
```

최신 복구 사본:

```text
C:/UnityProjects/PetTown3D_ApprovedNatureBackup_20261026/
C:/UnityProjects/PetTown3D_MeadowVariationBackup_20261026/
C:/UnityProjects/PetTown3D_ProgressionBackup_20261026/
```

이 백업들은 전체 프로젝트 복제본이 아니라 단계별 수정 파일·상태·저장 사본일 수 있습니다.

실제 Game View 증거:

```text
Captures/MobileStyle/compact-hud-light-oak.png
Captures/NatureReview/nature-review-93-trees.png
Captures/ApprovedNature/village-applied-final.png
Captures/ApprovedNature/construction-check-active.png
Captures/MeadowVariation/meadow-stream-ready.png
Captures/Progression/individual-adoption.png
Captures/Progression/pet-level-food-care-final.png
Captures/Progression/resident-happiness-readable.png
```

분양·돌봄 일부 캡처는 격리된 검증용 개체를 사용한 화면입니다. 실제 저장에 판매 재고가 새로 지급되었다는 증거로 해석하지 마세요.
