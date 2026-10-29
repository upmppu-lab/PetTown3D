# 참고 이미지의 밝은 잔디색 적용 — 2026-10-26

사용자가 첨부한 잔디 사진의 색감에 맞춰, 진한 녹색을 낮은 채도의 밝은 황록색으로 조정했습니다. 사진은 색상 참고로만 사용했고, 사진이나 워터마크를 게임 텍스처로 사용하지 않았습니다.

## 변경 파일

- ArtDirection/NatureReview/ForestGround.shader: 기존 지면 명암 팔레트를 머티리얼에서 조정할 수 있도록 두 Vector 속성 추가. 기본값은 기존 팔레트와 동일.
- Resources/Environment/ApprovedNature/Materials/ApprovedGround.mat: 지면 명암 팔레트 조정.
- 같은 Materials 폴더의 Grass_03_23a7c21d.mat, Grass_02_7e42dceb.mat: 근거리·원거리 잔디의 뿌리/끝 색상 조정.

3D 잔디 메시·밀도·LOD·나무·꽃·흙 공터·강·그림자·기존 게임 기능은 이번 색상 작업에서 변경하지 않았습니다. 원본 Asset Store 자료도 변경하지 않았습니다.

## 검증

- 같은 카메라·같은 해상도의 실제 Game View로 전후 비교.
- 참고 이미지의 녹색 픽셀 대표값 RGB(122,152,57). 게임의 선택한 지면 영역에서 녹색 픽셀 중앙값은 이전 (73,130,25)에서 (124,155,54)로 변경. 사진과 게임의 조명/시점이 다르므로 모든 픽셀의 동일함을 뜻하지 않습니다.
- 근거리/줌 아웃 실제 화면 확인. 최종 Console 오류 0건.
- 실제 캡처: Captures/GrassColor/grass-reference-matched.png, grass-reference-far.png. before 및 after/reference-final로 이름 지은 다른 캡처는 변경 전 또는 중간 시안입니다.
- 카메라와 시간 배율, 게임 실행 상태를 복구한 뒤 Play 종료. 씬 저장/게임 로직 수정/저장 형식 변경/Git commit·push 없음.

복구 지점: C:/UnityProjects/PetTown3D_GrassColorBackup_20261026. 원본 파일·Git 상태·파일별 패치·색상 비교 수치를 보관했습니다.
