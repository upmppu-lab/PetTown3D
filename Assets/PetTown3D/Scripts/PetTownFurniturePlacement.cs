using System;
using UnityEngine;

[Serializable]
public sealed class PetTownFurnitureSaveRecord
{
    public string id;
    public int kind;
    public bool detail,isDefault;
    public Vector3 localPosition;
    public Quaternion localRotation=Quaternion.identity,modelRotation=Quaternion.identity;
    public Vector2 footprint;
    public float modelHeight;
    public bool Valid=>(detail?PetTownCozyLibrary.KnownDetail(kind):kind>=0&&kind<6)&&Finite(localPosition.x)&&Finite(localPosition.y)&&Finite(localPosition.z)&&Mathf.Abs(localPosition.x)<250&&Mathf.Abs(localPosition.y)<100&&Mathf.Abs(localPosition.z)<250&&ValidRotation(localRotation)&&ValidRotation(modelRotation)&&footprint.x>0&&footprint.y>0&&footprint.x<50&&footprint.y<50&&modelHeight>=0&&modelHeight<20;
    static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    static bool ValidRotation(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.01f;
}
[DisallowMultipleComponent]
public sealed class PetTownFurniturePlacement : MonoBehaviour
{
    public PetTownInteriorState Owner { get; private set; }
    public int Kind { get; private set; }
    public bool Detail { get; private set; }
    public bool IsDefault { get; set; }
    public Vector2 Footprint { get; set; }
    public float ModelHeight { get; private set; }
    string id;
    public string Id=>id;
    public void Initialize(PetTownInteriorState owner,int kind,bool detail,Vector2 footprint,float modelHeight,string identity=null)
    {
        Owner=owner;Kind=kind;Detail=detail;Footprint=footprint;ModelHeight=modelHeight;id=string.IsNullOrEmpty(identity)?Guid.NewGuid().ToString("N"):identity;IsDefault=true;
    }
    public PetTownFurnitureSaveRecord Capture()
    {
        var model=transform.Find("Model");
        return new PetTownFurnitureSaveRecord{id=id,kind=Kind,detail=Detail,isDefault=IsDefault,localPosition=Owner.transform.InverseTransformPoint(transform.position),localRotation=Quaternion.Inverse(Owner.transform.rotation)*transform.rotation,modelRotation=model!=null?model.localRotation:Quaternion.identity,footprint=Footprint,modelHeight=ModelHeight};
    }
}
