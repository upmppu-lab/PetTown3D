# 참고 이미지 기준 포장석 후보 — 2026-10-27

기존 BPS Building Floor Tile 재질과 직선 직사각 산책로는 사용자에게 거절됐습니다. 새 기준은 밝은 크림색, 큰 석판, 닳은 모서리/두께, 표면 얼룩과 균열, 약간씩 어긋난 배치와 완만한 길입니다. 같은 벽돌 무늬를 평평한 면에 반복하는 방식으로 대체했다고 말하지 않습니다.

## 확인한 실제 자료

1. **보유 Ghibli Stone_Slab**: Assets/3D set of stylized nature - GHIBLI style/Art/Prefabs/Stone_Slab.prefab. 실제 입체 석판이며 보이는 메시246정점, 크기 약2.01×0.35×2.01m, 모서리에 작은 마모/깨짐과 원본 PBR 질감이 있습니다. 원본 색상은 어두운 회색이며 참고의 밝은 색/중앙 균열과 같지 않습니다. Native 미리보기: Captures/PaverCandidates/StoneSlab_Original.png. Cream으로 이름 붙은 비교 이미지의 _BaseColor 조정은 해당 셰이더에서 시각 변화가 없어 채택하지 않았습니다.
2. **Stylized Stone Floor 007**: 작가 StylizedTextures.com의 무료 CC0 PBR 재질입니다. 모서리가 닳고 금이 간 개별 포장석과 흙 틈이 보입니다. 참고보다 돌색이 회색이고 흙 틈이 넓습니다. 원본 4K basecolor/normal/height/roughness/AO 5맵을 다운로드해 이미지 형식과 크기를 확인했습니다.
3. **Stylized Stone Floor 010**: 같은 작가의 무료 CC0 PBR 재질입니다. 따뜻한 색과 불규칙한 판석 조각/표면 결이 있지만 참고의 큰 정사각 슬랩과 형태가 다릅니다. 원본 4K 5맵을 다운로드해 확인했습니다.
4. **Stylized Stone Floor 008**: 큰 사각 석판과 큰 균열을 표현하므로 참고의 형태에 가깝지만 주황빛이 강합니다. 작가 미리보기만 다운로드해 확인했고 원본 맵은 아직 다운로드하지 않았습니다.

이 외007/010 등 후보는 3D 길 완성 모델이 아닌 재질입니다. 별도로 움직일 수 있는 입체 돌을 만들려면 실제 석판 모델과 재질을 조합해 크기/각도를 조금 다르게 놓아야 합니다. 경로는 곡선으로 배치하고 개체 발 높이/입구/물 안전 경계를 확인해야 합니다. 이 검토 단계에서는 사용자 마을이나 공원/호수의 현재 경로를 새 후보로 교체하지 않았습니다.

## 출처와 파일

작가 카탈로그: https://stylizedtextures.com/browse
CC0 설명: https://stylizedtextures.com/faq
카탈로그에서 직접 참조한 CSV: https://raw.githubusercontent.com/gendosplace/Material_Library/refs/heads/main/StylizedTextures.csv
개별 폴더/원본 맵 URL과 SOURCE.json: C:/Users/seung/Downloads/PetTown_Paver_References_20261027.

미리보기는 원작자 자료이며 직접 그리거나 생성한 이미지가 아닙니다. 유료 상품을 구매하지 않았습니다. 보유 모델 확인용 임시 씬은 정리했고 게임 저장/원본 에셋을 변경하지 않았습니다.
