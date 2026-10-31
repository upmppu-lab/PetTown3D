using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownPetCareRecord
{
    public string id,nickname;
    public int sex,generation,stars,gene;public string parentA,parentB,breedingId;public float breedingCooldown;
    public static readonly string[] GeneNames={"","금빛 유전자","은빛 유전자","무지개 유전자"};
    public string GradeLabel=>new string('★',Mathf.Clamp(stars,1,5))+(gene>0&&gene<GeneNames.Length?" · "+GeneNames[gene]:"");
    public float GradeMultiplier=>(1+.15f*(Mathf.Clamp(stars,1,5)-1))*(gene>0?1.5f:1);
    public bool IsBreeding=>!string.IsNullOrEmpty(breedingId);
    public string SexLabel=>sex==1?"수컷 ♂":sex==2?"암컷 ♀":"성별 미등록";
    public void EnsureSex(){if(sex==1||sex==2)return;if(string.IsNullOrEmpty(id))return;uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);sex=1+(int)(hash&1);}
    public float fullness=100,cleanliness=100,stress,boredom,growth=1;
    public int affection,petLevel=1,experience,speciesKind=-1,appearance;
    public bool displayOnly,favorite,groomed;public string slotId;
    [NonSerialized] public bool busy;
    public const int AdultLevel=3;
    public int growthRulesVersion;
    public float growthExperienceClock;
    public static float SizeAtLevel(int level)=>Mathf.Lerp(.45f,1,Mathf.Clamp01((level-1)/(float)(AdultLevel-1)));
    public string LifeStage=>Level<2?"아기":Level<AdultLevel?"어린 개체":"성체";
    public int Level=>Mathf.Clamp(petLevel,1,10);
    public int ExperienceNeeded=>20+15*Level;
    public float Condition=>(fullness+cleanliness+100-stress+100-boredom)*.25f;
    public bool Adoptable=>!displayOnly&&!favorite&&!busy&&!IsBreeding&&Level>=AdultLevel;
    public void Normalize(bool legacy=false)
    {
        EnsureSex();stars=Mathf.Clamp(stars<=0?1:stars,1,5);gene=Mathf.Clamp(gene,0,3);if(displayOnly||slotId==null)slotId="";generation=Mathf.Max(0,generation);breedingCooldown=Mathf.Max(0,breedingCooldown);
        if(legacy)petLevel=Mathf.Clamp(1+affection/100,1,10);
        if(growthRulesVersion<1){if(growth>=.999f)petLevel=Mathf.Max(AdultLevel,petLevel);else if(growth>=.5f)petLevel=Mathf.Max(2,petLevel);growthRulesVersion=1;}
        petLevel=Mathf.Clamp(petLevel,1,10);growthExperienceClock=float.IsFinite(growthExperienceClock)?Mathf.Clamp(growthExperienceClock,0,3):0;experience=Mathf.Max(0,experience);
        fullness=Mathf.Clamp(fullness,0,100);cleanliness=Mathf.Clamp(cleanliness,0,100);stress=Mathf.Clamp(stress,0,100);boredom=Mathf.Clamp(boredom,0,100);growth=Mathf.Clamp01((Level-1)/(float)(AdultLevel-1));
    }
    public void AddExperience(int value)
    {
        if(value<=0||Level>=10)return;experience+=value;
        while(petLevel<10&&experience>=ExperienceNeeded){experience-=ExperienceNeeded;petLevel++;}
        if(petLevel>=10)experience=0;growth=Mathf.Clamp01((Level-1)/(float)(AdultLevel-1));
    }
    public static readonly string[] TraitNames={"장난꾸러기","겁쟁이","식탐왕","애교쟁이","잠꾸러기","호기심쟁이","얌전이","활발이"};
    static readonly float[][] TraitRates={new[]{1f,1.5f,1f},new[]{1f,.9f,1.3f},new[]{1.6f,1f,1f},new[]{1f,1.1f,.9f},new[]{.9f,.6f,.8f},new[]{1f,1.3f,1f},new[]{.9f,.8f,.7f},new[]{1.2f,1.4f,1.1f}};
    public int TraitIndex{get{if(string.IsNullOrEmpty(id))return 6;uint h=2166136261;foreach(char c in id)h=unchecked((h^c)*16777619);h=unchecked(h*2246822519u);return (int)((h>>8)%(uint)TraitNames.Length);}}
    public string TraitLabel=>TraitNames[TraitIndex];
    public void Tick(float seconds,bool night=false)
    {
        if(seconds<=0)return;breedingCooldown=Mathf.Max(0,breedingCooldown-seconds);float rate=seconds/60*.35f*(night?.4f:1f);var tr=TraitRates[TraitIndex];
        fullness=Mathf.Clamp(fullness-rate*3.5f*tr[0],0,100);cleanliness=Mathf.Clamp(cleanliness-rate*2.2f,0,100);
        float ds=rate*tr[2];if(fullness<25)ds+=rate*1.5f;if(night)ds-=seconds/60*2f;
        stress=Mathf.Clamp(stress+ds,0,100);boredom=Mathf.Clamp(boredom+rate*3*tr[1],0,100);
        float speed=Condition>=60?1:Condition>=35 ? .5f:.15f;
        if(!displayOnly&&Level<AdultLevel){growthExperienceClock+=seconds*speed;while(growthExperienceClock>=3&&Level<AdultLevel){growthExperienceClock-=3;AddExperience(1);}if(Level>=AdultLevel)growthExperienceClock=0;}
        growth=Mathf.Clamp01((Level-1)/(float)(AdultLevel-1));
    }
    public bool NeedsCare(int action)=>action==7?!groomed:action==0?fullness<90:action==1?cleanliness<90:action==2?cleanliness<90||stress>10:action==3?boredom>10:stress>10||boredom>10;
    public void Care(int action)
    {
        if(action<0||action>6)return;
        bool gain=NeedsCare(action);
        if(action==0)fullness=Mathf.Min(100,fullness+55);
        else if(action==1)cleanliness=100;
        else if(action==2){cleanliness=Mathf.Min(100,cleanliness+20);stress=Mathf.Max(0,stress-15);}
        else if(action==3)boredom=Mathf.Max(0,boredom-35);
        else {stress=Mathf.Max(0,stress-25);boredom=Mathf.Max(0,boredom-15);}
        if(gain){affection++;AddExperience(action==4||action==6?1:2);}
    }
}

[Serializable]
public sealed class PetTownPetCareState
{
    public int version=2;public List<PetTownPetCareRecord> pets=new List<PetTownPetCareRecord>();
    const string Key="PetTown3D_PetCare_v1";
    public static PetTownPetCareState Load()
    {
        try{var value=JsonUtility.FromJson<PetTownPetCareState>(PlayerPrefs.GetString(Key,""));if(value!=null&&(value.version==1||value.version==2)&&value.pets!=null){foreach(var p in value.pets)if(p!=null)p.Normalize(value.version==1);value.version=2;return value;}}catch(ArgumentException){}
        return new PetTownPetCareState();
    }
    public PetTownPetCareRecord Get(string id,string defaultName)
    {
        var pet=pets.Find(x=>x!=null&&x.id==id);if(pet==null){pet=new PetTownPetCareRecord{id=id,nickname=defaultName,petLevel=PetTownPetCareRecord.AdultLevel,growthRulesVersion=1};pets.Add(pet);}return pet;
    }
    public void Save(){PlayerPrefs.SetString(Key,JsonUtility.ToJson(this));PlayerPrefs.Save();}
}
public sealed class PetTownPetCareTarget
{
    public string id,name,species;public GameObject root;public Texture2D portrait;public Rect portraitUv=new Rect(0,0,1,1);public bool saleAvailable;
}
public sealed class PetTownPetCareContext
{
    public PetTownPetCareState state;
    public Func<PetTownPetCareRecord,int,string> care;
    public Func<PetTownPetCareRecord,string> food;
    public Action<PetTownPetCareRecord> buyFood,toggleFavorite,play;
    public Action save;
    public Func<PetTownPetCareRecord,string> slot;
}