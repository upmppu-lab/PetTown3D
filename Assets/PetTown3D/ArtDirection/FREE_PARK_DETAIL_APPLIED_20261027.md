# 무료 에셋으로 분수·화단·정자 교체 — 2026-10-27

유료는 제외한다는 사용자 요청에 따라 비용 없이 내려받을 수 있고 상업적 사용이 가능한 CC0 원본 3종만 사용했습니다. 유료 팩을 구매하지 않았습니다. 기존 Park의 단순 분수·색면 화단·Hovel 정자를 실제 생성 라이브러리에서 교체했고 건설 썸네일도 갱신했습니다.

## 원본과 적용

- **Asset Quest — Stylized Garden Asset Pack FREE DEMO**. https://assetquest.itch.io/stylized-garden-asset-pack. 공개 무료 데모의 Licence_DEMO.txt에서 CC0를 확인했습니다. 유료 본편은 사용하지 않았습니다. 수레국화·코스모스·알리움·거베라·델피니움·양귀비 6종과 테라코타 화분으로 3개 화분의 꽃 군락을 조합했습니다. 기존 평평한 색면 화단을 없앴습니다. 원본 색/불투명도 채널을 URP용 RGBA 아틀라스로 패킹하고 alpha clip·양면 잎·밉맵을 설정했습니다. 공원/호수 꽃도 새 모델을 활용합니다.
- **Innerscene — Octagonal gazebo original exterior model**. https://www.innerscene.com/tools/library/3d-parts/octagonal-gazebo-original-exterior-model-07eaba19. 원본 페이지의 CC0 선언 확인. 실제 목재 텍스처·기둥·난간·팔각 지붕/바닥이 있는 모델이며 게임 부지에 맞게 비율을 유지해 줄였습니다. 여러 부재를 재질별로 합쳐 정자 렌더러를 2개로 줄였습니다. 유료 또는 계정이 필요한 Romanian Wooden Gazebo 모델을 가져온 것으로 가장하지 않습니다. 이것은 별도의 무료 모델입니다.
- **Frakassets / Pixor — Fountain**. https://pixor.art/library/elements/fountain/. 원본 페이지의 CC0 선언 확인. 2,248 삼각형의 3D 모델, 돌 분수대·4방향 물줄기·낙수·거품, 원본 Flow 클립을 사용합니다. 스프라이트로 대체하지 않았습니다. 원본 블렌드셰이프 애니메이션을 재생하고 GameObject 파괴 시 복제 클립을 정리합니다. 기존 분수의 녹색 팔레트 변환 셰이더는 새 모델에 적용하지 않습니다.

원본과 출처/라이선스는 ArtDirection/SourceModels/AssetQuestGardenDemo, InnersceneGazebo, PixorFountain에 기록했습니다. 새 URP 재질·프리팹·합쳐진 메시·기술적 텍스처 변환 결과는 ArtDirection/Prefabs/ParkDetail입니다. Editor/PetTownParkDetailAssets.cs의 Build()로 재생성할 수 있습니다. 기존 사용자 임포트 팩은 덮어쓰지 않았습니다.

## 검증

Captures/ParkDetail의 CC0_Fountain.png / TerracottaFlowerGarden.png / CC0_TimberGazebo.png는 실제 교체 프리팹을 Unity 카메라에서 렌더한 모습입니다. park.png는 별도 검사용 공원 전체입니다. 사용자 마을에 검사용 공원을 지급하지 않았습니다.

원본 물 애니메이션 부착과 시각이 다른 두 프레임의 블렌드셰이프 변화를 확인했습니다. 기존 거절한 세 Park 모델을 참조하지 않는 것을 확인했습니다. 0도/90도 배치에서 입구부터 네 방문 지점까지 완전한 NavMesh 경로 8개가 모두 통과했습니다. 별도 평지 충돌 형상 베이크 검사이며 실기기 장시간 경로 검사는 아닙니다. ParkDetail/runtime-tests.txt와 navigation-tests.txt에 증거를 보관했습니다.

사용자 저장은 에디터 Play 확인 중 정상적으로 약 15초 진행된 환경 시계 값을 제외하고 모든 필드가 같았습니다. 주민·재고·펫 ID·골드·날짜·가구·도로·건물 배치·탐색을 바꾸지 않았고 검사용 데이터가 없습니다. 씬과 플레이어 코드의 SHA256도 같았습니다. ParkDetail/preservation-tests.json. 검사용 객체를 제거하고 Play를 종료했습니다. 마지막 Console error0을 확인했습니다. 실제 모바일 성능 검사는 아직 남았습니다. Git 커밋/푸시는 하지 않았습니다.

백업: C:/UnityProjects/PetTown3D_ParkDetailBackup_20261027. 종전 공원 미술 기록은 SCENERY_REDO_APPLIED_20261027.md이며 이 문서가 위 세 모델의 최신 상태입니다.

## 후속 요청: 배경 나무와 통일

공원·호수의 나무도 Park의 각진 모델에서 현재 맵 배경의 PetTownForestLibrary.Current(ApprovedNature)로 교체했습니다. 공원은 Ghibli_Tree_01/Anime_Tree_02, 호수는 Ghibli_Tree_03/Anime_Tree_04입니다. 원본 나뭇잎 재질과 비율을 유지하고 부지에 맞는 높이만 조절합니다. 공원 오른쪽 나무의 줄기를 정자 바깥으로 옮겼고 두 시설 모두 나무 수는 각2그루를 유지합니다. 배경 맵이나 동물원 나무 선택은 바꾸지 않았습니다.

나무 교체 뒤 공원/호수 0도·90도 각각 네 휴식 지점까지 경로16개 모두 통과했습니다. ParkDetail/park-tree-nav.txt, lake-tree-nav.txt. Captures/ParkDetail/park.png와 lake.png 및 두 건설 썸네일을 최종 모습으로 다시 렌더했습니다. 이 단계는 Edit 모드의 별도 미리보기에서 검사해 사용자 저장을 더 진행시키지 않았습니다.
