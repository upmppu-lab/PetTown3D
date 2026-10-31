using System;
using UnityEngine;

public sealed class PetTownPlacementPlanner
{
    public int Kind { get; private set; }=-1;
    public int FacingQuarter { get; private set; }
    public bool ManualRotation { get; private set; }
    public bool HasRoadSuggestion { get; private set; }
    PetTownBuildingDefinition definition;
    int variant;
    bool hasQuery; Vector3 queryCenter; float queryDistance; int queryRevision;
    public void Select(int kind,PetTownBuildingDefinition value,int modelVariant)
    {
        if(Kind==kind)return;
        Kind=kind;definition=value;variant=modelVariant;hasQuery=false;FacingQuarter=0;ManualRotation=false;HasRoadSuggestion=false;
    }
    public void RotateClockwise(){FacingQuarter=(FacingQuarter+1)%4;ManualRotation=true;}
    public void Recommend(Vector3 center,float maxDistance)
    {
        if(hasQuery&&queryCenter==center&&queryDistance==maxDistance&&queryRevision==PetTownRoadPath.Revision)return;
        hasQuery=true;queryCenter=center;queryDistance=maxDistance;queryRevision=PetTownRoadPath.Revision;
        HasRoadSuggestion=false;
        if(definition==null||!definition.hasEntrance)return;
        if(PetTownRoadPath.TryNearest(center,maxDistance,out Vector3 nearest))
        {
            Vector3 direction=nearest-center;direction.y=0;
            if(direction.sqrMagnitude<.01f)return;
            HasRoadSuggestion=true;
            if(!ManualRotation)FacingQuarter=QuarterFacing(direction,FacingQuarter);
        }
        else if(!ManualRotation)FacingQuarter=0;
    }
    public static int QuarterFacing(Vector3 direction,int previous=0)
    {
        direction.y=0;
        if(direction.sqrMagnitude<.0001f)return previous;
        direction.Normalize();
        int best=previous;float score=Vector3.Dot(Quaternion.Euler(0,best*90f,0)*Vector3.forward,direction);
        for(int i=0;i<4;i++)
        {
            float dot=Vector3.Dot(Quaternion.Euler(0,i*90f,0)*Vector3.forward,direction);
            if(dot>score+.0001f){best=i;score=dot;}
        }
        return best;
    }
    public PetTownBuildingSaveRecord Snapshot(bool newId=false)
    {
        return new PetTownBuildingSaveRecord{kind=Kind,id=newId?Guid.NewGuid().ToString("N"):null,facingQuarter=FacingQuarter,
            modelVariant=variant,modelHeight=3.8f,modelYawCorrection=definition.variantModelYawCorrections!=null&&variant>=0&&variant<definition.variantModelYawCorrections.Length?definition.variantModelYawCorrections[variant]:definition.modelYawCorrection,footprintMeters=definition.footprintMeters,
            entranceLocal=definition.variantEntranceLocalOverrides!=null&&variant>=0&&variant<definition.variantEntranceLocalOverrides.Length?definition.variantEntranceLocalOverrides[variant]:definition.entranceLocal,entranceBaseYaw=definition.entranceBaseYaw,hasEntrance=definition.hasEntrance};
    }
}