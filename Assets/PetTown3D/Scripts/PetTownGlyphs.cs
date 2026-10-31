using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetTownGlyph
{
    Farm,Ranch,Paw,Cafe,Hospital,Hammer,Armchair,Bag,Shop,Person,People,Gear,Dog,Cat,Rabbit,Roof,Tree,Expand,Save,Scissors,Box,Board,Bone,Heart,Cage,Book,Coin,Fish,Bowl,Drop,Sparkle,Egg,Road
}

// Flat vector-style glyphs drawn procedurally with signed distance shapes (anti-aliased, transparent background).
// Used for the HUD buttons and for catalog list pictures. Nothing is imported or modified; textures are owned and released here.
public static class PetTownGlyphs
{
    public const int FacilityCount=5; // Farm, Ranch, Paw (park/zoo), Cafe, Hospital
    const int Size=128;
    static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
    static readonly List<Texture2D> textures=new List<Texture2D>();
    static readonly Color White=Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache(){cache.Clear();textures.Clear();}

    public static Color Tint(PetTownGlyph g)
    {
        switch(g)
        {
            case PetTownGlyph.Farm:return C(0x4F,0xAE,0x5B);case PetTownGlyph.Ranch:return C(0xE0,0x8A,0x45);case PetTownGlyph.Paw:return C(0x36,0xA3,0x9E);
            case PetTownGlyph.Cafe:return C(0xA9,0x72,0x4B);case PetTownGlyph.Hospital:return C(0xE2,0x58,0x6A);case PetTownGlyph.Hammer:return C(0xE5,0x8B,0x2A);
            case PetTownGlyph.Armchair:return C(0xC8,0x69,0x4B);case PetTownGlyph.Bag:return C(0x7A,0x6A,0xD8);case PetTownGlyph.Shop:return C(0x3F,0x8F,0xD9);
            case PetTownGlyph.Person:return C(0x8E,0x7A,0xE0);case PetTownGlyph.People:return C(0x5B,0x8D,0xEF);case PetTownGlyph.Gear:return C(0x6C,0x7A,0x89);
            case PetTownGlyph.Dog:return C(0xF2,0xA6,0x5A);case PetTownGlyph.Cat:return C(0xE5,0x8F,0xB0);case PetTownGlyph.Rabbit:return C(0x7F,0xB2,0xF0);
            case PetTownGlyph.Roof:return C(0xC7,0x7D,0x5A);case PetTownGlyph.Tree:return C(0x4F,0xAE,0x5B);case PetTownGlyph.Expand:return C(0x5B,0x8D,0xEF);
            case PetTownGlyph.Save:return C(0x6C,0x7A,0x89);case PetTownGlyph.Scissors:return C(0xE2,0x58,0x6A);case PetTownGlyph.Box:return C(0xB9,0x86,0x55);
            case PetTownGlyph.Board:return C(0xD9,0xA4,0x41);case PetTownGlyph.Bone:return C(0xE0,0x8A,0x45);case PetTownGlyph.Heart:return C(0xE8,0x5D,0x75);
            case PetTownGlyph.Cage:return C(0x4A,0xA3,0xA8);case PetTownGlyph.Book:return C(0x7B,0x5E,0xA7);case PetTownGlyph.Coin:return C(0xE0,0xA5,0x26);
            case PetTownGlyph.Fish:return C(0x3E,0x9B,0xD6);case PetTownGlyph.Bowl:return C(0xD9,0x82,0x4B);case PetTownGlyph.Drop:return C(0x3E,0x9B,0xD6);
            case PetTownGlyph.Sparkle:return C(0x37,0xB6,0xA5);default:return C(0xE6,0xB0,0x4B);
        }
    }
    static Color C(int r,int g,int b)=>new Color32((byte)r,(byte)g,(byte)b,255);

    // White glyph on a transparent background (for HUD discs that provide their own colour).
    public static Sprite Glyph(PetTownGlyph g)
    {
        string key="glyph"+(int)g;Sprite sprite;if(cache.TryGetValue(key,out sprite)&&sprite)return sprite;
        var p=new Painter(Size,Size);Draw(p,g);return Store(key,p.ToTexture(),Vector4.zero);
    }
    public static Sprite Glyph(int facilityIndex)=>Glyph((PetTownGlyph)Mathf.Clamp(facilityIndex,0,FacilityCount-1));
    // Coloured disc with the glyph already on it (for catalog list pictures and window header icons).
    public static Sprite Badge(PetTownGlyph g)
    {
        string key="badge"+(int)g;Sprite sprite;if(cache.TryGetValue(key,out sprite)&&sprite)return sprite;
        var p=new Painter(Size,Size);p.Fill(Painter.Circle(new Vector2(64,64),62),Tint(g));
        var shine=new Color(1,1,1,.16f);p.Fill(Painter.Ellipse(new Vector2(64,100),44,22,0),shine);
        var glyph=new Painter(Size,Size);Draw(glyph,g);p.Overlay(glyph,64,64,.74f);
        return Store(key,p.ToTexture(),Vector4.zero);
    }
    public static Sprite Disc()
    {
        Sprite sprite;if(cache.TryGetValue("disc",out sprite)&&sprite)return sprite;
        var p=new Painter(Size,Size);p.Fill(Painter.Circle(new Vector2(64,64),63),White);return Store("disc",p.ToTexture(),Vector4.zero);
    }
    public static Sprite Pill()
    {
        Sprite sprite;if(cache.TryGetValue("pill",out sprite)&&sprite)return sprite;
        var p=new Painter(64,32);p.Fill(Painter.RoundRect(new Vector2(32,16),31,15,15),White);return Store("pill",p.ToTexture(),new Vector4(15,15,15,15));
    }
    // Small rounded rectangle for text plates (3 texels per canvas unit, nine-sliced).
    public static Sprite Plate()
    {
        Sprite sprite;if(cache.TryGetValue("plate",out sprite)&&sprite)return sprite;
        var p=new Painter(96,96);p.Fill(Painter.RoundRect(new Vector2(48,48),47.5f,47.5f,12),White);return Store("plate",p.ToTexture(),new Vector4(18,18,18,18),300);
    }
    static Sprite Store(string key,Texture2D texture,Vector4 border,float ppu=100)
    {
        textures.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect,border);sprite.name="PetTown_Glyph_"+key;cache[key]=sprite;return sprite;
    }
    public static void Release()
    {
        foreach(var sprite in cache.Values)if(sprite)UnityEngine.Object.Destroy(sprite);cache.Clear();
        foreach(var texture in textures)if(texture)UnityEngine.Object.Destroy(texture);textures.Clear();
    }

    // Picks the glyph that matches a list entry's text (name first, then the window title).
    public static PetTownGlyph Pick(string text,PetTownGlyph fallback=PetTownGlyph.Shop)
    {
        if(string.IsNullOrEmpty(text))return fallback;
        bool Has(params string[] words){foreach(var w in words)if(text.Contains(w))return true;return false;}
        if(Has("강아지"))return PetTownGlyph.Dog;if(Has("고양이"))return PetTownGlyph.Cat;if(Has("토끼"))return PetTownGlyph.Rabbit;
        if(Has("지붕"))return PetTownGlyph.Roof;if(Has("자연","환경"))return PetTownGlyph.Tree;if(Has("증축"))return PetTownGlyph.Expand;if(Has("저장"))return PetTownGlyph.Save;
        if(Has("직원","고용","급여","교육","근무"))return PetTownGlyph.Person;if(Has("미용"))return PetTownGlyph.Scissors;if(Has("배송"))return PetTownGlyph.Box;
        if(Has("퀘스트","의뢰","게시판"))return PetTownGlyph.Board;if(Has("놀기","놀이","장난감"))return PetTownGlyph.Bone;if(Has("교배","탄생","번식","새끼"))return PetTownGlyph.Heart;
        if(Has("전시","케이지"))return PetTownGlyph.Cage;if(Has("도감"))return PetTownGlyph.Book;if(Has("물고기","낚시"))return PetTownGlyph.Fish;
        if(Has("달걀","병아리","닭","알 "))return PetTownGlyph.Egg;
        if(Has("먹이","사료","재료"))return PetTownGlyph.Bowl;if(Has("물주기","급수","깨끗한 물"))return PetTownGlyph.Drop;if(Has("청소","청결"))return PetTownGlyph.Sparkle;
        if(Has("농장","씨앗","밭","심기","수확","작물"))return PetTownGlyph.Farm;
        if(Has("목장","젖소","가축","우유","양털","돼지","고기")||HasWord(text,"양")||HasWord(text,"소"))return PetTownGlyph.Ranch;
        if(Has("동물원","사슴","호랑이","펭귄","코끼리","기린","얼룩말"))return PetTownGlyph.Paw;if(Has("공원","호수"))return PetTownGlyph.Tree;
        if(Has("카페","레시피","메뉴","조리"))return PetTownGlyph.Cafe;if(Has("병원","진료","검진","치료","환자"))return PetTownGlyph.Hospital;
        if(Has("결제","기록","수납","골드","판매","출하"))return PetTownGlyph.Coin;
        if(Has("건설","짓기","건물"))return PetTownGlyph.Hammer;if(Has("가구","꾸미기"))return PetTownGlyph.Armchair;if(Has("가방","아이템"))return PetTownGlyph.Bag;
        if(Has("주민"))return PetTownGlyph.People;if(Has("설정"))return PetTownGlyph.Gear;if(Has("펫샵"))return PetTownGlyph.Shop;if(Has("펫"))return PetTownGlyph.Paw;
        return fallback;
    }
    static bool HasWord(string text,string word)
    {
        foreach(var token in text.Split(' ','·','/','(',')'))if(token==word)return true;return false;
    }

    static Vector2 V(float x,float y)=>new Vector2(x,y);
    static void Draw(Painter p,PetTownGlyph g)
    {
        var cream=new Color(1f,.93f,.8f);var pink=new Color(1f,.74f,.78f);var dark=new Color(.22f,.15f,.13f);var brown=new Color(.56f,.36f,.22f);
        switch(g)
        {
            case PetTownGlyph.Farm:
                p.Fill(Painter.RoundRect(V(64,26),36,7,7),White);p.Fill(Painter.Capsule(V(64,32),V(64,78),4.5f),White);
                p.Fill(Painter.Ellipse(V(45,86),25,11,152),White);p.Fill(Painter.Ellipse(V(83,86),25,11,28),White);break;
            case PetTownGlyph.Ranch:
                p.Fill(Painter.Capsule(V(40,100),V(31,114),5.5f),cream);p.Fill(Painter.Capsule(V(88,100),V(97,114),5.5f),cream);
                p.Fill(Painter.Ellipse(V(27,86),17,9,18),dark);p.Fill(Painter.Ellipse(V(101,86),17,9,162),dark);
                p.Fill(Painter.RoundRect(V(64,66),31,38,24),White);p.Fill(Painter.Ellipse(V(82,90),11,8,10),dark);
                p.Fill(Painter.RoundRect(V(64,38),23,15,15),pink);p.Fill(Painter.Circle(V(55,38),3.6f),dark);p.Fill(Painter.Circle(V(73,38),3.6f),dark);
                p.Fill(Painter.Circle(V(49,70),4.8f),dark);p.Fill(Painter.Circle(V(79,70),4.8f),dark);break;
            case PetTownGlyph.Paw:
                p.Fill(Painter.Ellipse(V(64,42),27,22,0),White);p.Fill(Painter.Ellipse(V(30,72),10,13,25),White);p.Fill(Painter.Ellipse(V(49,93),10,13,8),White);
                p.Fill(Painter.Ellipse(V(79,93),10,13,-8),White);p.Fill(Painter.Ellipse(V(98,72),10,13,-25),White);break;
            case PetTownGlyph.Cafe:
                p.Fill(Painter.RoundRect(V(60,62),30,25,14),White);p.Fill(Painter.Ring(V(94,62),14,3.2f),White);p.Fill(Painter.RoundRect(V(60,30),42,5.5f,5.5f),White);
                p.Fill(Painter.Capsule(V(48,95),V(48,108),3.6f),White);p.Fill(Painter.Capsule(V(68,95),V(68,112),3.6f),White);break;
            case PetTownGlyph.Hospital:
                p.Fill(Painter.RoundRect(V(64,64),15,42,7),White);p.Fill(Painter.RoundRect(V(64,64),42,15,7),White);break;
            case PetTownGlyph.Hammer:
                p.Fill(Painter.Capsule(V(34,30),V(80,76),6.5f),White);p.Fill(Painter.Rotate(Painter.RoundRect(V(86,86),25,12,4),V(86,86),-45),White);break;
            case PetTownGlyph.Armchair:
                p.Fill(Painter.RoundRect(V(64,78),28,24,12),White);p.Fill(Painter.RoundRect(V(64,48),34,12,6),White);
                p.Fill(Painter.RoundRect(V(28,54),9,22,6),White);p.Fill(Painter.RoundRect(V(100,54),9,22,6),White);
                p.Fill(Painter.RoundRect(V(44,24),5,6,2),White);p.Fill(Painter.RoundRect(V(84,24),5,6,2),White);break;
            case PetTownGlyph.Bag:
                p.Fill(Painter.Ring(V(64,92),15,4),White);p.Fill(Painter.RoundRect(V(64,52),36,30,14),White);
                p.Erase(Painter.RoundRect(V(64,68),28,2.6f,1.3f));p.Erase(Painter.Circle(V(64,56),5.5f));break;
            case PetTownGlyph.Shop:
                p.Fill(Painter.Poly(V(18,80),V(110,80),V(100,106),V(28,106)),White);
                for(int i=0;i<4;i++)p.Fill(Painter.Circle(V(32+i*21.3f,80),10.7f),White);
                p.Fill(Painter.RoundRect(V(64,44),36,26,4),White);p.Erase(Painter.RoundRect(V(64,32),9,15,3));
                p.Erase(Painter.RoundRect(V(43,52),8,7,2));p.Erase(Painter.RoundRect(V(85,52),8,7,2));break;
            case PetTownGlyph.Person:
                p.Fill(Painter.Circle(V(64,90),18),White);p.Fill(Painter.RoundRect(V(64,40),36,26,18),White);break;
            case PetTownGlyph.People:
                p.Fill(Painter.Circle(V(42,88),13),White);p.Fill(Painter.RoundRect(V(42,46),24,18,11),White);
                p.Erase(Painter.Expand(Painter.Circle(V(84,90),16),5));p.Erase(Painter.Expand(Painter.RoundRect(V(84,44),30,24,15),5));
                p.Fill(Painter.Circle(V(84,90),16),White);p.Fill(Painter.RoundRect(V(84,44),30,24,15),White);break;
            case PetTownGlyph.Gear:
                for(int i=0;i<8;i++)p.Fill(Painter.Rotate(Painter.RoundRect(V(64,100),8,11,3),V(64,64),i*45),White);
                p.Fill(Painter.Circle(V(64,64),31),White);p.Erase(Painter.Circle(V(64,64),13));break;
            case PetTownGlyph.Dog:
                p.Fill(Painter.Ellipse(V(28,76),13,26,-18),brown);p.Fill(Painter.Ellipse(V(100,76),13,26,18),brown);p.Fill(Painter.RoundRect(V(64,60),33,33,24),White);
                p.Fill(Painter.Ellipse(V(64,44),17,13,0),cream);p.Fill(Painter.Ellipse(V(64,50),8,5.5f,0),dark);p.Fill(Painter.Circle(V(51,70),4.6f),dark);p.Fill(Painter.Circle(V(77,70),4.6f),dark);break;
            case PetTownGlyph.Cat:
                p.Fill(Painter.Poly(V(28,64),V(32,112),V(62,88)),White);p.Fill(Painter.Poly(V(100,64),V(96,112),V(66,88)),White);
                p.Fill(Painter.Poly(V(37,78),V(38,100),V(54,88)),pink);p.Fill(Painter.Poly(V(91,78),V(90,100),V(74,88)),pink);
                p.Fill(Painter.Ellipse(V(64,56),37,31,0),White);p.Fill(Painter.Circle(V(50,60),4.6f),dark);p.Fill(Painter.Circle(V(78,60),4.6f),dark);p.Fill(Painter.Circle(V(64,48),4),pink);
                var whisker=new Color(.7f,.66f,.66f);p.Fill(Painter.Capsule(V(14,46),V(36,50),1.8f),whisker);p.Fill(Painter.Capsule(V(14,56),V(36,54),1.8f),whisker);
                p.Fill(Painter.Capsule(V(114,46),V(92,50),1.8f),whisker);p.Fill(Painter.Capsule(V(114,56),V(92,54),1.8f),whisker);break;
            case PetTownGlyph.Rabbit:
                p.Fill(Painter.Ellipse(V(46,92),10,26,-8),White);p.Fill(Painter.Ellipse(V(82,92),10,26,8),White);
                p.Fill(Painter.Ellipse(V(46,92),5,18,-8),pink);p.Fill(Painter.Ellipse(V(82,92),5,18,8),pink);
                p.Fill(Painter.Ellipse(V(64,48),31,27,0),White);p.Fill(Painter.Circle(V(52,52),4.2f),dark);p.Fill(Painter.Circle(V(76,52),4.2f),dark);p.Fill(Painter.Circle(V(64,41),3.6f),pink);break;
            case PetTownGlyph.Roof:
                p.Fill(Painter.Poly(V(10,68),V(64,112),V(118,68)),White);p.Fill(Painter.RoundRect(V(64,38),34,26,4),White);p.Erase(Painter.RoundRect(V(64,30),9,14,3));break;
            case PetTownGlyph.Tree:
                p.Fill(Painter.RoundRect(V(64,26),6,18,2),White);p.Fill(Painter.Circle(V(64,74),28),White);p.Fill(Painter.Circle(V(40,56),20),White);p.Fill(Painter.Circle(V(88,56),20),White);break;
            case PetTownGlyph.Expand:
                p.Fill(Painter.RoundRect(V(64,64),44,44,10),White);p.Erase(Painter.RoundRect(V(64,64),36,36,5));
                p.Fill(Painter.RoundRect(V(64,64),6,22,2.5f),White);p.Fill(Painter.RoundRect(V(64,64),22,6,2.5f),White);break;
            case PetTownGlyph.Save:
                p.Fill(Painter.RoundRect(V(64,64),40,40,8),White);p.Erase(Painter.RoundRect(V(76,92),12,10,2));p.Erase(Painter.RoundRect(V(64,38),26,17,3));break;
            case PetTownGlyph.Scissors:
                p.Fill(Painter.Ring(V(38,34),14,4.5f),White);p.Fill(Painter.Ring(V(90,34),14,4.5f),White);
                p.Fill(Painter.Capsule(V(44,46),V(88,108),4.8f),White);p.Fill(Painter.Capsule(V(84,46),V(40,108),4.8f),White);break;
            case PetTownGlyph.Box:
                p.Fill(Painter.RoundRect(V(64,50),38,28,6),White);p.Fill(Painter.RoundRect(V(64,86),44,11,4),White);p.Erase(Painter.RoundRect(V(64,50),5,28,0));p.Erase(Painter.RoundRect(V(64,86),5,11,0));p.Fill(Painter.Capsule(V(64,98),V(50,110),4),White);p.Fill(Painter.Capsule(V(64,98),V(78,110),4),White);break;
            case PetTownGlyph.Board:
                p.Fill(Painter.RoundRect(V(64,58),34,46,9),White);p.Fill(Painter.RoundRect(V(64,104),17,9,4.5f),White);
                p.Erase(Painter.RoundRect(V(64,78),20,2.6f,1.3f));p.Erase(Painter.RoundRect(V(64,62),20,2.6f,1.3f));p.Erase(Painter.RoundRect(V(56,46),12,2.6f,1.3f));break;
            case PetTownGlyph.Bone:
                p.Fill(Painter.Capsule(V(38,38),V(90,90),8),White);p.Fill(Painter.Circle(V(28,48),11),White);p.Fill(Painter.Circle(V(48,28),11),White);
                p.Fill(Painter.Circle(V(100,80),11),White);p.Fill(Painter.Circle(V(80,100),11),White);break;
            case PetTownGlyph.Heart:
                p.Fill(Painter.Circle(V(45,76),22),White);p.Fill(Painter.Circle(V(83,76),22),White);p.Fill(Painter.Poly(V(25,68),V(103,68),V(64,18)),White);break;
            case PetTownGlyph.Cage:
                p.Fill(Painter.RoundRect(V(64,64),40,36,9),White);p.Erase(Painter.RoundRect(V(64,64),32,28,5));
                p.Fill(Painter.RoundRect(V(46,64),2.6f,30,1),White);p.Fill(Painter.RoundRect(V(64,64),2.6f,30,1),White);p.Fill(Painter.RoundRect(V(82,64),2.6f,30,1),White);break;
            case PetTownGlyph.Book:
                p.Fill(Painter.RoundRect(V(42,62),21,34,6),White);p.Fill(Painter.RoundRect(V(86,62),21,34,6),White);
                p.Erase(Painter.RoundRect(V(42,74),11,2.4f,1.2f));p.Erase(Painter.RoundRect(V(42,62),11,2.4f,1.2f));p.Erase(Painter.RoundRect(V(86,74),11,2.4f,1.2f));p.Erase(Painter.RoundRect(V(86,62),11,2.4f,1.2f));break;
            case PetTownGlyph.Coin:
                p.Fill(Painter.Circle(V(64,64),38),White);p.Erase(Painter.Ring(V(64,64),27,2.8f));p.Fill(Painter.RoundRect(V(64,64),5,15,2.5f),White);p.Fill(Painter.RoundRect(V(64,64),11,5,2.5f),White);break;
            case PetTownGlyph.Fish:
                p.Fill(Painter.Ellipse(V(56,64),34,22,0),White);p.Fill(Painter.Poly(V(84,64),V(112,88),V(112,40)),White);p.Erase(Painter.Circle(V(40,70),4.2f));break;
            case PetTownGlyph.Bowl:
                p.Fill(Painter.Poly(V(20,70),V(108,70),V(94,28),V(34,28)),White);p.Fill(Painter.RoundRect(V(64,74),48,6,3),White);
                p.Fill(Painter.Circle(V(48,88),9),White);p.Fill(Painter.Circle(V(66,94),10),White);p.Fill(Painter.Circle(V(84,88),9),White);break;
            case PetTownGlyph.Drop:
                p.Fill(Painter.Circle(V(64,46),27),White);p.Fill(Painter.Poly(V(64,112),V(38,56),V(90,56)),White);break;
            case PetTownGlyph.Sparkle:
                p.Fill(Painter.Ellipse(V(64,64),9,44,0),White);p.Fill(Painter.Ellipse(V(64,64),44,9,0),White);
                p.Fill(Painter.Circle(V(100,100),6),White);p.Fill(Painter.Circle(V(28,100),5),White);p.Fill(Painter.Circle(V(100,28),5),White);break;
            case PetTownGlyph.Road:
                p.Fill(Painter.Poly(V(44,12),V(84,12),V(102,116)),White);p.Fill(Painter.Poly(V(44,12),V(102,116),V(26,116)),White);
                p.Erase(Painter.RoundRect(V(64,28),2.6f,8,1));p.Erase(Painter.RoundRect(V(64,56),2.4f,8,1));p.Erase(Painter.RoundRect(V(64,86),2.2f,8,1));break;
            default: // Egg
                p.Fill(Painter.Rotate(Painter.Ellipse(V(64,60),28,38,0),V(64,60),8),White);break;
        }
    }

    // Minimal RGBA canvas. Shapes are signed distance functions in pixel units (negative inside).
    sealed class Painter
    {
        readonly int w,h;readonly Color[] px;
        public Painter(int width,int height){w=width;h=height;px=new Color[w*h];}
        public void Fill(Func<Vector2,float> sd,Color color)
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float coverage=Mathf.Clamp01(.5f-sd(new Vector2(x+.5f,y+.5f)))*color.a;if(coverage<=0)continue;
                var dst=px[y*w+x];float outA=coverage+dst.a*(1-coverage);
                px[y*w+x]=outA<=0?Color.clear:new Color((color.r*coverage+dst.r*dst.a*(1-coverage))/outA,(color.g*coverage+dst.g*dst.a*(1-coverage))/outA,(color.b*coverage+dst.b*dst.a*(1-coverage))/outA,outA);
            }
        }
        public void Erase(Func<Vector2,float> sd)
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float coverage=Mathf.Clamp01(.5f-sd(new Vector2(x+.5f,y+.5f)));if(coverage<=0)continue;
                var c=px[y*w+x];c.a*=1-coverage;px[y*w+x]=c;
            }
        }
        // Draws another painter scaled around the centre on top of this one.
        public void Overlay(Painter other,float cx,float cy,float scale)
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float sx=(x+.5f-cx)/scale+other.w*.5f-.5f,sy=(y+.5f-cy)/scale+other.h*.5f-.5f;
                var src=other.Sample(sx,sy);if(src.a<=0)continue;
                var dst=px[y*w+x];float outA=src.a+dst.a*(1-src.a);
                px[y*w+x]=outA<=0?Color.clear:new Color((src.r*src.a+dst.r*dst.a*(1-src.a))/outA,(src.g*src.a+dst.g*dst.a*(1-src.a))/outA,(src.b*src.a+dst.b*dst.a*(1-src.a))/outA,outA);
            }
        }
        Color Sample(float fx,float fy)
        {
            int x0=Mathf.FloorToInt(fx),y0=Mathf.FloorToInt(fy);float tx=fx-x0,ty=fy-y0;
            return Color.Lerp(Color.Lerp(At(x0,y0),At(x0+1,y0),tx),Color.Lerp(At(x0,y0+1),At(x0+1,y0+1),tx),ty);
        }
        Color At(int x,int y)=>x<0||y<0||x>=w||y>=h?Color.clear:px[y*w+x];
        public Texture2D ToTexture()
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,name="PetTown_Glyph"};texture.SetPixels(px);texture.Apply(false,false);return texture;
        }
        public static Func<Vector2,float> Circle(Vector2 c,float r)=>q=>(q-c).magnitude-r;
        public static Func<Vector2,float> Ring(Vector2 c,float r,float halfThickness)=>q=>Mathf.Abs((q-c).magnitude-r)-halfThickness;
        public static Func<Vector2,float> Expand(Func<Vector2,float> f,float amount)=>q=>f(q)-amount;
        public static Func<Vector2,float> Rotate(Func<Vector2,float> f,Vector2 c,float degrees)
        {
            float cos=Mathf.Cos(degrees*Mathf.Deg2Rad),sin=Mathf.Sin(degrees*Mathf.Deg2Rad);
            return q=>{var d=q-c;return f(c+new Vector2(d.x*cos+d.y*sin,-d.x*sin+d.y*cos));};
        }
        public static Func<Vector2,float> Ellipse(Vector2 c,float a,float b,float degrees)
        {
            float cos=Mathf.Cos(-degrees*Mathf.Deg2Rad),sin=Mathf.Sin(-degrees*Mathf.Deg2Rad);float scale=Mathf.Min(a,b);
            return q=>{var d=q-c;float x=d.x*cos-d.y*sin,y=d.x*sin+d.y*cos;return (Mathf.Sqrt(x*x/(a*a)+y*y/(b*b))-1)*scale;};
        }
        public static Func<Vector2,float> RoundRect(Vector2 c,float halfW,float halfH,float radius)
        {
            return q=>{float x=Mathf.Abs(q.x-c.x)-(halfW-radius),y=Mathf.Abs(q.y-c.y)-(halfH-radius);return new Vector2(Mathf.Max(x,0),Mathf.Max(y,0)).magnitude+Mathf.Min(Mathf.Max(x,y),0)-radius;};
        }
        public static Func<Vector2,float> Capsule(Vector2 a,Vector2 b,float r)
        {
            return q=>{var pa=q-a;var ba=b-a;float t=Mathf.Clamp01(Vector2.Dot(pa,ba)/Mathf.Max(.0001f,ba.sqrMagnitude));return (pa-ba*t).magnitude-r;};
        }
        // Convex polygon, either winding.
        public static Func<Vector2,float> Poly(params Vector2[] v)
        {
            float area=0;for(int i=0;i<v.Length;i++){var a=v[i];var b=v[(i+1)%v.Length];area+=a.x*b.y-b.x*a.y;}float sign=area>0?1:-1;
            return q=>{float d=-1e9f;for(int i=0;i<v.Length;i++){var a=v[i];var e=v[(i+1)%v.Length]-a;var n=new Vector2(e.y,-e.x).normalized*sign;d=Mathf.Max(d,Vector2.Dot(q-a,n));}return d;};
        }
    }
}
