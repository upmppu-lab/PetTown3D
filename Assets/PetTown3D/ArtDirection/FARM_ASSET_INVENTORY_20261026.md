# 사용자가 Import한 농장·시장 에셋 확인 — 2026-10-26

이 단계에서는 Import·모델·머티리얼을 읽기 전용으로 확인했습니다. 농장/목장 건설 항목이나 생산 시스템을 활성화한 기록은 아닙니다. 기존 보류된 농업 소스는 Assets 밖에 유지합니다.

| 팩 | 실제 경로 | 파일 확인 |
|---|---|---|
| Low Poly Farm Pack Lite | Assets/LowPolyFarmLite | FBX 11, Prefab 11 |
| Cozy Farm Asset Pack | Assets/CozyFarmAssetPack | FBX 101, Prefab 101 |
| Stylized Farm Asset Pack - Small | Assets/Easy3D/Stylized Farm Asset Pack - Small | FBX 11, Prefab 16 |
| Low Poly Farm & Market 30 Free Models | Assets/FREE_VERSION_30_MODELS/FREE_VERSION_30_MODELS | FBX 30, OBJ 30, GLB 30 |

FREE 팩의 90개 파일은 30개 모델의 형식별 중복입니다. Unity의 t:Model 검색에는 60개가 잡혔고, GLB까지 각각 화면 품질을 검증한 것은 아닙니다. 게임에는 적합한 형식 한 종류를 선택해 사용합니다.

## 실제 조회한 머티리얼

- LowPolyFarmLite: Standard 1개.
- CozyFarmAssetPack: Universal Render Pipeline/Unlit 1개.
- Stylized Farm Small: Standard 8개.
- FREE 팩: URP/Lit 60개와 glTF PBR Shader Graph 30개.

현재 게임의 조명·그림자와 어울리도록 채택 시 검증이 필요합니다. Standard 머티리얼의 URP 호환성, Unlit의 동적 그림자 표현을 확인하고 필요한 경우 프로젝트용 복사본에서 조정합니다. 원본 팩을 통째로 일괄 변환하지 않았습니다.

사용자가 제공한 FREE 팩의 USAGE_TERMS.json에는 상업적 최종 제품 및 그 제품을 위한 수정 허용, 원본 모델 단독 판매·재배포·미러링 금지가 기재돼 있습니다. 다른 팩까지 라이선스를 새로 검증했다고 의미하지 않습니다.

실제 채택은 기존 마을의 스타일, 모델 크기, 모바일 폴리곤/텍스처 비용, 그림자 품질을 비교한 뒤 정합니다. 목장 에셋은 사용자가 계속 찾는 상태입니다.

파일/머티리얼의 상세 개수는 C:/UnityProjects/PetTown3D_GrassWindowsBackup_20261026/farm-file-inventory.json 및 farm-material-inventory.txt에 보관했습니다.
