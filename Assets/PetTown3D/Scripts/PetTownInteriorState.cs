using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownInteriorSaveRecord
{
    public int legacyIndex=-1,kind;
    public string id;
    public Vector2 size;
    public float floorOffset;
    public Vector2 center,originalSize;
    public int expansionVersion;
    public Vector3 entranceLocal;
    public List<PetTownFurnitureSaveRecord> furniture=new List<PetTownFurnitureSaveRecord>();
    public bool Valid=>Finite(center.x)&&Finite(center.y)&&Mathf.Abs(center.x)<100&&Mathf.Abs(center.y)<100&&Finite(size.x)&&Finite(size.y)&&size.x>=1&&size.y>=1&&size.x<=100&&size.y<=100&&Finite(floorOffset)&&Mathf.Abs(floorOffset)<10&&Finite(entranceLocal.x)&&Finite(entranceLocal.y)&&Finite(entranceLocal.z);
    static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
}
// Ownership/coordinates only. Does not move, scale or rebuild the building.
[DisallowMultipleComponent]
public sealed class PetTownInteriorState : MonoBehaviour
{
    [SerializeField] string id;
    [SerializeField] int kind;
    [SerializeField] Vector2 size;
    [SerializeField] Vector2 center,originalSize;
    [SerializeField] float floorOffset;
    [SerializeField] Vector3 entranceLocal;
    public string Id=>id;
    public int Kind=>kind;
    public Vector2 Size=>size;
    public float FloorOffset=>floorOffset;
    public float FloorY=>transform.position.y+floorOffset;
    public Vector3 EntranceLocal=>entranceLocal;
    public Rect LocalBounds=>new Rect(center-size*.5f,size);
    public Rect OriginalBounds=>new Rect(-originalSize*.5f,originalSize);
    public void SetBounds(Rect bounds){center=bounds.center;size=bounds.size;if(kind==0)entranceLocal=new Vector3(Mathf.Clamp(entranceLocal.x,bounds.xMin+1,bounds.xMax-1),floorOffset,bounds.yMin);}
    public void RestoreBounds(PetTownInteriorSaveRecord record){originalSize=record.originalSize.x>0&&record.originalSize.y>0?record.originalSize:size;center=record.expansionVersion>=1?record.center:Vector2.zero;}
    public readonly List<PetTownFurnitureSaveRecord> Unresolved=new List<PetTownFurnitureSaveRecord>();
    public void Initialize(string ownerId,Vector2 roomSize,float floor,Vector3 entrance,int buildingKind=0)
    {
        kind=buildingKind;id=string.IsNullOrEmpty(ownerId)?Guid.NewGuid().ToString("N"):ownerId;size=roomSize;originalSize=roomSize;center=Vector2.zero;floorOffset=floor;entranceLocal=entrance;
    }
    public PetTownInteriorSaveRecord Capture(int index)=>new PetTownInteriorSaveRecord{legacyIndex=index,kind=kind,id=id,size=size,floorOffset=floorOffset,entranceLocal=entranceLocal,expansionVersion=1,center=center,originalSize=originalSize};
}
