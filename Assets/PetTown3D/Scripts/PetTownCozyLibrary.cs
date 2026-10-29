using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownCozyFurniture
{
    public int kind;
    public bool detail;
    public bool onSurface;
    public string title,category;
    public float height;
    public Vector2 footprint;
    public GameObject prefab;
    public Texture2D thumbnail;
}

// Stable legacy IDs remain 0..5 / detail 0..9. Additional furniture uses detail IDs >=10.
public sealed class PetTownCozyLibrary : ScriptableObject
{
    public PetTownArtPreviewData style;
    public List<PetTownCozyFurniture> furniture=new List<PetTownCozyFurniture>();
    public Material woodFloor,plaster,timber,glass;
    public Texture2D careIcons;
    static PetTownCozyLibrary cached;
    public static PetTownCozyLibrary Current=>cached!=null?cached:(cached=Resources.Load<PetTownCozyLibrary>("UI/Cozy/Library"));
    public PetTownCozyFurniture Find(int kind,bool detail)=>furniture.Find(e=>e.kind==kind&&e.detail==detail);
    public static bool KnownDetail(int kind)=>kind>=0&&(kind<10||(Current!=null&&Current.Find(kind,true)!=null));
    public Texture2D Thumbnail(string heading,int key)
    {
        if(heading.Contains("건물")){foreach(var b in style.buildings)if(b.key==key)return b.thumbnail;}
        if(heading.Contains("가구")){var f=Find(key<6?key:key-6,key>=6);return f?.thumbnail;}
        return null;
    }
}
