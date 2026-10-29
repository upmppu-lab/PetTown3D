using System;
using UnityEngine;

public enum PetTownExpansionDirection { Left,Right,Front,Back }
public enum PetTownExpansionStatus { Ready,NeedsClearing,Blocked }
[Serializable] public sealed class PetTownExpansionSettings
{
    [Min(3)] public float moduleDepth=3;
    [Min(1)] public int shopGoldPerSquareMeter=20,cafeGoldPerSquareMeter=24,hospitalGoldPerSquareMeter=28;
    [Min(15)] public float maximumSpan=45;
    public int Rate(int kind)=>kind==3?cafeGoldPerSquareMeter:kind==4?hospitalGoldPerSquareMeter:shopGoldPerSquareMeter;
}
public struct PetTownExpansionPlan
{
    public Rect Before,After,Added;
    public PetTownExpansionDirection Direction;
    public int Cost;
    public static PetTownExpansionPlan Create(PetTownInteriorState owner,PetTownExpansionDirection direction,PetTownExpansionSettings settings)
    {
        Rect before=owner.LocalBounds,after=before;float depth=Mathf.Max(3,Mathf.Round(settings.moduleDepth/3)*3);
        Rect added=before;
        switch(direction)
        {
            case PetTownExpansionDirection.Left:after.xMin-=depth;added=new Rect(after.xMin,before.yMin,depth,before.height);break;
            case PetTownExpansionDirection.Right:after.xMax+=depth;added=new Rect(before.xMax,before.yMin,depth,before.height);break;
            case PetTownExpansionDirection.Front:after.yMin-=depth;added=new Rect(before.xMin,after.yMin,before.width,depth);break;
            case PetTownExpansionDirection.Back:after.yMax+=depth;added=new Rect(before.xMin,before.yMax,before.width,depth);break;
        }
        return new PetTownExpansionPlan{Before=before,After=after,Added=added,Direction=direction,Cost=Mathf.CeilToInt(added.width*added.height*settings.Rate(owner.Kind))};
    }
    public static Rect WorldArea(PetTownInteriorState owner,Rect local)
    {
        var a=owner.transform.TransformPoint(new Vector3(local.xMin,0,local.yMin));var b=owner.transform.TransformPoint(new Vector3(local.xMax,0,local.yMax));
        return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.z,b.z),Mathf.Max(a.x,b.x),Mathf.Max(a.z,b.z));
    }
}
public struct PetTownExpansionCheck
{
    public PetTownExpansionStatus Status;public string Reason;
    public bool CanCommit=>Status==PetTownExpansionStatus.Ready;
    public static PetTownExpansionCheck Block(string reason)=>new PetTownExpansionCheck{Status=PetTownExpansionStatus.Blocked,Reason=reason};
}
