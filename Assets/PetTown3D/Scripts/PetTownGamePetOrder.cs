using System.Collections.Generic;
using UnityEngine;

// 펫 주문(종류·성별·외형 선택) + 펫샵 앞에 상자로 도착하는 배달.
public sealed class PetTownCrateTag : MonoBehaviour { public string deliveryId; }

public partial class PetTownGame
{
    static readonly int[] PetOrderPrices = { 70, 70, 100, 110, 80, 60, 60, 120, 90 };
    readonly Dictionary<string, GameObject> petCrates = new Dictionary<string, GameObject>();
    float nextCrateCheck;
    static bool KoreanFinalConsonant(string s){if(string.IsNullOrEmpty(s))return false;char c=s[s.Length-1];return c>=0xAC00&&c<=0xD7A3&&(c-0xAC00)%28!=0;}
    int OrderPriceFor(int species,int appearance)=>PetOrderPrices[species]*(PetTownRarity.IsRare(species,appearance)?3:1);

    string PetDeliveryName(PetTownDeliveryRecord p)
    {
        var dv = PetTownPetVisualLibrary.Current != null ? PetTownPetVisualLibrary.Current.Find(p.species, p.appearance) : null;
        string look = dv != null && !string.IsNullOrEmpty(dv.displayName) ? " · " + dv.displayName : "";
        return PetTownProgressionState.Species[p.species] + look + " · " + (p.sex == 1 ? "수컷 ♂" : "암컷 ♀");
    }

    // onlySpecies: -1이면 모든 종류, 그 외에는 그 종류만.
    void OpenPetOrder(int onlySpecies)
    {
        if (loadingTown || expansionPending || !OperationsReady()) return;
        SetMode(Mode.Play);
        var entries = new List<PetTownCatalogEntry>();
        var picks = new List<int[]>();
        var lib = PetTownPetVisualLibrary.Current;
        for (int species = 0; species < PetTownProgressionState.Species.Length; species++)
        {
            if (onlySpecies >= 0 && onlySpecies != species) continue;
            int looks = lib != null ? Mathf.Max(1, lib.CountLooks(species)) : 1;
            int price = PetOrderPrices[species];
            string sname = PetTownProgressionState.Species[species];
            string need; int free = OrderableSlots(species, out need);
            for (int a = 0; a < looks; a++)
            {
                var vis = lib != null ? lib.Look(species, a) : null;
                int appearanceIndex = vis != null ? vis.appearance : a;
                price = OrderPriceFor(species, appearanceIndex);
                string look = vis != null && !string.IsNullOrEmpty(vis.displayName) ? " " + vis.displayName : (looks > 1 ? " 외형 " + (a + 1) : "");
                var entry = new PetTownCatalogEntry
                {
                    Key = picks.Count,
                    Name = look.Length > 0 ? look.Trim() : sname,
                    Category = sname,
                    Price = price,
                    PriceLabel = price + " 골드" + (free > 0 ? " · 빈 자리 " + free : ""),
                    ActionLabel = "사기",
                    Description = "어린 " + sname + " 한 마리를 주문합니다. 오른쪽 ♂/♀ 버튼으로 성별을 고를 수 있어요 (기본: 수컷).\n주문 후 활성 플레이 " + Mathf.RoundToInt(PetTownShopOperationsState.PetDeliverySeconds) + "초가 지나면 배달 입구에 펫 상자가 도착해요.\n상자를 누르거나 배송함에서 열면 펫이 나와요.\n성체(Lv.3)까지 돌보면 분양과 교배를 할 수 있어요.",
                    Thumbnail = vis != null ? vis.portrait : null,
                    IconKey = 9,
                    ToggleLabels = new[] { "♂", "♀" },
                    ToggleTints = new[] { new Color(.36f, .62f, .96f), new Color(.97f, .48f, .72f) },
                    ToggleInk = new[] { new Color(.16f, .42f, .92f), new Color(.93f, .28f, .60f) },
                    ToggleValue = 0
                };
                int slotsFree = free; string slotNeed = need, kindName = sname;
                entry.SizeProvider = () => slotsFree > 0 ? kindName + (entry.ToggleValue == 0 ? " 수컷" : " 암컷") + " · 1마리" : slotNeed + (KoreanFinalConsonant(slotNeed) ? "이" : "가") + " 필요해요 (자리 없음)";
                entries.Add(entry);
                picks.Add(new[] { species, 1, appearanceIndex });
            }
        }
        if (entries.Count == 0) { Message("주문할 수 있는 펫이 없어요."); return; }
        catalogWindow.Show("펫 주문", entries, e =>
        {
            if (e.Key < 0 || e.Key >= picks.Count || !OperationsReady()) return;
            var pk = picks[e.Key];
            int sexPick = e.ToggleValue == 0 ? 1 : 2;
            string reason;
            { string needNow; if (OrderableSlots(pk[0], out needNow) <= 0) { Message(needNow + "를 펫샵에 놓아야 " + PetTownProgressionState.Species[pk[0]] + "을(를) 주문할 수 있어요."); return; } }
            if (Operations.OrderPet(pk[0], sexPick, pk[2], OrderPriceFor(pk[0], pk[2]), ref money, out reason))
            {
                progressionDirty = true; SaveProgressionSilent();
                Message(PetTownProgressionState.Species[pk[0]] + (sexPick == 1 ? " 수컷" : " 암컷") + "을 주문했어요. 곧 배달 입구에 상자가 도착해요!");
            }
            else Message(reason);
        }, () => { }, () => { });
    }

    public const int DeliveryGateId = 404;
    Vector3 crateGatePos;
    float nextGateTry;

    PetTownDecorTag FindDeliveryGate()
    {
        foreach (var t in decorTags) if (t && t.defId == DeliveryGateId) return t;
        return null;
    }

    void RemoveDeliveryGate()
    {
        for (int i = decorTags.Count - 1; i >= 0; i--)
        {
            var t = decorTags[i];
            if (!t) { decorTags.RemoveAt(i); continue; }
            if (t.defId != DeliveryGateId) continue;
            decorSave.items.Remove(t.record); decorTags.RemoveAt(i); Destroy(t.gameObject);
        }
    }

    // 배달 입구가 없으면 펫샵에서 떨어진 평탄하고 길 가까운 빈 땅에 자동으로 만든다.
    void EnsureDeliveryGate()
    {
        if (!decorLoaded || FindDeliveryGate() != null || Time.unscaledTime < nextGateTry) return;
        nextGateTry = Time.unscaledTime + 5f;
        var def = PetTownDecorDefs.Get(DeliveryGateId); if (def == null) return;
        Vector3 center = shopPosition, best = Vector3.zero; float bestScore = float.MaxValue; bool found = false;
        float[] limits = { .7f, 1.6f };
        for (int li = 0; li < limits.Length && !found; li++)
        {
            for (int ring = 0; ring < 5; ring++)
            {
                float dist = 16f + ring * 4f;
                for (int a = 0; a < 18; a++)
                {
                    float ang = a * 20f * Mathf.Deg2Rad;
                    float x = center.x + Mathf.Cos(ang) * dist, z = center.z + Mathf.Sin(ang) * dist;
                    string reason;
                    if (!CanPlaceDecor(def, x, z, out reason)) continue;
                    float h = Ground(x, z);
                    float slope = Mathf.Max(Mathf.Abs(Ground(x + 2f, z) - h), Mathf.Abs(Ground(x, z + 2f) - h));
                    if (slope > limits[li]) continue;
                    bool road = PetTownRoadPath.Overlaps(new Rect(x - 6f, z - 6f, 12f, 12f));
                    float score = (road ? 0f : 100f) + slope * 20f + ring;
                    if (score < bestScore) { bestScore = score; best = new Vector3(x, h, z); found = true; }
                }
            }
        }
        if (!found) return;
        var rec = new PetTownDecorRecord { id = DeliveryGateId, x = best.x, z = best.z, yaw = DecorYaw(def, best) };
        decorSave.items.Add(rec); BuildDecorObject(def, rec); SaveDecor();
        Message("배달 입구를 마을에 만들었어요. 주문한 펫과 물품이 여기로 도착해요. (마을 꾸미기 > 배달 입구에서 옮길 수 있어요)");
    }

    public void PetDeliveryTick()
    {
        if (Time.unscaledTime < nextCrateCheck) return;
        nextCrateCheck = Time.unscaledTime + 1f;
        if (loadingTown || expansionPending || !shop || cam == null) return;
        EnsureDeliveryGate();
        if (progression == null) return;
        var ops = Progression.Operations(day);
        var arrived = ops.deliveries.FindAll(v => v.Arrived);
        var gate = FindDeliveryGate();
        Vector3 gp = gate ? gate.transform.position : Vector3.zero;
        if (petCrates.Count > 0 && (gp - crateGatePos).sqrMagnitude > .01f) { foreach (var c in petCrates.Values) if (c) Destroy(c); petCrates.Clear(); }
        crateGatePos = gp;
        var stale = new List<string>();
        foreach (var pair in petCrates)
            if (!pair.Value || !arrived.Exists(v => v.id == pair.Key)) { if (pair.Value) Destroy(pair.Value); stale.Add(pair.Key); }
        foreach (var id in stale) petCrates.Remove(id);
        for (int i = 0; i < arrived.Count && petCrates.Count < 12; i++)
        {
            if (petCrates.ContainsKey(arrived[i].id)) continue;
            petCrates[arrived[i].id] = BuildPetCrate(arrived[i], petCrates.Count);
            Message((gate ? "배달 입구" : "펫샵 앞") + "에 상자가 도착했어요! 상자를 눌러 열어 보세요.");
        }
    }

    GameObject BuildPetCrate(PetTownDeliveryRecord rec, int index)
    {
        Vector3 pos;
        var gate = FindDeliveryGate();
        if (gate)
        {
            Vector3 g = gate.transform.position, dir = shopPosition - g; dir.y = 0;
            dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            pos = g + dir * (2.8f + (index / 3) * 1.5f) + right * (((index % 3) - 1) * 1.5f);
        }
        else
        {
            Vector3 basePos = EntrancePoint(shopPosition);
            pos = new Vector3(basePos.x + 1.6f + (index % 3) * 1.5f, 0, basePos.z - 1.0f - (index / 3) * 1.5f);
        }
        float x = pos.x, z = pos.z;
        var root = new GameObject("PetCrate_REPLACEABLE");
        root.transform.position = new Vector3(x, Ground(x, z), z);
        var tag = root.AddComponent<PetTownCrateTag>(); tag.deliveryId = rec.id;
        var lidMat = new Material(wood) { name = "CrateLid" }; lidMat.color = new Color(.62f, .43f, .24f);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "Body";
        body.transform.SetParent(root.transform, false); body.transform.localPosition = new Vector3(0, .36f, 0); body.transform.localScale = new Vector3(1.1f, .72f, .8f);
        body.GetComponent<Renderer>().sharedMaterial = wood;
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube); lid.name = "Lid";
        lid.transform.SetParent(root.transform, false); lid.transform.localPosition = new Vector3(0, .78f, 0); lid.transform.localScale = new Vector3(1.2f, .12f, .9f);
        lid.GetComponent<Renderer>().sharedMaterial = lidMat; Destroy(lid.GetComponent<Collider>());
        for (int s = -1; s <= 1; s += 2)
        {
            var slat = GameObject.CreatePrimitive(PrimitiveType.Cube); slat.name = "Slat";
            slat.transform.SetParent(root.transform, false); slat.transform.localPosition = new Vector3(s * .3f, .38f, -.41f); slat.transform.localScale = new Vector3(.12f, .74f, .04f);
            slat.GetComponent<Renderer>().sharedMaterial = lidMat; Destroy(slat.GetComponent<Collider>());
        }
        AddDecorLabel(root.transform, rec.IsPet ? "새 가족 도착!" : "택배 도착!", 1.9f);
        MarkNavMeshIgnore(root);
        return root;
    }

    bool TryTouchPetCrate(Vector2 screen)
    {
        if (petCrates.Count == 0 || cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screen);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return false;
        var tag = hit.collider.GetComponentInParent<PetTownCrateTag>();
        if (tag == null) return false;
        OpenPetCrate(tag.deliveryId);
        return true;
    }

    void OpenPetCrate(string id)
    {
        if (!OperationsReady()) return;
        var rec = Operations.deliveries.Find(x => x.id == id);
        if (rec == null || !rec.Arrived) { Message("아직 배송 중이거나 이미 열린 상자예요."); return; }
        GameObject crate;
        if (!rec.IsPet)
        {
            if (Operations.Collect(id, Progression)) { progressionDirty = true; SaveProgressionSilent(); if (petCrates.TryGetValue(id, out crate)) { if (crate) Destroy(crate); petCrates.Remove(id); } Message("배송 물품을 받았어요."); }
            return;
        }
        PetTownPetCareRecord pet;
        if (Operations.CollectPet(id, Progression, out pet))
        {
            progressionDirty = true; SyncProgressionStock(); SaveProgressionSilent();
            if (petCrates.TryGetValue(id, out crate)) { if (crate) Destroy(crate); petCrates.Remove(id); }
            Message(pet.nickname + " (" + (pet.sex == 1 ? "수컷" : "암컷") + ")이 상자에서 나왔어요! 돌보며 키워 주세요.");
        }
    }
}
