# 선택한 돌 산책로 적용 — 2026-10-27

> 이후 동물원 개편과 함께 실제 풀·텍스처 반복 크기·부드러운 길 경계도 보완했습니다. 최신 미리보기와 검증은 ZOO_AND_MEADOW_APPLIED_20261027.md 및 Captures/ZooRedesign을 참고하세요.

사용자가 1000043523.jpg로 선택한 StylizedTextures / Katsukagi의 Stylized Stone Floor 005를 공원과 호수 내부 산책로에 적용했습니다. 기존 BPS 바닥 타일 사용 결과를 대체합니다. 일반 맵 도로 메뉴는 기존 흙길 1종을 유지합니다.

## 출처와 재질

- 원본: https://drive.google.com/drive/folders/1W2G9E44DR0HY0jKZ9E8Ixuu59KIXSWPs
- 배포자 FAQ 및 무료 상업 이용/CC0 안내: https://stylizedtextures.com/faq
- 원본 4K basecolor, normal, roughness, height, ambientOcclusion PNG를 ArtDirection/SourceModels/StylizedStoneFloor005에 보관했습니다. SOURCE.json과 LICENSE_SOURCE.txt에 출처를 기록했습니다.
- 기존 StonePromenade.mat의 GUID를 보존하고 선택한 재질로 연결했습니다. URP/Lit, 원본 색상, normal 강도 0.9, AO 강도 0.75입니다. roughness의 역수를 metallic map의 alpha로 패킹하고 RGB는 0으로 두어 비금속 표면을 사용합니다.
- Unity 최대 해상도: 색상/normal 2048, AO/roughness/패킹 mask 1024, 읽기 가능한 비압축 height 512. mipmap과 Repeat를 사용합니다. 원본 에셋 팩 재질은 덮어쓰지 않았습니다.

## 경로 구성

PetTownStoneWalkway가 완만한 곡선과 둥근 모서리를 가진 단일 산책로 메시를 생성합니다. 공원은 분수 주변 원형 포장, 십자 길, 벤치 연결부와 실제 출입구를 잇습니다. 호수는 기존 물 안전 영역 바깥을 도는 둥근 순환 산책로와 진입로를 사용합니다. 원본 텍스처의 돌 틈 풀이 유지되고, 별도로 배치하는 풀/꽃은 산책로에서 제외합니다.

메시의 UV는 시설 로컬 거리 4m마다 반복되어 교차부와 굽은 곳에서도 돌 크기가 일정합니다. 원본 높이 맵으로 약 2.23cm의 작은 표면 굴곡을 만들고 가장자리는 낮춥니다. 개별 돌을 별도 3D 프리팹으로 배치한 방식은 아닙니다. 시설 생성 시 한 번 만들고 시설 파괴 시 소유 메시를 정리합니다. 산책로 렌더러는 시설당 1개이며 공원 3816정점/6740삼각형, 호수 3244정점/5440삼각형입니다. 실제 모바일 기기 성능 측정은 아직 하지 않았습니다.

## 검증과 보존

시설별 8개 메시 검사 모두 통과: 선택 재질 연결, 높이 굴곡, PBR 맵, 거리 기반 UV, 물 안전 영역 침범 없음, 퇴화/중복 삼각형 없음, 방문 지점 포장 연결, 1만 미만 정점. 기본/90도 배치 각각 입구에서 4개 방문 지점으로 가는 총 16개 NavMesh 경로도 완전 연결됐습니다. 별도 평지 검사 결과이며 전체 저장 마을의 장시간 동작 검사와 구분합니다.

사용자 PlayerPrefs 저장 문자열이 작업 전과 정확히 같습니다. 실제 씬, PetTownPlayerControls.cs, PetTownGame.cs의 SHA256도 작업 전과 같습니다. Edit 모드, 씬 dirty=false, QA 객체 0, 컴파일 오류 0을 확인했습니다. 사용자 마을에 시험 시설이나 재고를 추가하지 않았습니다.

Captures/ChosenPavers/park.png와 lake.png는 실제 프로젝트 에셋으로 생성한 격리된 Unity 미리보기이며 두 건설 썸네일도 갱신했습니다. 검사 증거는 ArtDirection/ChosenPavers에 있습니다. 원본 백업: C:/UnityProjects/PetTown3D_ChosenPaversBackup_20261027. Git 커밋/푸시 및 유료 구매는 하지 않았습니다.
