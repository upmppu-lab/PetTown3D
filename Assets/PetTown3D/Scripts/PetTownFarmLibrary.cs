using UnityEngine;

// References the user's imported prefabs; source packs and their materials remain unchanged.
public sealed class PetTownFarmLibrary : ScriptableObject
{
    public GameObject soil,cabbage,tomato,fence,barn,crate,wateringCan;
    public GameObject carrot,corn,wheat,pumpkin,potato;
    public Texture2D thumbnail;public Texture2D[] cropIcons=new Texture2D[PetTownFarmState.CropCount];
    public Texture2D CropIcon(int kind)=>cropIcons!=null&&kind>=0&&kind<cropIcons.Length?cropIcons[kind]:null;
    public bool Ready=>soil&&cabbage&&tomato&&fence&&barn&&crate&&wateringCan;
    public GameObject Crop(int kind)=>kind==0?cabbage:kind==1?tomato:kind==2?carrot:kind==3?corn:kind==4?wheat:kind==5?pumpkin:kind==6?potato:null;
    public static Vector3 CropSize(int kind)=>kind==0?new Vector3(.8f,.55f,.8f):kind==1?new Vector3(.75f,1.05f,.75f):kind==2?new Vector3(.75f,.65f,.75f):kind==3?new Vector3(.8f,1.55f,.8f):kind==4?new Vector3(.8f,1.15f,.8f):kind==5?new Vector3(.95f,.55f,.95f):new Vector3(.8f,.65f,.8f);
    static PetTownFarmLibrary cached;
    public static PetTownFarmLibrary Current=>cached?cached:(cached=Resources.Load<PetTownFarmLibrary>("Environment/Farm/Library"));
}
