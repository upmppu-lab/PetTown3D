using System;
using UnityEngine;

// Generates architecture only. Furniture and the original building transform are untouched.
public static class PetTownExpansionShell
{
    public static bool IsOriginalArchitecture(Transform t)=>t.name.StartsWith("Modular_Wall_")||t.name=="Toggle_Roof"||t.name=="Foundation_Trim"||t.name=="Window_Glass"||t.name=="Window_Sill";
    public static GameObject Build(PetTownInteriorState owner,Rect bounds,bool roofVisible,Action<GameObject> ignore,Action<GameObject> obstacle)
    {
        var floor=Resources.Load<GameObject>("Modular/Floor_01");var wall=Resources.Load<GameObject>("Modular/Wall_Simple_01");var roof=Resources.Load<GameObject>("Modular/Roof_01");
        if(floor==null||wall==null||roof==null)throw new InvalidOperationException("증축에 필요한 3m 건축 모듈이 없습니다.");
        var shell=new GameObject("Expansion_Shell");shell.transform.SetParent(owner.transform,false);
        try
        {
            Rect original=owner.OriginalBounds;
            for(float x=bounds.xMin;x<bounds.xMax-.01f;x+=3)for(float z=bounds.yMin;z<bounds.yMax-.01f;z+=3)
            {
                if(original.Contains(new Vector2(x+1.5f,z+1.5f)))continue;
                var tile=UnityEngine.Object.Instantiate(floor,shell.transform);tile.name="Expansion_Floor";tile.transform.localPosition=new Vector3(x+3,owner.FloorOffset,z);tile.transform.localRotation=Quaternion.identity;
                foreach(var c in tile.GetComponentsInChildren<Collider>())UnityEngine.Object.Destroy(c);
            }
            for(float x=bounds.xMin;x<bounds.xMax-.01f;x+=3)Wall(new Vector3(x,0,bounds.yMax),90);
            for(float z=bounds.yMin;z<bounds.yMax-.01f;z+=3)Wall(new Vector3(bounds.xMin,0,z),0);
            // Front/right are intentionally open cutaway sides, matching the existing shop.
            var top=new GameObject("Toggle_Roof");top.transform.SetParent(shell.transform,false);
            for(float x=bounds.xMin;x<bounds.xMax-.01f;x+=3)for(float z=bounds.yMin;z<bounds.yMax-.01f;z+=3)
            {var tile=UnityEngine.Object.Instantiate(roof,top.transform);tile.transform.localPosition=new Vector3(x+3,0,z);tile.transform.localRotation=Quaternion.identity;ignore(tile);}
            top.SetActive(roofVisible);return shell;
        }
        catch{shell.SetActive(false);UnityEngine.Object.Destroy(shell);throw;}
        void Wall(Vector3 position,float yaw)
        {
            var tile=UnityEngine.Object.Instantiate(wall,shell.transform);tile.name="Expansion_Wall";tile.transform.localPosition=position;tile.transform.localRotation=Quaternion.Euler(0,yaw,0);ignore(tile);obstacle(tile);
        }
    }
}
