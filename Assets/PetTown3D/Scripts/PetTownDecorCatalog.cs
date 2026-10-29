using System;
using System.Collections.Generic;
using UnityEngine;

// Town decoration catalogue: trees, flowers, decorations and small shops/facilities.
// Models live in Resources/Decor/<res>.prefab. If a prefab is missing the game draws a simple
// placeholder building instead, so a better asset can be dropped in later by just adding
// Resources/Decor/<res>.prefab (for example Resources/Decor/School.prefab) - no code change needed.
public sealed class PetTownDecorDef
{
    public int id;
    public string name, category, res, desc;
    public float height, radius, happy;
    public int price, income, cluster = 1;
    public bool building;      // shop / facility / landmark: gets a name board and faces the pet shop
    public Color color = new Color(.95f, .88f, .72f);
}

public static class PetTownDecorDefs
{
    public const string Trees = "나무", Flowers = "꽃·덤불", Decor = "장식물", Facilities = "상점·시설";
    static List<PetTownDecorDef> all;
    static readonly Dictionary<int, PetTownDecorDef> byId = new Dictionary<int, PetTownDecorDef>();

    public static List<PetTownDecorDef> All { get { Build(); return all; } }
    public static PetTownDecorDef Get(int id) { Build(); PetTownDecorDef d; return byId.TryGetValue(id, out d) ? d : null; }

    static void Add(int id, string name, string cat, string res, float height, float radius, int price, float happy, int income = 0, int cluster = 1, bool building = false, string desc = null, Color? color = null)
    {
        var d = new PetTownDecorDef { id = id, name = name, category = cat, res = res, height = height, radius = radius, price = price, happy = happy, income = income, cluster = cluster, building = building, desc = desc };
        if (color.HasValue) d.color = color.Value;
        all.Add(d); byId[id] = d;
    }

    public static string Describe(PetTownDecorDef d)
    {
        string s = d.desc != null ? d.desc + "\n" : "";
        s += "주변 주민의 행복도가 올라가요 (꾸미기 점수 +" + d.happy.ToString("0.0") + ").";
        if (d.income > 0) s += "\n하루마다 " + d.income + " 골드를 벌어요.";
        if (d.building) s += "\n펫샵을 바라보게 놓이고, 이름판이 붙어요.";
        return s;
    }

    static void Build()
    {
        if (all != null) return;
        all = new List<PetTownDecorDef>();
        string[] treeRes = { "TreeA", "TreeB", "TreeC", "TreeD", "TreeE", "TreeF", "TreeG", "TreeH" };
        for (int i = 0; i < treeRes.Length; i++)
            Add(100 + i, "나무 " + (i + 1), Trees, treeRes[i], 10f, 1.5f, 150, .8f, 0, 1, false, "마을에 그늘을 만들어 주는 큰 나무예요.");
        Add(108, "공원 나무 1", Trees, "ParkTree1", 6f, 1.1f, 100, .7f);
        Add(109, "공원 나무 2", Trees, "ParkTree2", 6f, 1.1f, 100, .7f);
        Add(110, "단풍나무", Trees, "ParkTree3", 6f, 1.0f, 110, .7f);
        Add(111, "공원 나무 4", Trees, "ParkTree4", 6f, 1.0f, 100, .7f);
        Add(112, "거대한 나무", Trees, "BigTree", 20f, 3f, 800, 2.5f, 0, 1, false, "마을의 상징이 될 만큼 커다란 나무예요.");

        float[] fh = { .9f, .9f, .9f, .9f, .9f, 1.2f, 1.2f, 1.2f, 1.2f, .9f, .9f, .9f, .9f };
        for (int i = 0; i < 13; i++)
            Add(200 + i, "꽃 " + (i + 1), Flowers, "Flower" + (i + 1), fh[i], .9f, 30, .4f, 0, 7, false, "한 번에 꽃 여러 송이가 피어요.");
        Add(213, "꽃 화단", Flowers, "FlowerBed", .8f, 1.8f, 80, .9f, 0, 1, false, "벽돌 화단에 꽃이 가득해요.");
        for (int i = 0; i < 3; i++) Add(214 + i, "관목 " + (i + 1), Flowers, "Shrub" + (i + 1), 1.6f, .9f, 40, .4f);
        for (int i = 0; i < 4; i++) Add(217 + i, "덤불 " + (i + 1), Flowers, "Bush" + (i + 1), 2.6f, 1.4f, 60, .5f);

        Add(300, "분수대", Decor, "Fountain", 3.5f, 2.5f, 700, 3f, 0, 1, false, "마을 광장에 어울리는 분수대예요.");
        Add(301, "작은 분수", Decor, "Fountain2", 2.8f, 2f, 450, 2f);
        Add(302, "벤치", Decor, "Bench", 1f, 1.3f, 60, .5f);
        Add(303, "2인 벤치", Decor, "DoubleBench", 1f, 2f, 90, .7f);
        Add(304, "공원 가로등", Decor, "ParkLamp", 4.5f, .5f, 80, .6f);
        Add(305, "가로등", Decor, "StreetLamp", 4.5f, .4f, 70, .5f);
        Add(306, "쓰레기통", Decor, "TrashBin", 1.1f, .5f, 25, .15f);
        Add(307, "제단", Decor, "Altar", 3.2f, 1.6f, 300, 1.2f);
        Add(308, "아치문", Decor, "Arch", 4.5f, 2.2f, 260, 1.2f);
        Add(309, "돌판", Decor, "StoneSlab", .4f, 1.2f, 25, .1f);
        Add(310, "수레", Decor, "Wagon", 2.2f, 1.8f, 140, .6f);
        Add(311, "표지판", Decor, "SignPost", 2.2f, .4f, 25, .1f);
        Add(312, "기념탑", Decor, "Monument", 14f, 4f, 3000, 6f, 0, 1, true, "마을의 자랑이 되는 커다란 기념탑이에요.");
        Add(313, "시계탑", Decor, "ClockTower", 12f, 3.5f, 2200, 4.5f, 0, 1, true, "마을 사람들이 시간을 맞추는 시계탑이에요.");

        Add(400, "편의점", Facilities, "ShopConvenience", 5f, 6f, 2500, 3f, 150, 1, true, "주민이 간식과 생필품을 사러 와요.");
        Add(401, "사진관", Facilities, "ShopPhoto", 4.5f, 5f, 2000, 2.5f, 120, 1, true, "주민이 추억 사진을 찍으러 와요.");
        Add(402, "유기동물 보관소", Facilities, "ShelterBarn", 7f, 5.5f, 3000, 4f, 0, 1, true, "길 잃은 동물을 보호해 주는 곳이에요. 주민들이 마음 놓고 지내요.");
        Add(404, "배달 입구", Facilities, "Arch", 3.6f, 2.2f, 0, 0f, 0, 1, false, "주문한 펫과 사료·물품이 이 입구 앞에 상자로 도착해요. 한 곳에만 둘 수 있고, 다른 곳에 다시 놓으면 무료로 옮겨져요.");
        Add(403, "학교", Facilities, "School", 7f, 7f, 5000, 6f, 0, 1, true, "아이들이 배우는 학교예요. (임시 모델: Resources/Decor/School.prefab 에셋을 넣으면 자동으로 바뀌어요)", new Color(.96f, .86f, .62f));
    }
}

// Placed decoration instance marker.
public sealed class PetTownDecorTag : MonoBehaviour
{
    public int defId;
    public PetTownDecorRecord record;
    public Vector3 Center { get { return transform.position + Vector3.up * 1.2f; } }
}

[Serializable]
public sealed class PetTownDecorRecord { public int id; public float x, z, yaw; }

[Serializable]
public sealed class PetTownDecorSave { public List<PetTownDecorRecord> items = new List<PetTownDecorRecord>(); }
