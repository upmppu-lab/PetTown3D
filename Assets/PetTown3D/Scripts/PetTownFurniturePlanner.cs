using UnityEngine;

// Pure local-grid geometry, shared by preview and commit.
public static class PetTownFurniturePlanner
{
    public static Vector2 ReservedSize(Vector2 measured,float cell)
    {
        cell=PetTownBuildGrid.ValidCellSize(cell);return new Vector2(Mathf.Max(1,Mathf.CeilToInt((measured.x+.02f)/cell))*cell,Mathf.Max(1,Mathf.CeilToInt((measured.y+.02f)/cell))*cell);
    }
    public static Vector3 Snap(Vector3 world,PetTownInteriorState owner,float cell)
    {
        var local=owner.transform.InverseTransformPoint(world);cell=PetTownBuildGrid.ValidCellSize(cell);local.x=Mathf.Round(local.x/cell)*cell;local.z=Mathf.Round(local.z/cell)*cell;local.y=owner.FloorOffset;return owner.transform.TransformPoint(local);
    }
    public static Rect Area(Vector3 world,PetTownInteriorState owner,Vector2 size,int quarter)
    {
        if((quarter&1)!=0)size=new Vector2(size.y,size.x);var local=owner.transform.InverseTransformPoint(world);return new Rect(local.x-size.x*.5f,local.z-size.y*.5f,size.x,size.y);
    }
    public static string CheckRoom(Vector3 world,PetTownInteriorState owner,Vector2 size,int quarter)
    {
        var area=Area(world,owner,size,quarter);var room=owner.LocalBounds;
        if(area.xMin<room.xMin+.12f||area.xMax>room.xMax-.12f||area.yMin<room.yMin+.12f||area.yMax>room.yMax-.12f)return "가구가 실내 바닥이나 벽의 경계를 벗어납니다.";
        Vector3 entrance=owner.EntranceLocal;var outward=new Vector3(entrance.x,0,entrance.z).normalized;
        var door=PetTownPlacementValidator.EntranceArea(entrance,-outward,1.6f,2f);
        if(area.Overlaps(door))return "출입구 앞 통로를 비워 주세요.";
        return null;
    }
}
