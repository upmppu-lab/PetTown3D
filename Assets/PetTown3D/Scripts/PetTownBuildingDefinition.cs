using System;
using UnityEngine;

[Serializable]
public sealed class PetTownBuildingDefinition
{
    public int kind;
    public string label;
    public Vector2 footprintMeters;
    public bool hasEntrance=true;
    public Vector3 entranceLocal;
    [Tooltip("Authored entrance direction in the unrotated building. 180 = -Z.")]
    public float entranceBaseYaw=180f;
    [Tooltip("Rotation of the imported resident model inside its building root.")]
    public float modelYawCorrection;
    [Tooltip("Optional per-model corrections in resident prefab order; empty uses the shared correction.")]
    public float[] variantModelYawCorrections=new float[0];
    [Tooltip("Optional corrected local entrance/approach point for each resident model.")]
    public Vector3[] variantEntranceLocalOverrides=new Vector3[0];
    public static PetTownBuildingDefinition Default(int kind)
    {
        if(kind==10)return new PetTownBuildingDefinition{kind=10,label="Ranch",footprintMeters=new Vector2(22,22),entranceLocal=new Vector3(0,0,-11)};
        if(kind==9)return new PetTownBuildingDefinition{kind=9,label="Farm",footprintMeters=new Vector2(18,20),entranceLocal=new Vector3(0,0,-10)};
        string[] labels={"Pet Shop","Home","Resident House","Pet Cafe","Pet Hospital","Park","Lake","Zoo"};
        Vector2 size=kind==0?new Vector2(15,15):kind==2?new Vector2(7,7):kind==7?new Vector2(22,22):(kind==5||kind==6)?new Vector2(15,15):new Vector2(11,10);
        float gateX=0;
        return new PetTownBuildingDefinition{kind=kind,label=labels[Mathf.Clamp(kind,0,7)],footprintMeters=size,hasEntrance=true,entranceLocal=new Vector3(gateX,kind==0?.12f:(kind==1||kind==3||kind==4)?.4f:0,-size.y*.5f),modelYawCorrection=kind==2?180f:0f};
    }
    public static PetTownBuildingDefinition[] Defaults()
    {
        var result=new PetTownBuildingDefinition[8];
        for(int i=0;i<result.Length;i++)result[i]=Default(i);
        return result;
    }
}

[Serializable]
public sealed class PetTownBuildingSaveRecord
{
    public int legacyIndex=-1,kind,facingQuarter,modelVariant;
    public string id;
    public float groundY,modelHeight=3.8f,modelYawCorrection,entranceBaseYaw=180f;
    public Vector2 footprintMeters;
    public Vector3 entranceLocal;
    public bool hasEntrance=true;
    public float BuildingYaw => Mathf.Repeat(facingQuarter*90f-entranceBaseYaw,360f);
    public Quaternion Rotation => Quaternion.Euler(0,BuildingYaw,0);
    public Vector3 EntranceForward => Quaternion.Euler(0,facingQuarter*90f,0)*Vector3.forward;
    public Vector2 WorldFootprint
    {
        get
        {
            bool swap=Mathf.RoundToInt(BuildingYaw/90f)%2!=0;
            return swap?new Vector2(footprintMeters.y,footprintMeters.x):footprintMeters;
        }
    }
    public bool ValidFor(int expectedKind)
    {
        return !float.IsNaN(groundY)&&!float.IsInfinity(groundY)&&kind==expectedKind&&kind>=0&&(kind<8||kind==9||kind==10)&&facingQuarter>=0&&facingQuarter<4&&
            footprintMeters.x>0&&footprintMeters.x<=64&&footprintMeters.y>0&&footprintMeters.y<=64&&
            !float.IsNaN(entranceBaseYaw)&&!float.IsInfinity(entranceBaseYaw)&&
            !float.IsNaN(modelYawCorrection)&&!float.IsInfinity(modelYawCorrection)&&modelHeight>=1&&modelHeight<=12;
    }
    public PetTownBuildingSaveRecord Copy(int index)
    {
        var copy=(PetTownBuildingSaveRecord)MemberwiseClone();copy.legacyIndex=index;return copy;
    }
}