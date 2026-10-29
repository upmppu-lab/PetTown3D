using UnityEngine;

// The kinds of road the player can lay. The look is drawn by the PetTown/RoadSurface shader (no imported art).
public static class PetTownRoadTypes
{
    public struct Def
    {
        public string name, description;
        public float width, cell;
        public int cost, unlockLevel, pattern;
        public Color tint, alt;
    }

    static Def D(string name, string description, float width, int cost, int unlock, int pattern, Color tint, Color alt, float cell = .6f)
    {
        return new Def { name = name, description = description, width = width, cost = cost, unlockLevel = unlock, pattern = pattern, tint = tint, alt = alt, cell = cell };
    }

    public static readonly Def[] All =
    {
        D("흙길", "가장 기본적인 흙길입니다.", 2.2f, 20, 1, 0, Color.white, Color.white),
        D("오솔길", "좁고 아담한 산책용 오솔길입니다.", 1.4f, 12, 1, 0, new Color(.95f, .88f, .7f), Color.white),
        D("모래길", "부드러운 모래가 깔린 길입니다.", 2.2f, 24, 2, 1, new Color(.86f, .78f, .55f), Color.white),
        D("자갈길", "자갈이 깔려 걷는 소리가 좋은 길입니다.", 2.0f, 28, 3, 1, new Color(.62f, .6f, .58f), Color.white),
        D("징검돌길", "풀밭 위에 돌을 띄엄띄엄 놓은 길입니다.", 1.8f, 26, 4, 7, new Color(.55f, .5f, .38f), new Color(.7f, .7f, .68f), .9f),
        D("보도블록길", "반듯한 보도블록이 깔린 길입니다.", 2.2f, 40, 5, 2, new Color(.72f, .72f, .74f), new Color(.45f, .45f, .47f), .55f),
        D("벽돌길", "붉은 벽돌을 엇갈려 깐 길입니다.", 2.2f, 48, 6, 3, new Color(.7f, .32f, .24f), new Color(.62f, .58f, .52f), .6f),
        D("나무 산책로", "나무 판자를 이어 붙인 산책로입니다.", 2.0f, 52, 7, 4, new Color(.62f, .42f, .24f), new Color(.3f, .2f, .12f)),
        D("아스팔트 도로", "차선이 그려진 넓은 도로입니다.", 3.4f, 70, 8, 5, new Color(.2f, .21f, .23f), new Color(.9f, .9f, .85f)),
        D("대리석길", "매끈한 흰 대리석이 깔린 길입니다.", 2.6f, 90, 10, 6, new Color(.92f, .92f, .9f), new Color(.7f, .72f, .78f), 1.1f),
        D("큰 가로수길", "넓고 시원한 큰길입니다.", 4.2f, 110, 12, 2, new Color(.78f, .72f, .64f), new Color(.5f, .45f, .4f), .8f),
        D("황금 벽돌길", "반짝이는 황금빛 벽돌길입니다.", 2.4f, 140, 15, 3, new Color(.95f, .75f, .2f), new Color(.6f, .45f, .1f), .55f)
    };

    public static int Count { get { return All.Length; } }
    public static Def Get(int type) { return All[Mathf.Clamp(type, 0, All.Length - 1)]; }
    public static float Width(int type) { return Get(type).width; }
    public static int Cost(int type) { return Get(type).cost; }
    public static bool Paved(int type) { return type >= 5; }

    static float Hash(int x, int y)
    {
        unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }
    }

    // A small preview picture for the road list.
    public static Texture2D Swatch(int type)
    {
        var def = Get(type);
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RoadSwatch" + type, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        Color dirt = new Color(.52f, .4f, .26f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = dirt * def.tint;
                switch (def.pattern)
                {
                    case 1: c = def.tint * (.72f + .42f * Hash(x / 2, y / 2)); break;
                    case 2:
                        { int cx = x / 16, cy = y / 16; bool line = x % 16 < 1 || y % 16 < 1; c = line ? def.alt : def.tint * (.86f + .22f * Hash(cx, cy)); break; }
                    case 3:
                        { int row = y / 10; int xx = x + (row % 2) * 8; bool line = xx % 16 < 1 || y % 10 < 1; c = line ? def.alt : def.tint * (.8f + .3f * Hash(xx / 16, row)); break; }
                    case 4:
                        { int plank = x / 8; bool gap = x % 8 < 1; c = gap ? def.alt : def.tint * (.8f + .3f * Hash(plank, y / 24)); break; }
                    case 5:
                        { c = def.tint * (.9f + .2f * Hash(x, y)); bool dash = Mathf.Abs(x - size / 2) < 1 && (y / 8) % 2 == 0; bool edge = x == 6 || x == size - 7; if (dash || edge) c = def.alt; break; }
                    case 6:
                        { bool line = x % 32 < 1 || y % 32 < 1; float vein = Mathf.Sin((x + y * .7f) * .2f + Hash(x / 32, y / 32) * 6f) * .5f + .5f; c = Color.Lerp(def.tint, def.alt, vein * .22f); if (line) c = def.alt * .8f; break; }
                    case 7:
                        { int cx = x / 22, cy = y / 22; float dx = x - (cx * 22 + 11), dy = y - (cy * 22 + 11); c = dx * dx + dy * dy < 90 ? def.alt * (.85f + .25f * Hash(cx, cy)) : dirt * def.tint; break; }
                }
                c.a = 1;
                pixels[y * size + x] = c;
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }
}
