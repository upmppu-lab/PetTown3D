# 펫 전시 자리(케이지 배정) 적용 — 2026-10-27

## 구현 내용
- 자리 = 플레이어가 매장에 실제로 배치한 가구(PetTownFurniturePlacement.Id). 새 3D 모델/에셋은 만들지 않았고 기존 소품을 사용합니다.
- 규칙(PetTownShopSlots.cs): RAT 케이지(소품 종류2) 토끼·페럿 정원2 / 강아지 쿠션(4) 강아지 정원1 / 고양이 펫타워(7) 고양이 정원2. 새장·수족관·장난감 진열대 등은 자리가 아님(해당 종이 없음).
- 배정은 펫 기록의 slotId(PetTownPetCareRecord)에 저장됩니다. 기존 저장은 slotId가 없어도 그대로 읽히고(빈 값으로 이관) 개체 ID/이름/레벨은 변하지 않습니다.
- 메뉴: 펫샵 관리 > '펫 전시 자리'(키31). 자리 목록(입주 n/정원) → 자리 선택 → 입주 펫 내보내기 / 입고 펫 넣기(다른 자리 펫은 옮김, 불가 사유 표시). 전시 동물(displayOnly)은 배정 불가.
- 이동: 배정된 펫은 UpdatePet의 목적지 선택에서 자리 가구 주변 고리(반경 0.75~1.2m)의 NavMesh 점만 고릅니다. 배정이 없거나 가구가 사라지면 기존 자유 활동 코드 그대로입니다.
- 가구를 치우면 목록을 열 때 자동으로 자유 활동으로 되돌립니다(해제 후 저장).

## 변경 파일 (Assets/PetTown3D/)
- 신규: Scripts/PetTownShopSlots.cs, Scripts/PetTownGame.ShopSlots.cs, ArtDirection/QA/PetTownShopSlotsQA.cs, PetTownShopSlotsMenuQA.cs, QA/ShopSlotsTest.unity(빌드 목록에 넣지 않음)
- 수정: Scripts/PetTownPetCareState.cs(slotId), PetTownGame.Operations.cs(메뉴 항목), PetTownGame.cs(메뉴 처리·UpdatePet 목적지 선택 3곳). 수정 전 원본: C:/UnityProjects/PetTown3D_ShopSlotsBackup_20261027

## 검증
- 컴파일 오류 0, Console error 0.
- PetTownShopSlotsQA 26/26(규칙·종/정원·옮기기·정리·구 저장 이관·JSON 왕복·이동 점 범위).
- PetTownShopSlotsMenuQA 44/44(격리 가짜 매장): 메뉴 클릭으로 배정/정원 초과 거부/종 불일치 거부/내보내기/빈자리 재배정, 쿠션 배정, 이동 목표 중심 = 해당 가구 위치, 미배정·전시·없는 펫은 중심 없음, 가구 제거 시 중심 사라짐+배정 자동 해제, 자리 0개 안내, 펫샵 관리에서 항목 진입. 화면 6종 글자 넘침/말줄임/44미만 버튼 0. 실제 저장+펫 돌봄 저장 해시 동일.
- 첫 실행에서 말줄임 3건 발견 → 문구 단축 후 재실행해 통과: '…정원 2인 자리예요' → '토끼·페럿만 지낼 수 있어요.', 빈 안내 '케이지·쿠션·펫타워'.
- 회귀: PoultryBreedingQA 29/29, FarmCropsQA 94/94, RanchBreedingQA 66/66. 종료 후 원본 장면 복구(dirty=false), QA 잔여 0.

## 확인하지 못한 것 / 한계
- **실제 펫샵에서 펫이 자리 주변으로 걸어가는 모습은 확인하지 못했습니다.** 이동 목표 계산(중심 위치·고리 범위)은 검사했지만, 실제 매장 NavMesh 위에서 경로가 나오는지·가구/벽에 걸리지 않는지는 미확인입니다. 가구가 매장 가장자리(중심에서 약 6.25~7.4m)보다 바깥이면 목표를 못 찾아 제자리에 있을 수 있습니다.
- 펫이 케이지 '안'에 들어가는 연출·전용 3D 케이지 모델은 없습니다(펫은 가구 주변 고리에서 지냄). 새장/수족관은 대상 종이 없어 자리가 아닙니다.
- UpdatePet은 실제 게임에서만 돌아 QA 장면에서 직접 실행하지 못했습니다. 이 함수 수정은 배정 펫에만 영향을 주도록 분기했고, 미배정 펫은 기존 코드와 값(6.25m)이 같습니다.

## 추가(같은 날 밤) — 펫 정보 창에 현재 자리 표시
- 펫 정보(돌봄) 창의 종·성별 줄에 자리 이름을 붙입니다. 예: '강아지 · 성별 미등록 · 강아지 쿠션 1'. 자리가 없거나 가구가 사라졌으면 표시하지 않습니다.
- 변경: PetTownPetCareState.cs(PetTownPetCareContext.slot), PetTownGame.Progression.cs(slot=ShopSlotName), PetTownGame.ShopSlots.cs(ShopSlotName), PetTownPetCareWindow.cs(SlotSuffix). 수정 전 원본: PetTown3D_ShopSlotsBackup_20261027/*.before_slotlabel
- 검증: 격리 메뉴 QA 53/53(실제 돌봄 창을 띄워 자리 문구 표시, 자리 없는 펫엔 미표시, 요약 글자 14 크기·넘침 없음). 회귀 4종 통과, 원본 장면 복구.
