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
    public int unlock;        // residents needed before it can be placed (0 = always)
    public bool avenue;       // a row of seasonal street trees with lamps
    public bool home;         // the player's own house (only one, family pets rest there)
    public bool open;         // open lot: residents stand in front instead of walking inside
    public string visitLine;  // speech line after a visit
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

    static void Civic(int id, string name, string res, float height, float radius, int price, float happy, int income, int unlock, bool open, string desc, string line, Color color)
    {
        Add(id, name, Facilities, res, height, radius, price, happy, income, 1, true, desc, color);
        var d = byId[id]; d.unlock = unlock; d.open = open; d.visitLine = line;
    }

    public static string Describe(PetTownDecorDef d)
    {
        string s = d.desc != null ? d.desc + "\n" : "";
        s += "주변 주민의 행복도가 올라가요 (꾸미기 점수 +" + d.happy.ToString("0.0") + ").";
        if (d.income > 0) s += "\n하루마다 " + d.income + " 골드를 벌어요.";
        if (d.building) s += "\n펫샵을 바라보게 놓이고, 이름판이 붙어요.";
        if (d.unlock > 0) s += "\n해금: 주민 " + d.unlock + "명 이상";
        return s;
    }

    static void Build()
    {
        if (all != null) return;
        all = new List<PetTownDecorDef>();
        string[] treeRes = { "TreeA", "TreeB", "TreeC", "TreeD", "TreeE", "TreeF", "TreeG", "TreeH" };
        for (int i = 0; i < treeRes.Length; i++)
            Add(100 + i, "나무 " + (i + 1), Trees, treeRes[i], 10f, 1.5f, 150, .8f, 0, 1, false, "마을에 그늘을 만들어 주는 큰 나무예요.");
        Add(418, "가로수길", Trees, "ParkTree1", 6f, 12f, 1200, 2.5f, 0, 1, false, "나무 여섯 그루와 가로등이 줄지어 선 길이에요. 계절마다 나무 색이 바뀌어요 (봄 분홍·여름 초록·가을 주황·겨울 흰빛).");
        byId[418].avenue = true;
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
        // 2D parity: more shops / public buildings. unlock = residents needed (scaled for this 3D town). open = open lot (no building to walk into).
        Civic(417, "가족 펫 쉼터", "MyHome", 6.5f, 6f, 3000, 3f, 0, 0, false, "보호(분양 안 함)로 설정한 가족 펫들이 쉬는 작은 집이에요. 한 채만 지을 수 있고 다시 놓으면 옮겨져요.\n가족 펫은 매일 아침 여기서 쉬며 회복해요. 레벨 1~5 (가족 펫 정원 2·4·6·8·10마리)", "가족 펫들과 편히 쉬었어!", new Color(.98f, .86f, .78f));
        byId[417].home = true;
        Civic(405, "빵집", "Bakery", 5f, 5f, 2200, 2.5f, 130, 6, false, "갓 구운 빵 냄새가 마을에 퍼져요.", "갓 구운 빵이 맛있어!", new Color(.95f, .78f, .5f));
        Civic(406, "꽃집", "FlowerShop", 4.5f, 4.5f, 1800, 3f, 100, 8, false, "계절 꽃을 파는 꽃집이에요. 마을이 더 화사해져요.", "꽃 향기에 기분이 좋아졌어.", new Color(.98f, .8f, .85f));
        Civic(407, "동네 의원", "TownClinic", 5.5f, 5.5f, 3500, 3.5f, 0, 10, false, "주민이 건강 검진을 받는 작은 의원이에요.", "건강 검진을 받았어. 안심이야.", new Color(.85f, .93f, .96f));
        Civic(408, "펫 운동장", "PetPlayground", 3f, 6f, 2600, 4f, 0, 12, true, "주민이 펫과 함께 뛰어노는 열린 운동장이에요.", "우리 펫이 신나게 뛰어놀았어!", new Color(.62f, .82f, .5f));
        Civic(409, "경찰서", "PoliceStation", 6f, 6f, 4500, 3f, 0, 15, false, "마을을 지켜 주는 경찰서예요.", "든든해서 마음이 놓여.", new Color(.55f, .65f, .85f));
        Civic(410, "소방서", "FireStation", 7f, 6.5f, 4800, 3f, 0, 15, false, "불이 나면 달려가는 소방서예요.", "소방관 아저씨가 멋있었어!", new Color(.85f, .35f, .3f));
        Civic(411, "도서관", "Library", 7f, 7f, 4200, 4f, 0, 18, false, "조용히 책을 읽는 마을 도서관이에요.", "좋은 책을 빌렸어!", new Color(.78f, .62f, .45f));
        Civic(412, "벼룩시장", "FleaMarket", 3.5f, 6f, 2000, 2.5f, 110, 18, true, "주민들이 물건을 사고파는 열린 시장이에요.", "싸게 득템했어!", new Color(.9f, .8f, .6f));
        Civic(413, "펫 훈련학교", "PetAcademy", 6.5f, 6.5f, 5200, 4f, 0, 24, false, "펫에게 예절을 가르치는 훈련학교예요.", "우리 펫이 앉아를 배웠어!", new Color(.7f, .85f, .7f));
        Civic(414, "펫 호텔", "PetHotel", 8f, 7f, 6500, 5f, 220, 30, false, "여행 중인 주민의 펫이 편히 쉬는 호텔이에요.", "펫 호텔이 정말 아늑해!", new Color(.9f, .82f, .95f));
        Civic(415, "전망대", "Lookout", 12f, 3.5f, 3800, 5f, 0, 36, true, "마을이 한눈에 보이는 전망대예요.", "마을이 한눈에 보여!", new Color(.75f, .72f, .68f));
        Civic(416, "웨딩 채플", "WeddingChapel", 9f, 5.5f, 7000, 6f, 0, 45, false, "행복한 결혼식이 열리는 하얀 채플이에요.", "채플이 너무 로맨틱해!", new Color(.97f, .96f, .92f));
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
public sealed class PetTownDecorRecord { public int id; public float x, z, yaw; public int lv; }

[Serializable]
public sealed class PetTownDecorSave { public List<PetTownDecorRecord> items = new List<PetTownDecorRecord>(); }
