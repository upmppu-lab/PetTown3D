using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;

// Touch a pet, a resident, a sign or the hero to open that thing's status window.
public partial class PetTownGame
{
    Vector2 touchPressPosition;
    float touchPressTime;
    bool touchPressing;
    int touchFinger = -1;

    public void TouchTick()
    {
        if (mode != Mode.Play || loadingTown || expansionPending || !cam) return;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) { touchPressing = true; touchPressPosition = t.position; touchPressTime = Time.unscaledTime; touchFinger = t.fingerId; }
            else if (t.phase == TouchPhase.Canceled) touchPressing = false;
            else if (t.phase == TouchPhase.Ended && touchPressing && t.fingerId == touchFinger) { touchPressing = false; TouchClick(t.position, t.fingerId); }
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) { touchPressing = true; touchPressPosition = Input.mousePosition; touchPressTime = Time.unscaledTime; touchFinger = -1; }
            else if (Input.GetMouseButtonUp(0) && touchPressing) { touchPressing = false; TouchClick(Input.mousePosition, -1); }
        }
    }

    void TouchClick(Vector2 position, int finger)
    {
        if ((position - touchPressPosition).magnitude > 18f || Time.unscaledTime - touchPressTime > .5f) return;
        // The virtual joystick area is for walking only.
        if (position.x < Screen.width * .38f && position.y < Screen.height * .36f && Input.touchCount > 0) return;
        if (PetTownCustomizationUI.IsScreenPointOverUI(position)) return;
        if (EventSystem.current != null && (finger < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(finger))) return;
        if (decorSelected != -1) { TryPlaceDecor(position); return; }
        if (TryTouchPetCrate(position)) return;
        if (TryTouchShelf(position)) return;
        TryInspect(position);
    }

    enum InspectKind { Resident, ResidentPet, ShopPet, Sign, Hero }

    sealed class InspectTarget
    {
        public InspectKind kind;
        public string id, extra;
        public GameObject obj;
        public float score;
    }

    void TryInspect(Vector2 screen)
    {
        float limit = Mathf.Max(52f, Screen.height * .06f);
        InspectTarget best = null;
        System.Action<Vector3, InspectKind, string, string, GameObject, float> consider = (world, kind, id, extra, obj, bias) =>
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return;
            float d = Vector2.Distance(new Vector2(sp.x, sp.y), screen) - bias;
            if (d > limit) return;
            if (best == null || d < best.score) best = new InspectTarget { kind = kind, id = id, extra = extra, obj = obj, score = d };
        };
        foreach (var pair in residentActors)
            if (pair.Value && pair.Value.activeInHierarchy) consider(pair.Value.transform.position + Vector3.up * 1f, InspectKind.Resident, pair.Key, null, pair.Value, 0);
        foreach (var pair in residentCompanions)
            if (pair.Value != null && pair.Value.root && pair.Value.root.activeInHierarchy) consider(pair.Value.root.transform.position + Vector3.up * .35f, InspectKind.ResidentPet, pair.Value.petId, pair.Value.ownerId, pair.Value.root, 6);
        foreach (var pair in shopPetActors)
            if (pair.Value != null && pair.Value.root && pair.Value.root.activeInHierarchy) consider(pair.Value.root.transform.position + Vector3.up * .35f, InspectKind.ShopPet, pair.Key, null, pair.Value.root, 6);
        foreach (var live in livePets)
            if (live != null && live.root && !live.inventory && live.root.activeInHierarchy && !string.IsNullOrEmpty(live.careId)) consider(live.root.transform.position + Vector3.up * .35f, InspectKind.ShopPet, live.careId, null, live.root, 6);
        foreach (var tag in FindSignTags())
            if (tag && tag.isActiveAndEnabled) consider(tag.BoardPosition, InspectKind.Sign, null, null, tag.building, 22);
        if (hero && hero.activeInHierarchy) consider(hero.transform.position + Vector3.up * 1.1f, InspectKind.Hero, null, null, hero, -8);
        if (best == null) return;
        switch (best.kind)
        {
            case InspectKind.Resident: OpenResidentCard(best.id); break;
            case InspectKind.ResidentPet: OpenResidentPetActions(best.extra, best.id); break;
            case InspectKind.ShopPet:
                OpenPetCare();
                if (petCareWindow) petCareWindow.SelectPet(best.id);
                break;
            case InspectKind.Sign: OpenBuildingCard(best.obj); break;
            case InspectKind.Hero: OpenTownProgression(); break;
        }
    }

    static string HomeIdOf(GameObject b)
    {
        var metadata = b.GetComponent<PetTownBuildingPlacement>();
        string id = metadata != null && metadata.Record != null ? metadata.Record.id : null;
        if (string.IsNullOrEmpty(id))
        {
            var p = b.transform.position;
            id = "legacy-house:" + p.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ":" + p.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
        return id;
    }

    int BuildingKindOf(GameObject b)
    {
        if (!b) return -1;
        var placement = b.GetComponent<PetTownBuildingPlacement>();
        if (placement != null && placement.Record != null) return placement.Record.kind;
        var interior = b.GetComponent<PetTownInteriorState>();
        if (interior != null) return interior.Kind;
        var community = b.GetComponent<PetTownCommunityView>();
        if (community != null) return community.Kind;
        if (b.name.StartsWith("Resident_House")) return 2;
        if (b.name.StartsWith("Farm")) return 9;
        if (b.name.StartsWith("Ranch")) return 10;
        return -1;
    }

    string BuildingIdOf(GameObject b)
    {
        var placement = b.GetComponent<PetTownBuildingPlacement>();
        if (placement != null && placement.Record != null && !string.IsNullOrEmpty(placement.Record.id)) return placement.Record.id;
        var interior = b.GetComponent<PetTownInteriorState>();
        if (interior != null && !string.IsNullOrEmpty(interior.Id)) return interior.Id;
        var community = b.GetComponent<PetTownCommunityView>();
        if (community != null) return community.Id;
        return null;
    }

    // The sign's status window: each building opens its own management screen.
    void OpenBuildingCard(GameObject b)
    {
        if (!b) return;
        int kind = BuildingKindOf(b);
        string id = BuildingIdOf(b);
        switch (kind)
        {
            case 0: OpenShopManagement(); break;
            case 1: OpenHomeCard(b); break;
            case 2: OpenHouseCard(HomeIdOf(b)); break;
            case 3: case 4: if (!string.IsNullOrEmpty(id)) OpenFacilityBusiness(id); else OpenFacilities(kind); break;
            case 5: case 6: case 7: if (!string.IsNullOrEmpty(id)) OpenCommunityLot(id); else OpenCommunityFacilities(); break;
            case 9: if (!string.IsNullOrEmpty(id)) OpenFarmLot(id); else OpenFarmManagement(); break;
            case 10: if (!string.IsNullOrEmpty(id)) OpenRanchLot(id); else OpenRanches(); break;
        }
    }

    static PetTownCatalogEntry CardRow(int key, int icon, string name, string category, string value, string detail, string action, string description)
    {
        return new PetTownCatalogEntry { Key = key, IconKey = icon, TextOnlyDetail = true, Name = name, Category = category, PriceLabel = value, FootprintLabel = detail, ActionLabel = action, Description = description };
    }

    void OpenHouseCard(string homeId)
    {
        SetMode(Mode.Play);
        var state = EnsureResidents();
        var homes = ResidentHomes();
        var home = homes.Find(h => h.id == homeId);
        var members = state.people.FindAll(p => p.homeId == homeId);
        var entries = new List<PetTownCatalogEntry>();
        var ids = new List<string>();
        foreach (var person in members)
        {
            PetTownResidentProfile.Ensure(person);
            ids.Add(person.id);
            entries.Add(CardRow(ids.Count - 1, 5, person.name, "거주자", person.job, "펫 " + person.pets.Count + "마리 · 행복도 " + Mathf.RoundToInt(person.happiness) + "%", "주민 정보",
                "이 집에 사는 주민입니다.\n" + ResidentStatusText(person) + "\n소지금 " + person.money + " 골드"));
        }
        if (entries.Count == 0)
        {
            ids.Add(null);
            entries.Add(CardRow(0, 0, "비어 있는 집", "주택", "0 / " + (home != null ? home.capacity : 2) + "명", "입주 대기 중", "주택 목록", "새 날이 되면 행복도와 빈 집 수에 따라 주민이 입주합니다."));
        }
        string title = home != null ? home.label : "주민 주택";
        catalogWindow.Show(title + " · " + members.Count + "/" + (home != null ? home.capacity : 2) + "명", entries, e =>
        {
            string pick = e.Key >= 0 && e.Key < ids.Count ? ids[e.Key] : null;
            if (!string.IsNullOrEmpty(pick)) OpenResidentCard(pick); else OpenResidentHousing();
        }, () => { }, () => { });
    }

    // Resident window: home (resident registry), pets, state, happiness, money and job.
    void OpenResidentCard(string residentId)
    {
        SetMode(Mode.Play);
        var state = EnsureResidents();
        var person = state.people.Find(p => p.id == residentId);
        if (person == null) { OpenResidentRoster(); return; }
        PetTownResidentProfile.Ensure(person);
        var homes = ResidentHomes();
        var home = homes.Find(h => h.id == person.homeId);
        var mates = state.people.FindAll(p => p.id != person.id && !string.IsNullOrEmpty(person.homeId) && p.homeId == person.homeId);
        string mateNames = mates.Count == 0 ? "혼자 살고 있어요" : "함께 사는 주민: " + string.Join(", ", mates.ConvertAll(p => p.name).ToArray());
        string petNames = person.pets.Count == 0 ? "아직 함께 사는 펫이 없어요" : string.Join(", ", person.pets.ConvertAll(p => p.name).ToArray());
        string status = ResidentStatusText(person);
        int happiness = Mathf.RoundToInt(person.happiness);
        int salary = PetTownResidentProfile.Salary(person);
        var entries = new List<PetTownCatalogEntry>
        {
            CardRow(0, 5, "현재 상태", "주민", status, PetTownResidentProfile.MoodLabel(person.happiness), "새로고침", "지금 하고 있는 일입니다.\n움직이면서 계속 바뀌어요."),
            CardRow(1, 5, "행복도", "주민", happiness + "%", PetTownResidentProfile.MoodLabel(person.happiness), "마을 요약", "마을 시설과 서비스가 좋아지면 조금씩 올라갑니다.\n행복도가 낮은 날이 이어지면 이사를 갈 수도 있어요."),
            CardRow(2, 0, "사는 집 (주민등록)", "주택", home != null ? home.label : "주택 배정 대기", mateNames, home != null ? "주택 보기" : "주택 목록", "입주: Day " + person.joinedDay + " (" + Mathf.Max(1, day - person.joinedDay + 1) + "일째)\n" + (home != null ? "마을 주민 명부에 등록된 실제 주택입니다." : "빈 주택이 생기면 입주합니다.")),
            CardRow(3, 9, "함께 사는 펫", "펫", person.pets.Count + "마리", petNames, person.pets.Count > 0 ? "펫 보기" : "펫 분양", "펫을 분양받으면 이곳에 표시됩니다.\n최대 " + PetTownResidentState.PetLimit + "마리까지 키울 수 있어요."),
            CardRow(4, 5, "소지금", "재산", person.money.ToString("N0") + " 골드", "하루 수입 약 +" + salary + " 골드", "마을 요약", "직업으로 번 돈에서 생활비와 펫 돌봄비를 뺀 금액입니다."),
            CardRow(5, 5, "직업", "직업", person.job, "일당 약 " + salary + " 골드", "마을 요약", person.name + "의 직업은 " + person.job + "입니다.\n새 날이 시작될 때마다 월급이 들어와요.")
        };
        catalogWindow.Show(person.name + "님", entries, e =>
        {
            switch (e.Key)
            {
                case 0: OpenResidentCard(residentId); break;
                case 2: if (home != null) OpenHouseCard(home.id); else OpenResidentHousing(); break;
                case 3: if (person.pets.Count > 0) OpenResidentPets(residentId); else OpenAdoptionSelection(); break;
                default: OpenResidentRoster(); break;
            }
        }, () => { }, () => { });
    }
}
