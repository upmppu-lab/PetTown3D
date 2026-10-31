using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Town decorating: plant trees and flowers, place decorations and small shops/facilities.
// Everything placed raises nearby residents' happiness (instantly and every new day).
public partial class PetTownGame
{
    public const int DecorEntryKey = 9000;
    const string DecorSaveKey = "PetTown3D_Decor_v1";
    // -1 = nothing, -2 = removal mode, >=100 = definition id being placed
    int decorSelected = -1;
    bool decorLoaded;
    PetTownDecorSave decorSave = new PetTownDecorSave();
    readonly List<PetTownDecorTag> decorTags = new List<PetTownDecorTag>();
    GameObject decorMarker;
    Material decorMarkOk, decorMarkBad;

    // ---- save / load -------------------------------------------------------------------------
    void SaveDecor()
    {
        PlayerPrefs.SetString(DecorSaveKey, JsonUtility.ToJson(decorSave));
        PlayerPrefs.Save();
    }

    void DecorEnsureLoaded()
    {
        if (decorLoaded || loadingTown || !cam || !wood) return;
        decorLoaded = true;
        string json = PlayerPrefs.GetString(DecorSaveKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, decorSave); } catch { decorSave = new PetTownDecorSave(); }
        }
        if (decorSave.items == null) decorSave.items = new List<PetTownDecorRecord>();
        var valid = new List<PetTownDecorRecord>();
        foreach (var rec in decorSave.items)
        {
            var def = PetTownDecorDefs.Get(rec.id);
            if (def == null) continue;
            valid.Add(rec);
            BuildDecorObject(def, rec);
        }
        decorSave.items = valid;
    }

    // ---- model -------------------------------------------------------------------------------
    static void StripPhysics(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(r);
    }

    static bool RendererBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(false))
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return !first;
    }

    // Scales the model to the wanted height and stands it on the ground, centred under the root.
    static void FitDecor(GameObject inst, Transform root, float height)
    {
        Bounds b;
        if (!RendererBounds(inst, out b)) return;
        if (b.size.y > .001f)
        {
            float s = height / b.size.y;
            inst.transform.localScale *= s;
            RendererBounds(inst, out b);
        }
        inst.transform.position += new Vector3(root.position.x - b.center.x, root.position.y - b.min.y, root.position.z - b.center.z);
    }

    GameObject BuildDecorObject(PetTownDecorDef def, PetTownDecorRecord rec)
    {
        var root = new GameObject("Decor_" + def.name);
        root.transform.position = new Vector3(rec.x, Ground(rec.x, rec.z), rec.z);
        root.transform.rotation = Quaternion.Euler(0, rec.yaw, 0);
        GameObject prefab = string.IsNullOrEmpty(def.res) ? null : Resources.Load<GameObject>("Decor/" + def.res);
        float topY;
        if (def.avenue) topY = BuildAvenue(root.transform, def, rec);
        else if (prefab == null) topY = BuildPlaceholder(root.transform, def);
        else
        {
            int n = Mathf.Max(1, def.cluster);
            var rnd = new System.Random(Mathf.RoundToInt(rec.x * 31f + rec.z * 17f) + def.id);
            for (int i = 0; i < n; i++)
            {
                var inst = Object.Instantiate(prefab, root.transform);
                inst.name = def.res + "_" + i;
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.Euler(0, n > 1 ? (float)rnd.NextDouble() * 360f : 0f, 0) * prefab.transform.rotation;
                StripPhysics(inst);
                float h = def.height * (n > 1 ? .75f + (float)rnd.NextDouble() * .5f : 1f);
                FitDecor(inst, root.transform, h);
                if (n > 1 && i > 0)
                {
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = Mathf.Sqrt((float)rnd.NextDouble()) * def.radius;
                    var off = new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                    inst.transform.position += off;
                    inst.transform.position = new Vector3(inst.transform.position.x, Ground(inst.transform.position.x, inst.transform.position.z) + (inst.transform.position.y - root.transform.position.y), inst.transform.position.z);
                }
            }
            topY = def.height;
        }
        MarkNavMeshIgnore(root);
        if (def.building || def.id == DeliveryGateId) AddDecorLabel(root.transform, def.name + (prefab == null ? "\n<size=55%>(임시 모델)</size>" : ""), topY + 1.6f);
        var tag = root.AddComponent<PetTownDecorTag>();
        tag.defId = def.id; tag.record = rec;
        decorTags.Add(tag);
        ApplyDecorLevelLabel(tag);
        return root;
    }

    // A simple stand-in building, used until a real model exists in Resources/Decor.
    float BuildPlaceholder(Transform root, PetTownDecorDef def)
    {
        if (def.open) return BuildOpenLot(root, def);
        float w = def.radius * 1.5f, d = def.radius * 1.2f, h = def.height * .62f;
        var body = new Material(wall) { name = "PlaceholderBody" }; body.color = def.color;
        var roofM = new Material(roofMat) { name = "PlaceholderRoof" };
        var dark = new Material(wall) { name = "PlaceholderDoor" }; dark.color = new Color(.35f, .22f, .15f);
        var glass = new Material(wall) { name = "PlaceholderGlass" }; glass.color = new Color(.55f, .8f, .92f);
        SignPart(PrimitiveType.Cube, "Body", root, new Vector3(0, h * .5f, 0), new Vector3(w, h, d), body);
        SignPart(PrimitiveType.Cube, "Roof", root, new Vector3(0, h + .3f, 0), new Vector3(w + 1f, .6f, d + 1f), roofM);
        SignPart(PrimitiveType.Cube, "RoofTop", root, new Vector3(0, h + .9f, 0), new Vector3(w * .6f, .6f, d * .6f), roofM);
        SignPart(PrimitiveType.Cube, "Door", root, new Vector3(0, 1.1f, d * .5f + .02f), new Vector3(1.6f, 2.2f, .12f), dark);
        for (int i = -1; i <= 1; i += 2)
            SignPart(PrimitiveType.Cube, "Window" + i, root, new Vector3(i * w * .3f, h * .6f, d * .5f + .02f), new Vector3(1.5f, 1.3f, .1f), glass);
        SignPart(PrimitiveType.Cylinder, "FlagPole", root, new Vector3(w * .5f - .3f, h + 2f, 0), new Vector3(.12f, 2f, .12f), dark);
        return h + 3.2f;
    }

    float BuildOpenLot(Transform root, PetTownDecorDef def)
    {
        float r = def.radius;
        var ground = new Material(wall) { name = "OpenLotGround" }; ground.color = def.color;
        var dark = new Material(wall) { name = "OpenLotPost" }; dark.color = new Color(.35f, .22f, .15f);
        var roofM = new Material(roofMat) { name = "OpenLotRoof" };
        SignPart(PrimitiveType.Cube, "Platform", root, new Vector3(0, .08f, 0), new Vector3(r * 1.7f, .16f, r * 1.7f), ground);
        if (def.res == "Lookout")
        {
            SignPart(PrimitiveType.Cylinder, "Tower", root, new Vector3(0, def.height * .4f, 0), new Vector3(1.2f, def.height * .4f, 1.2f), dark);
            SignPart(PrimitiveType.Cube, "Deck", root, new Vector3(0, def.height * .82f, 0), new Vector3(3.6f, .4f, 3.6f), roofM);
            SignPart(PrimitiveType.Cube, "Cap", root, new Vector3(0, def.height * .82f + 1.6f, 0), new Vector3(3f, .3f, 3f), roofM);
            return def.height + 1.5f;
        }
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                SignPart(PrimitiveType.Cube, "Post" + sx + sz, root, new Vector3(sx * r * .7f, 1.2f, sz * r * .7f), new Vector3(.25f, 2.4f, .25f), dark);
        SignPart(PrimitiveType.Cube, "Canopy", root, new Vector3(0, 2.6f, 0), new Vector3(r * 1.8f, .25f, r * 1.8f), roofM);
        return 3.6f;
    }

    void AddDecorLabel(Transform root, string label, float y)
    {
        float top = y;
        Bounds lb;
        if (RendererBounds(root.gameObject, out lb)) top = lb.max.y - root.position.y + .7f;
        PetTownPillLabel.Create(root, label, new Vector3(0, top, 0), 3.2f);
    }

    // ---- placement ---------------------------------------------------------------------------
    bool ScreenToTerrain(Vector2 screen, out Vector3 p)
    {
        Ray ray = cam.ScreenPointToRay(screen);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f, 1 << TerrainLayer, QueryTriggerInteraction.Ignore)) { p = hit.point; p.y = Ground(p.x, p.z); return true; }
        var plane = new Plane(Vector3.up, Vector3.zero);
        float dist;
        if (plane.Raycast(ray, out dist)) { p = ray.GetPoint(dist); p.y = Ground(p.x, p.z); return true; }
        p = Vector3.zero; return false;
    }

    bool CanPlaceDecor(PetTownDecorDef def, float x, float z, out string reason)
    {
        reason = null;
        if (Mathf.Abs(x) > 104f || Mathf.Abs(z) > 104f) { reason = "지도 밖에는 놓을 수 없어요."; return false; }
        float r = Mathf.Max(.5f, def.radius);
        for (int i = 0; i < 9; i++)
        {
            float ang = i * Mathf.PI * .25f, rr = i == 8 ? 0f : r;
            if (IsWater(x + Mathf.Cos(ang) * rr, z + Mathf.Sin(ang) * rr)) { reason = "물 위에는 놓을 수 없어요."; return false; }
        }
        if (InsideAnyBuildingFootprint(x, z, def.building ? r : r * .5f + .3f)) { reason = "건물과 너무 가까워요."; return false; }
        if (PetTownRoadPath.Overlaps(new Rect(x - Mathf.Max(r, .6f), z - Mathf.Max(r, .6f), Mathf.Max(r, .6f) * 2f, Mathf.Max(r, .6f) * 2f))) { reason = "길 위에는 놓을 수 없어요."; return false; }
        foreach (var tag in decorTags)
        {
            if (!tag) continue;
            var other = PetTownDecorDefs.Get(tag.defId);
            if (other == null) continue;
            if ((def.id == DeliveryGateId || def.home) && tag.defId == def.id) continue;
            float need = (r + Mathf.Max(.5f, other.radius)) * (def.building || other.building ? .95f : .7f);
            float dx = tag.record.x - x, dz = tag.record.z - z;
            if (dx * dx + dz * dz < need * need) { reason = "다른 장식물과 겹쳐요."; return false; }
        }
        return true;
    }

    float DecorYaw(PetTownDecorDef def, Vector3 p)
    {
        if (def.avenue) { Vector3 av = shopPosition - p; av.y = 0; return av.sqrMagnitude > 1f ? Mathf.Round(Mathf.Atan2(av.x, av.z) * Mathf.Rad2Deg / 90f) * 90f : 0f; }
        if (def.building || def.id == DeliveryGateId)
        {
            Vector3 dir = shopPosition - p; dir.y = 0;
            if (dir.sqrMagnitude > 1f) return Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 90f) * 90f;
            return 0f;
        }
        return (Mathf.Abs(Mathf.RoundToInt(p.x * 13f + p.z * 7f)) % 12) * 30f;
    }

    void TryPlaceDecor(Vector2 screen)
    {
        Vector3 p;
        if (!ScreenToTerrain(screen, out p)) return;
        if (decorSelected == -2) { TryRemoveDecor(screen); return; }
        var def = PetTownDecorDefs.Get(decorSelected);
        if (def == null) { ClearDecorSelection(); return; }
        string reason;
        if (!CanPlaceDecor(def, p.x, p.z, out reason)) { Message(reason); return; }
        if (def.unlock > 0 && EnsureResidents().Population < def.unlock) { Message(def.name + "은(는) 주민이 " + def.unlock + "명 이상일 때 지을 수 있어요. (지금 " + EnsureResidents().Population + "명)"); return; }
        if (money < def.price) { Message("코인이 부족해요. 필요한 금액: " + def.price.ToString("N0") + " 골드"); return; }
        money -= def.price;
        if (def.id == DeliveryGateId) RemoveDeliveryGate();
        if (def.home) RemoveHomeTags();
        var rec = new PetTownDecorRecord { id = def.id, x = p.x, z = p.z, yaw = DecorYaw(def, p) };
        decorSave.items.Add(rec);
        BuildDecorObject(def, rec);
        SaveDecor();
        int boosted = DecorInstantBoost(def, p);
        SaveGame();
        Message(def.name + " 설치 완료! " + (boosted > 0 ? "주민 " + boosted + "명이 기뻐해요." : "") + " (계속 탭하면 또 놓아요)");
    }

    void ClearDecorSelection()
    {
        decorSelected = -1;
        if (decorMarker) decorMarker.SetActive(false);
    }

    void UpdateDecorMarker()
    {
        if (decorSelected < 100 || Input.touchCount > 0 || IsPointerOverUI()) { if (decorMarker) decorMarker.SetActive(false); return; }
        var def = PetTownDecorDefs.Get(decorSelected);
        Vector3 p;
        if (def == null || !ScreenToTerrain(Input.mousePosition, out p)) { if (decorMarker) decorMarker.SetActive(false); return; }
        if (decorMarker == null)
        {
            decorMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            decorMarker.name = "PetTown_DecorMarker";
            Object.Destroy(decorMarker.GetComponent<Collider>());
            decorMarkOk = Mat(new Color(.3f, .85f, .4f)); decorMarkBad = Mat(new Color(.9f, .3f, .3f));
            MarkNavMeshIgnore(decorMarker);
        }
        string reason;
        bool ok = CanPlaceDecor(def, p.x, p.z, out reason);
        decorMarker.SetActive(true);
        float diameter = Mathf.Max(1.2f, def.radius * 2f);
        decorMarker.transform.position = p + Vector3.up * .05f;
        decorMarker.transform.localScale = new Vector3(diameter, .04f, diameter);
        decorMarker.GetComponent<Renderer>().sharedMaterial = ok ? decorMarkOk : decorMarkBad;
    }

    // ---- removal -----------------------------------------------------------------------------
    void TryRemoveDecor(Vector2 screen)
    {
        float limit = Mathf.Max(60f, Screen.height * .07f);
        PetTownDecorTag best = null; float bestD = limit;
        foreach (var tag in decorTags)
        {
            if (!tag) continue;
            Vector3 sp = cam.WorldToScreenPoint(tag.Center);
            if (sp.z <= 0) continue;
            float d = Vector2.Distance(new Vector2(sp.x, sp.y), screen);
            if (d < bestD) { bestD = d; best = tag; }
        }
        if (best == null) { Message("철거할 장식물을 눌러 주세요."); return; }
        var def = PetTownDecorDefs.Get(best.defId);
        if (def == null) return;
        if (def.id == DeliveryGateId) { Message("배달 입구는 철거할 수 없어요. 다른 곳에 다시 놓으면 옮겨져요."); return; }
        int refund = def.price / 2;
        var tag2 = best;
        var entries = new List<PetTownCatalogEntry>
        {
            DecorEntry(0, def.name + " 철거", refund.ToString("N0") + " 골드 환불", "설치 금액의 50%", "철거하기", "이 장식물을 치우고 설치 금액의 절반을 돌려받아요.", 1),
            DecorEntry(1, "취소", "", "", "취소", "아무것도 바뀌지 않아요.", 3)
        };
        catalogWindow.Show("장식물을 철거할까요?", entries, e =>
        {
            if (e.Key != 0 || !tag2) return;
            money += refund;
            decorSave.items.Remove(tag2.record);
            decorTags.Remove(tag2);
            Object.Destroy(tag2.gameObject);
            SaveDecor(); SaveGame();
            Message(def.name + "을(를) 철거했어요. (+" + refund.ToString("N0") + " 골드)");
        }, () => { }, () => { });
    }

    // ---- happiness ---------------------------------------------------------------------------
    float DecorScoreAt(Vector3 point)
    {
        float score = 0f;
        foreach (var tag in decorTags)
        {
            if (!tag) continue;
            var def = PetTownDecorDefs.Get(tag.defId);
            if (def == null) continue;
            float R = def.building ? 70f : 45f;
            float dx = tag.record.x - point.x, dz = tag.record.z - point.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < R) score += def.happy * DecorLvMul(tag.record) * (1f - d / R);
        }
        return score;
    }

    static float DecorBonus(float score) { return 10f * (1f - Mathf.Exp(-score / 6f)); }

    Vector3 ResidentReferencePoint(PetTownResidentRecord person, List<PetTownResidentHome> homes)
    {
        var home = homes != null ? homes.Find(h => h.id == person.homeId) : null;
        return home != null ? home.entrance : shopPosition;
    }

    int DecorInstantBoost(PetTownDecorDef def, Vector3 p)
    {
        var state = EnsureResidents();
        var homes = ResidentHomes();
        int count = 0;
        foreach (var person in state.people)
        {
            Vector3 refPoint = ResidentReferencePoint(person, homes);
            float R = def.building ? 70f : 45f;
            float d = Vector2.Distance(new Vector2(refPoint.x, refPoint.z), new Vector2(p.x, p.z));
            if (d >= R) continue;
            person.happiness = Mathf.Min(100f, person.happiness + def.happy * (1f - d / R) * 1.5f);
            count++;
        }
        if (count > 0) progressionDirty = true;
        return count;
    }

    // Called at the start of every day.
    void DecorNewDay()
    {
        if (decorTags.Count == 0) return;
        int income = 0;
        foreach (var tag in decorTags)
        {
            if (!tag) continue;
            var def = PetTownDecorDefs.Get(tag.defId);
            if (def != null) income += Mathf.RoundToInt(def.income * DecorLvMul(tag.record));
        }
        if (income > 0) money += income;
        var state = EnsureResidents();
        var homes = ResidentHomes();
        float total = 0f; int n = 0;
        foreach (var person in state.people)
        {
            float bonus = DecorBonus(DecorScoreAt(ResidentReferencePoint(person, homes)));
            if (bonus <= 0f) continue;
            person.happiness = Mathf.Min(100f, person.happiness + bonus);
            total += bonus; n++;
        }
        if (n > 0 || income > 0)
        {
            progressionDirty = true;
            Message("마을 꾸미기 효과로 주민 행복도 평균 +" + (n > 0 ? total / n : 0f).ToString("0.0") + (income > 0 ? " · 상점 수입 " + income.ToString("N0") + " 골드" : ""));
        }
    }

    // ---- building level-up (2D parity: Lv1 -> Lv3, x1.5 / x2 happiness + income) ------------------
    static int DecorLv(PetTownDecorRecord r) { return r == null ? 1 : Mathf.Clamp(r.lv, 1, 5); }
    static int DecorMaxLv(PetTownDecorDef d) { return d != null && d.home ? 5 : 3; }
    static float DecorLvMul(PetTownDecorRecord r) { int lv = DecorLv(r); return 1f + (lv - 1) * .5f; }
    static int DecorLevelCost(PetTownDecorDef d, int lv) { return Mathf.RoundToInt(d.price * lv * 1.2f); }
    static int DecorLevelNeed(PetTownDecorDef d, int nextLv) { if (d.home) return nextLv == 2 ? 6 : nextLv == 3 ? 12 : nextLv == 4 ? 20 : 30; return d.unlock + (nextLv >= 3 ? 20 : 8); }
    void ApplyDecorLevelLabel(PetTownDecorTag tag)
    {
        if (!tag || DecorLv(tag.record) < 2) return;
        var def = PetTownDecorDefs.Get(tag.defId); if (def == null) return;
        var pillLabel = tag.GetComponentInChildren<PetTownPillLabel>(); if (pillLabel) pillLabel.SetText(def.name + "\n<size=60%>★ Lv." + DecorLv(tag.record) + "</size>");
    }
    void OpenDecorLevelUps()
    {
        DecorEnsureLoaded();
        var tags = new List<PetTownDecorTag>(); var entries = new List<PetTownCatalogEntry>();
        int pop = EnsureResidents().Population;
        foreach (var tag in decorTags)
        {
            if (!tag) continue; var def = PetTownDecorDefs.Get(tag.defId);
            if (def == null || !def.building || def.id == DeliveryGateId) continue;
            int lv = DecorLv(tag.record); int key = tags.Count; tags.Add(tag);
            string price, action, desc;
            if (lv >= DecorMaxLv(def)) { price = "최고 레벨"; action = "확인"; desc = def.name + "은(는) 이미 최고 레벨이에요. 행복·수입 효과가 2배예요."; }
            else
            {
                int need = DecorLevelNeed(def, lv + 1), cost = DecorLevelCost(def, lv);
                price = cost.ToString("N0") + " 골드 · 주민 " + need + "명";
                action = pop >= need ? "레벨업" : "조건 확인";
                desc = "Lv." + lv + " → Lv." + (lv + 1) + "\n행복·수입 효과 x" + (1f + lv * .5f).ToString("0.#") + "\n필요: 주민 " + need + "명 이상 (지금 " + pop + "명), " + cost.ToString("N0") + " 골드";
            }
            entries.Add(DecorEntry(key, def.name + " Lv." + lv, price, def.category, action, desc, 3));
        }
        if (entries.Count == 0) { Message("레벨업할 상점·시설이 아직 없어요. 꾸미기에서 상점을 먼저 지어 주세요."); return; }
        catalogWindow.Show("상점·시설 레벨업", entries, e =>
        {
            if (e.Key < 0 || e.Key >= tags.Count || !tags[e.Key]) return;
            var tag = tags[e.Key]; var def = PetTownDecorDefs.Get(tag.defId); int lv = DecorLv(tag.record);
            if (lv >= DecorMaxLv(def)) { Message("이미 최고 레벨이에요."); return; }
            int need = DecorLevelNeed(def, lv + 1), cost = DecorLevelCost(def, lv);
            if (EnsureResidents().Population < need) { Message(def.name + " 레벨업에는 주민이 " + need + "명 이상 필요해요."); OpenDecorLevelUps(); return; }
            if (money < cost) { Message("골드가 부족해요. 필요한 금액: " + cost.ToString("N0") + " 골드"); OpenDecorLevelUps(); return; }
            money -= cost; tag.record.lv = lv + 1; ApplyDecorLevelLabel(tag); SaveDecor(); SaveGame(); progressionDirty = true;
            Message(def.name + " Lv." + (lv + 1) + "! 행복·수입 효과가 커졌어요.");
            OpenDecorLevelUps();
        }, () => { }, () => { });
    }

    // ---- UI ----------------------------------------------------------------------------------
    static PetTownCatalogEntry DecorEntry(int key, string name, string price, string info, string action, string description, int icon)
    {
        return new PetTownCatalogEntry { Key = key, Name = name, Category = "꾸미기", TextOnlyDetail = true, IconKey = icon, PriceLabel = price, FootprintLabel = info, ActionLabel = action, Description = description };
    }

    void OpenDecorChooser()
    {
        if (catalogWindow == null || !catalogWindow.Ready) return;
        DecorEnsureLoaded();
        var entries = new List<PetTownCatalogEntry>();
        int trees = 0, flowers = 0, decor = 0, fac = 0;
        foreach (var tag in decorTags)
        {
            if (!tag) continue;
            var d = PetTownDecorDefs.Get(tag.defId);
            if (d == null) continue;
            if (d.category == PetTownDecorDefs.Trees) trees++; else if (d.category == PetTownDecorDefs.Flowers) flowers++; else if (d.category == PetTownDecorDefs.Decor) decor++; else fac++;
        }
        float bonus = DecorBonus(DecorScoreAt(shopPosition));
        entries.Add(DecorEntry(-1, "마을 꾸미기 현황", "펫샵 기준 하루 행복도 +" + bonus.ToString("0.0"),
            "나무 " + trees + " · 꽃 " + flowers + " · 장식 " + decor + " · 시설 " + fac,
            "확인", "나무·꽃·장식·시설을 많이 놓을수록 주변 주민의 행복도가 올라가요. 가까울수록 효과가 커요.", 3));
        entries.Add(DecorEntry(-2, "철거하기", "50% 환불", "탭한 장식물을 치워요", "철거 모드", "철거 모드에서 치울 장식물을 탭하세요.", 1));
        entries.Add(DecorEntry(-4, "상점·시설 레벨업", "Lv.1 → 3", "행복·수입 효과 x1.5 / x2", "레벨업 보기", "지은 상점과 시설을 키워요. 주민이 많을수록 더 높은 레벨로 올릴 수 있어요.", 3));
        entries.Add(DecorEntry(-3, "꾸미기 끝내기", "", "", "끝내기", "배치 모드를 끝내고 마을로 돌아가요.", 3));
        foreach (var def in PetTownDecorDefs.All)
        {
            var tex = Resources.Load<Texture2D>("DecorThumbs/" + def.res);
            var entry = new PetTownCatalogEntry
            {
                Key = def.id, Name = def.name, Category = def.category, Price = def.price,
                Description = PetTownDecorDefs.Describe(def) + "\n탭한 자리에 설치돼요. 초록색 원은 설치 가능, 빨간색 원은 불가능이에요.",
                Footprint = Vector2.zero
            };
            if (tex != null) entry.Thumbnail = tex; else { entry.TextOnlyDetail = true; entry.IconKey = 2; }
            entries.Add(entry);
        }
        catalogWindow.Show("마을 꾸미기", entries, e =>
        {
            if (e.Key == -1) { OpenDecorChooser(); return; }
            if (e.Key == -4) { OpenDecorLevelUps(); return; }
            if (e.Key == -3) { ClearDecorSelection(); SetMode(Mode.Play); Message("꾸미기를 마쳤어요."); return; }
            SetMode(Mode.Play);
            if (e.Key == -2) { decorSelected = -2; Message("철거 모드: 치울 장식물을 탭하세요."); return; }
            var def = PetTownDecorDefs.Get(e.Key);
            if (def == null) return;
            decorSelected = def.id;
            Message(def.name + "을(를) 놓을 자리를 탭하세요. (" + def.price.ToString("N0") + " 골드, 하단 메뉴를 누르면 끝나요)");
        }, () => SetMode(Mode.Play), () => { });
    }

    public void DecorTick()
    {
        DecorEnsureLoaded();
        if (decorSelected != -1 && mode != Mode.Play) ClearDecorSelection();
        UpdateDecorMarker();
        DecorVisitTick();
    }
}
