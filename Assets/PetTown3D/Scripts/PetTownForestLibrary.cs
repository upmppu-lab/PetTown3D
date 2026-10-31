using UnityEngine;

public sealed class PetTownForestLibrary : ScriptableObject
{
    public GameObject[] trees,groundCover;
    public bool approvedMeadow;
    public Material groundMaterial;
    public Texture2D meadowMask;
    public Vector4 meadowBounds=new Vector4(-110,-110,220,220);
    public Color Coverage(float x,float z)=>meadowMask?meadowMask.GetPixelBilinear(Mathf.PingPong((x-meadowBounds.x)/meadowBounds.z,1),Mathf.PingPong((z-meadowBounds.y)/meadowBounds.w,1)):new Color(0,1,1,.5f);
    public GameObject[] grasses,flowers,shrubs,rocks;
    public GameObject Find(string key)
    {
        foreach(var group in new[]{trees,groundCover})if(group!=null)foreach(var p in group)if(p&&p.name==key)return p;
        if(!approvedMeadow)return null;
        switch(key){
            case "Grass_Common_Short":return At(grasses,0);
            case "Grass_Wispy_Short":return At(grasses,1);
            case "Flower_3_Group":return At(flowers,0);
            case "Flower_4_Group":return At(flowers,1);
            case "Clover_1":return At(flowers,2);
            case "Fern_1":return At(flowers,3);
            case "Bush_Common":return At(shrubs,0);
            case "Bush_Common_Flowers":return At(shrubs,1);
            case "Rock_Medium_1":return At(rocks,0);
        }
        return null;
    }
    static GameObject At(GameObject[] values,int index)=>values!=null&&index<values.Length?values[index]:null;
    static PetTownForestLibrary cached;
    public static PetTownForestLibrary Current=>cached?cached:(cached=Resources.Load<PetTownForestLibrary>("Environment/ApprovedNature/Library")??Resources.Load<PetTownForestLibrary>("Environment/Forest/Library"));
}