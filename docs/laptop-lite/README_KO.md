# 회사 노트북용 경량 개발 버전

브랜치: `laptop-lite-20261029` · Unity **6000.6.6f1**

집의 원본 프로젝트는 변경하지 않았습니다. 기본 MasterPlayer 캐릭터 한 종류와 베이지 말티즈 펫 한 종류만 포함합니다. 주민과 직원은 동일한 기본 캐릭터 모델을 사용하며, 다른 펫 종류의 화면상 모델도 말티즈로 표시됩니다. 게임 데이터의 종/직업 구분은 유지됩니다. 추가 헤어·의상 및 주민별 전용 애니메이션은 제외했습니다. 농장 동물과 수족관 콘텐츠는 별개 기능으로 유지했습니다.

## 가져오기

회사에 수정한 프로젝트가 있으면 먼저 별도로 보존하세요. 새 폴더로 복제합니다.

```powershell
git clone --depth 1 --branch laptop-lite-20261029 https://github.com/upmppu-lab/PetTown3D.git PetTown3D-Laptop
cd PetTown3D-Laptop
git lfs pull
```

Git LFS가 필요합니다. 브라우저 ZIP 다운로드보다 위 명령을 사용하세요.

1. Unity Hub에서 이 폴더를 추가하고 6000.6.6f1로 엽니다. Packages 의존성은 다운로드됩니다.
2. 아래 에셋들을 My Assets에서 원래 폴더 경로로 가져옵니다. 폴더 이름은 실제 스토어 상품명과 다를 수 있습니다. 집에서 사용한 것과 같은 버전을 사용하세요. `manual-asset-files.json`에 필요한 원본 파일 경로가 있습니다.
3. Unity를 닫고 프로젝트 루트에서 다음을 실행합니다. 집에서 수정한 머티리얼·프리팹·임포트 설정을 복원합니다.

```powershell
Expand-Archive -LiteralPath .\docs\laptop-lite\asset-overrides.zip -DestinationPath . -Force
```

4. Unity를 다시 열고 `Assets/PetTown3D/Scenes/PetTown3D_CharacterTest.unity`를 엽니다.
5. Console에서 오류를 확인한 뒤 Play를 누릅니다. 첫 실행은 패키지/에셋 임포트 때문에 시간이 걸립니다.

## 수동 설치할 에셋 폴더

- `3D set of stylized nature - GHIBLI style`
- `ADG_Textures`
- `Anime Trees`
- `Barking_Dog`
- `Brick Project Studio`
- `CozyFarmAssetPack`
- `Easy3D`
- `FREE_VERSION_30_MODELS`
- `Floreswa`
- `FurnishedCabin`
- `InnerverseInteractive`
- `Low-Poly Medieval Market`
- `LowPolyFarmLite`
- `LowPolyMedievalStarterPack`
- `Park`
- `Playground Low Poly`
- `Polytope Studio`
- `Starter Assets`
- `Stylized Nature MegaKit[Standard]`
- `TerrainDemoScene_HDRP`
- `ithappy`

## 검증 범위

경량 버전의 코드 컴파일과 파일/GUID 의존 관계를 점검합니다. 수동 에셋 설치를 마친 회사 노트북에서의 Unity 임포트 및 실제 Play 검증은 별도로 필요합니다. 이 저장소만 받은 상태에서는 일부 환경 모델/텍스처가 표시되지 않습니다.

전체 개발 이력을 보존하기 위해 기존 main 대신 별도 브랜치로 게시했습니다. `--depth 1`은 이전의 큰 파일 이력을 함께 받지 않도록 합니다. 세이브 데이터는 포함하지 않습니다.
