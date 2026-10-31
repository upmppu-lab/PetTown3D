using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Presentation bridge. Existing placement/payment/navigation/save routines remain the authority.
public partial class PetTownGame
{
    PetTownPetCareWindow petCareWindow;
    bool PetCareOpen=>petCareWindow!=null&&petCareWindow.IsOpen;
    public bool IsPresentationMenuOpen=>CatalogOpen||ExpansionOpen||PetCareOpen||PetPlayOpen;
    public int VegetationRevision=>constructionWorldRevision+PetTownRoadPath.Revision;
    public bool VegetationAllowed(Vector3 point)
    {
        if(ConstructionWater(point.x,point.z))return false;
        if(InsideAnyBuildingFootprint(point.x,point.z,.4f))
        {
            bool pasture=false;foreach(var building in buildings){var ranch=building?building.GetComponent<PetTownRanchView>():null;if(ranch&&ranch.AllowsPastureGrass(point)){pasture=true;break;}}if(!pasture)return false;
        }
        return !PetTownRoadPath.TryNearestHit(point,4,out var road)||Vector2.Distance(new Vector2(point.x,point.z),new Vector2(road.Point.x,road.Point.z))>road.Width*.5f+.25f;
    }
    void OpenPetCare()
    {
        SetMode(Mode.Play);uiShopOpen=false;if(!petCareWindow)petCareWindow=uiRoot.gameObject.AddComponent<PetTownPetCareWindow>();
        var targets=ProgressionPetTargets();
        if(targets.Count<=1||catalogWindow==null||!catalogWindow.Ready){petCareWindow.Show(uiRoot,targets,Message,ProgressionCareContext());return;}
        var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<targets.Count;i++)
        {
            var t=targets[i];var rec=Progression.care.Get(t.id,t.name);
            string kind=rec.speciesKind>=0&&rec.speciesKind<PetTownProgressionState.Species.Length?PetTownProgressionState.Species[rec.speciesKind]:t.species;
            entries.Add(new PetTownCatalogEntry{Key=i,Name=string.IsNullOrEmpty(rec.nickname)?t.name:rec.nickname,Category=kind,PriceLabel=kind+" · "+rec.SexLabel+" · Lv."+rec.petLevel,FootprintLabel=rec.displayOnly?"전시 동물":(rec.Adoptable?"분양 가능":"성장 중")+" · 배부름 "+Mathf.RoundToInt(rec.fullness)+"%",ActionLabel="보기",Thumbnail=t.portrait,TextOnlyDetail=true});
        }
        catalogWindow.Show("내 펫",entries,e=>{if(e.Key<0||e.Key>=targets.Count)return;petCareWindow.SelectIndex(e.Key);petCareWindow.Show(uiRoot,targets,Message,ProgressionCareContext());},()=>{},()=>{});
    }
    PetTownCozyFurniture CozySelected=>PetTownCozyLibrary.Current?.Find(furnitureChoice<6?furnitureChoice:furnitureChoice-6,furnitureChoice>=6);
    bool FurnitureRequiresSurface=>furnitureChoice==14||CozySelected?.onSurface==true;
    bool TryCreateCozyFurniture(int kind,bool detail,Vector3 point,float heightOverride,out GameObject root)
    {
        root=null;var def=PetTownCozyLibrary.Current?.Find(kind,detail);if(def==null||def.prefab==null)return false;
        root=new GameObject(detail?"PetShopDetail_"+kind+"_REPLACEABLE":"Furniture_"+def.title+"_REPLACEABLE");root.transform.position=point;
        var model=Instantiate(def.prefab,root.transform);model.name="Model";model.transform.localPosition=Vector3.zero;float height=heightOverride>0?heightOverride:def.height;model.transform.localScale*=height/def.height;MarkNavMeshIgnore(root);
        if(!furniturePreviewBuilding)
        {
            props.Add(root);RegisterFurniture(root,kind,detail,height);
            if(!(def.height<.08f))
            {
                var solid=root.AddComponent<BoxCollider>();solid.center=new Vector3(0,height*.5f,0);solid.size=new Vector3(def.footprint.x*height/def.height,height,def.footprint.y*height/def.height);
                StartCoroutine(AddFittedFurnitureObstacle(root));
            }
            if(detail&&kind==3)foreach(var child in model.GetComponentsInChildren<Transform>())if(child.name.StartsWith("Swimmer_"))
            {
                VillagerAnim anim=null;var actual=child.Find("Model");if(actual!=null)SetupPetAnimation(actual.gameObject,"pets/fish_1",out anim);
                tankFish.Add(new TankFish{root=child,center=child.position,phase=tankFish.Count*2.1f,animation=anim});
            }
        }
        return true;
    }
    bool OpenCozyFurnitureCatalog()
    {
        var lib=PetTownCozyLibrary.Current;if(lib==null||catalogWindow==null||!catalogWindow.Ready)return false;
        uiShopOpen=false;ClearGhost();var entries=new List<PetTownCatalogEntry>();
        bool houseOwner=FurnitureOwner!=null&&FurnitureOwner.Kind==1; // 집 전용 가구(카테고리가 "집·"으로 시작)는 집(우리 집)에서만 보임
        foreach(var f in lib.furniture)
        {
            if(!FurnitureAllowedFor(f,FurnitureOwner))continue;
            var def=f;entries.Add(new PetTownCatalogEntry{Key=def.detail?6+def.kind:def.kind,Name=def.title,Category=def.category,Price=50,Footprint=PetTownFurniturePlanner.ReservedSize(def.footprint,furnitureGridSize),Thumbnail=def.thumbnail,Description=(def.onSurface?"계산대나 테이블 위에 배치합니다.\n":"실내 0.5m 격자에 맞춰 배치합니다.\n")+"R키로 90° 회전 · 설치 전 공간 검사"});
        }
        catalogWindow.Show("가구 목록",entries,entry=>{furnitureChoice=entry.Key;furnitureQuarter=0;ghostFurnitureFor=-1;furnitureGhostReady=false;furnitureCached=false;uiActionBuiltFor=-999;},()=>SetMode(Mode.Play),()=>{useNewFurnitureCatalog=false;uiActionBuiltFor=-999;});uiActionBuiltFor=-999;return true;
    }
    void FitCozySavedFurniture(GameObject root,PetTownFurnitureSaveRecord record)
    {
        if(PetTownCozyLibrary.Current?.Find(record.kind,record.detail)==null)return;var model=root.transform.Find("Model");if(!model)return;
        var b=new Bounds();bool first=true;foreach(var r in model.GetComponentsInChildren<Renderer>())for(int i=0;i<8;i++){var p=r.bounds.center+Vector3.Scale(r.bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=root.transform.InverseTransformPoint(p);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
        float scale=Mathf.Min(1,Mathf.Min(record.footprint.x/Mathf.Max(.01f,b.size.x),record.footprint.y/Mathf.Max(.01f,b.size.z)));model.localScale=Vector3.Scale(model.localScale,new Vector3(scale,1,scale));var collider=root.GetComponent<BoxCollider>();if(collider){var size=collider.size;size.x=Mathf.Min(size.x,record.footprint.x);size.z=Mathf.Min(size.z,record.footprint.y);collider.size=size;}
    }
    void OpenCozyInformation(int menu)
    {
        if(menu==7){OpenPetCare();return;}
        if(menu==6){OpenTownProgression();return;}
        if(menu==5){OpenProgressionInventory();return;}
        SetMode(Mode.Play);uiShopOpen=false;var entries=new List<PetTownCatalogEntry>();
        if(menu==8)
        {
            entries.Add(new PetTownCatalogEntry{Key=0,Name="마을 저장",Category="설정",PriceLabel="",FootprintLabel="",ActionLabel="저장하기",Description="현재 마을·가구·증축 정보를 저장합니다."});
            entries.Add(new PetTownCatalogEntry{Key=1,Name="지붕 표시",Category="설정",PriceLabel=showRoofs?"표시 중":"숨김",FootprintLabel="",ActionLabel="전환하기",Description="건물 지붕을 표시하거나 실내를 볼 수 있도록 숨깁니다."});
            entries.Add(new PetTownCatalogEntry{Key=2,Name="마을 처음부터 시작",Category="설정",PriceLabel="",FootprintLabel="",ActionLabel="초기화…",Description="저장된 마을, 건물, 펫, 주민, 돈을 모두 지우고 새 마을로 다시 시작합니다.\n누르면 한 번 더 확인해요."});
        }
        else if(menu==6)
        {
            entries.Add(new PetTownCatalogEntry{Key=0,Name="마을 주민",Category="주민",PriceLabel=population+"명",FootprintLabel="",ActionLabel="닫기",Description="현재 마을의 주민 수입니다.\n명성: "+reputation+"\n매장 레벨: "+shopLevel});
        }
        else
        {
            int[] stocks={dogStock,catStock,rabbitStock};string[] names={"강아지","고양이","토끼"};for(int i=0;i<3;i++)entries.Add(new PetTownCatalogEntry{Key=i,Name=names[i],Category="펫 재고",PriceLabel=stocks[i]+"마리",FootprintLabel="",ActionLabel="재고 관리",Description="현재 펫샵의 실제 "+names[i]+" 재고입니다.\n입고는 펫샵 관리에서 진행합니다."});
        }
        catalogWindow.Show(menu==8?"설정":menu==6?"주민 정보":menu==5?"아이템 가방":"펫 정보",entries,e=>{if(menu==8){if(e.Key==0)SaveGame();else if(e.Key==2)OpenResetConfirm();else ToggleMobileRoof();}else if(menu!=6)OpenShopManagement();},()=>{},()=>{});
    }
#if UNITY_EDITOR
    // Explicitly requested test-town art staging; never runs in a player build or automatically.
    public IEnumerator StyleCozyTestInterior()
    {
        var owner=FurnitureOwner;if(owner==null||PetTownCozyLibrary.Current==null)yield break;
        owner.transform.rotation=Quaternion.identity;var placed=owner.GetComponent<PetTownBuildingPlacement>();if(placed!=null)placed.Record.facingQuarter=Mathf.RoundToInt(Mathf.Repeat(placed.Record.entranceBaseYaw,360)/90)%4;
        for(int i=props.Count-1;i>=0;i--){var p=props[i];var m=p?p.GetComponent<PetTownFurniturePlacement>():null;if(m==null||m.Owner!=owner)continue;tankFish.RemoveAll(f=>f.root==null||f.root.IsChildOf(p.transform));p.SetActive(false);props.RemoveAt(i);Destroy(p);}owner.Unresolved.Clear();
        var previous=furnishingOwner;furnishingOwner=owner;
        try
        {
            CozyStage(0,false,-3.2f,-4.5f);CozyStage(3,false,-3.2f,-3,2);CozyStage(8,true,-3.2f,-4.5f,0,1.06f);CozyStage(24,true,-4.2f,-4.5f,0,1.06f);CozyStage(28,true,-2.3f,-4.4f,0,1.06f);
            CozyStage(2,false,-6.1f,-.5f,1);CozyStage(9,true,-6.1f,2.3f,1);CozyStage(14,true,-5.9f,5.6f);CozyStage(2,true,-3,5.75f);CozyStage(3,true,0,5.75f);CozyStage(1,true,3,5.7f);CozyStage(5,false,5.9f,5.8f);
            CozyStage(4,true,2,-.5f);CozyStage(7,true,4.8f,.3f);CozyStage(5,true,4.4f,2.5f);CozyStage(6,true,6,3.8f);
            CozyStage(11,true,3.7f,-4.9f,2);CozyStage(12,true,6,-4.8f);CozyStage(19,true,6,-4.8f,0,.71f);CozyStage(13,true,3.7f,-2.9f);CozyStage(28,true,3.7f,-2.9f,0,.51f);CozyStage(18,true,6.1f,-2.5f);CozyStage(23,true,0,-5.6f);
            CozyStage(5,false,-6,-6);CozyStage(5,false,6,-6);
        }
        finally{furnishingOwner=previous;}
        yield return null;yield return null;yield return null;yield return null;RebakeNavMesh();QueueConstructionNavRefresh();yield return null;yield return null;SaveGame();Message("새 펫샵 인테리어를 적용했습니다.");
    }
    void CozyStage(int kind,bool detail,float x,float z,int quarter=0,float lift=0)
    {
        var owner=FurnitureOwner;var p=owner.transform.TransformPoint(new Vector3(x,owner.FloorOffset+lift,z));var root=detail?CreatePetShopDetail(kind,p):CreateFurniture(kind,p);var rotation=owner.transform.rotation*Quaternion.Euler(0,quarter*90,0);foreach(var fish in tankFish)if(fish.root&&fish.root.IsChildOf(root.transform)){fish.center=p+rotation*(fish.center-p);fish.orientation=rotation;}root.transform.SetParent(owner.transform,true);root.transform.rotation=rotation;var metadata=root.GetComponent<PetTownFurniturePlacement>();metadata.IsDefault=false;
    }
#endif
}
