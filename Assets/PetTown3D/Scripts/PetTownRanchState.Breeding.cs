using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownRanchPregnancy
{
    public string id,lotId,motherId,fatherId,motherName,fatherName;public int species,generation,remainingDays=2,startedDay;
}
[Serializable] public sealed class PetTownRanchBirth
{
    public string id,lotId,childId,childName,motherId,fatherId,motherName,fatherName;public int species,generation,sex,day;
}
public sealed partial class PetTownRanchState
{
    public int animalSequence;
    public List<PetTownRanchPregnancy> pregnancies=new List<PetTownRanchPregnancy>();
    public List<PetTownRanchBirth> births=new List<PetTownRanchBirth>();
    void EnsureBreedingLists(){if(pregnancies==null)pregnancies=new List<PetTownRanchPregnancy>();if(births==null)births=new List<PetTownRanchBirth>();}
    public int Reserved(PetTownRanchLot lot,int species){EnsureBreedingLists();return lot==null?0:pregnancies.FindAll(x=>x.lotId==lot.id&&x.species==species).Count;}
    public bool BreedingLocked(PetTownRanchLot lot,string id){EnsureBreedingLists();return lot!=null&&pregnancies.Exists(x=>x.lotId==lot.id&&(x.motherId==id||x.fatherId==id));}
    string NextAnimalName(PetTownRanchLot lot,int species)
    {
        string name;do{animalSequence++;name=Species[species]+" "+animalSequence;}while(lot.animals.Exists(a=>a.name==name));return name;
    }
    public bool BuyAnimalOfSex(PetTownRanchLot lot,int species,int sex,int day,ref int gold)
    {
        if(sex!=1&&sex!=2)return false;if(!BuyAnimal(lot,species,day,ref gold))return false;lot.animals[lot.animals.Count-1].sex=sex;return true;
    }
    static bool SameParent(string first,string second)=>!string.IsNullOrEmpty(first)&&first==second;
    public static bool Related(PetTownRanchAnimal a,PetTownRanchAnimal b)
    {
        if(a==null||b==null)return false;return a.id==b.parentA||a.id==b.parentB||b.id==a.parentA||b.id==a.parentB||SameParent(a.parentA,b.parentA)||SameParent(a.parentA,b.parentB)||SameParent(a.parentB,b.parentA)||SameParent(a.parentB,b.parentB);
    }
    public bool CanBreedAnimal(PetTownRanchLot lot,PetTownRanchAnimal animal,int day,out string reason)
    {
        reason="";if(!Owns(lot)||animal==null||!lot.animals.Contains(animal)||day<lot.lastDay){reason="목장과 개체를 다시 확인해 주세요.";return false;}
        animal.EnsureSex();if(!animal.Adult){reason="성체가 된 뒤 함께할 수 있어요.";return false;}
        if(BreedingLocked(lot,animal.id)){reason="이미 번식을 진행 중인 부모예요.";return false;}
        if(animal.breedRestDays>0){reason="번식 휴식 "+animal.breedRestDays+"일이 남았어요.";return false;}
        if(lot.cleanliness<60||animal.fedDay!=day||animal.wateredDay!=day){reason="오늘 먹이·물과 목장 청결60 이상이 필요해요.";return false;}
        return true;
    }
    public bool StartBreeding(PetTownRanchLot lot,string first,string second,int day,out string reason)
    {
        reason="";if(!Owns(lot)){reason="실제 목장을 선택해 주세요.";return false;}var a=lot.animals.Find(x=>x.id==first);var b=lot.animals.Find(x=>x.id==second);
        if(a==null||b==null||a==b||a.species!=b.species){reason="같은 종류의 서로 다른 성체를 선택해 주세요.";return false;}
        if(!CanBreedAnimal(lot,a,day,out reason)||!CanBreedAnimal(lot,b,day,out reason))return false;
        if(a.sex==b.sex){reason="암컷과 수컷을 한 마리씩 선택해 주세요.";return false;}
        if(Related(a,b)){reason="부모·자식 또는 같은 부모의 자식끼리는 짝지을 수 없어요.";return false;}
        if(lot.animals.FindAll(x=>x.species==a.species).Count+Reserved(lot,a.species)>=Capacity[a.species]){reason="새끼1마리를 위한 같은 종류의 빈자리가 필요해요.";return false;}
        var mother=a.sex==2?a:b;var father=a.sex==1?a:b;
        pregnancies.Add(new PetTownRanchPregnancy{id=Guid.NewGuid().ToString("N"),lotId=lot.id,motherId=mother.id,fatherId=father.id,motherName=mother.name,fatherName=father.name,species=a.species,generation=Mathf.Max(a.generation,b.generation)+1,remainingDays=2,startedDay=day});a.breedRestDays=b.breedRestDays=3;revision++;return true;
    }
    public bool CancelBreeding(PetTownRanchLot lot,string id)
    {
        EnsureBreedingLists();if(!Owns(lot))return false;var pregnancy=pregnancies.Find(x=>x.id==id&&x.lotId==lot.id);if(pregnancy==null)return false;pregnancies.Remove(pregnancy);revision++;return true;
    }
    public static int AnimalSellQuote(PetTownRanchAnimal animal)=>animal==null||animal.species<0||animal.species>2?0:Mathf.Max(1,Mathf.RoundToInt(Prices[animal.species]*(animal.Adult?.6f:.3f)));
    public bool SellAnimal(PetTownRanchLot lot,string id,ref int gold,out string reason)
    {
        reason="";if(!Owns(lot)){reason="목장을 다시 선택해 주세요.";return false;}var animal=lot.animals.Find(x=>x.id==id);
        if(animal==null){reason="이미 목장을 떠난 개체예요.";return false;}
        if(BreedingLocked(lot,id)){reason="번식을 진행 중인 부모는 판매할 수 없어요.";return false;}
        if(animal.ready>0){reason="준비된 생산물을 먼저 모아 주세요.";return false;}
        int price=AnimalSellQuote(animal);if((long)gold+price>int.MaxValue){reason="골드 보관 한도에 도달했어요.";return false;}
        lot.animals.Remove(animal);gold+=price;revision++;return true;
    }
    void AdvanceBreedingDay(PetTownRanchLot lot,int day,int previous)
    {
        EnsureBreedingLists();foreach(var animal in lot.animals)animal.breedRestDays=Mathf.Max(0,animal.breedRestDays-1);
        for(int i=pregnancies.Count-1;i>=0;i--)
        {
            var p=pregnancies[i];if(p.lotId!=lot.id)continue;if(births.Exists(x=>x.id==p.id)){pregnancies.RemoveAt(i);continue;}var mother=lot.animals.Find(x=>x.id==p.motherId);var father=lot.animals.Find(x=>x.id==p.fatherId);
            if(mother==null||father==null){pregnancies.RemoveAt(i);continue;}
            if(lot.cleanliness<40||mother.fedDay!=previous||mother.wateredDay!=previous||father.fedDay!=previous||father.wateredDay!=previous||p.startedDay>previous)continue;
            p.remainingDays=Mathf.Max(0,p.remainingDays-1);if(p.remainingDays>0||lot.animals.FindAll(x=>x.species==p.species).Count>=Capacity[p.species])continue;
            var baby=new PetTownRanchAnimal{id=Guid.NewGuid().ToString("N"),name=NextAnimalName(lot,p.species),species=p.species,boughtDay=day,generation=p.generation,parentA=p.motherId,parentB=p.fatherId};baby.sex=PetTownRanchAnimal.StableSex(baby.id);lot.animals.Add(baby);
            births.Insert(0,new PetTownRanchBirth{id=p.id,lotId=lot.id,childId=baby.id,childName=baby.name,motherId=p.motherId,fatherId=p.fatherId,motherName=p.motherName,fatherName=p.fatherName,species=p.species,generation=p.generation,sex=baby.sex,day=day});if(births.Count>30)births.RemoveRange(30,births.Count-30);pregnancies.RemoveAt(i);
        }
    }
    void NormalizeBreeding()
    {
        EnsureBreedingLists();animalSequence=Mathf.Clamp(animalSequence,0,1000000);var ids=new HashSet<string>();var parents=new HashSet<string>();
        for(int i=0;i<pregnancies.Count;)
        {
            var p=pregnancies[i];var lot=p==null?null:lots.Find(x=>x.id==p.lotId);var a=lot==null?null:lot.animals.Find(x=>x.id==p.motherId);var b=lot==null?null:lot.animals.Find(x=>x.id==p.fatherId);
            bool valid=p!=null&&!string.IsNullOrEmpty(p.id)&&lot!=null&&a!=null&&b!=null&&a!=b&&a.Adult&&b.Adult&&a.sex==2&&b.sex==1&&a.species==b.species&&p.species==a.species&&!Related(a,b)&&!ids.Contains(p.id)&&!births.Exists(x=>x!=null&&x.id==p.id)&&!parents.Contains(a.id)&&!parents.Contains(b.id);
            if(valid){int prior=0;for(int j=0;j<i;j++)if(pregnancies[j].lotId==lot.id&&pregnancies[j].species==p.species)prior++;valid=lot.animals.FindAll(x=>x.species==p.species).Count+prior<Capacity[p.species];}
            if(!valid){pregnancies.RemoveAt(i);continue;}ids.Add(p.id);parents.Add(a.id);parents.Add(b.id);p.remainingDays=Mathf.Clamp(p.remainingDays,0,2);p.generation=Mathf.Max(1,p.generation);p.startedDay=Mathf.Max(0,p.startedDay);i++;
        }
        births.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.childId)||x.species<0||x.species>2);if(births.Count>30)births.RemoveRange(30,births.Count-30);
    }
}
