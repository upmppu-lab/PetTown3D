using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PetTownEmployeeRole
{
    public string id,title;public int level,hire,wage;
    public static readonly PetTownEmployeeRole[] All={
        new PetTownEmployeeRole{id="clean",title="청결 담당",level=1,hire=250,wage=30},
        new PetTownEmployeeRole{id="porter",title="배송 담당",level=1,hire=300,wage=35},
        new PetTownEmployeeRole{id="cashier",title="계산 담당",level=2,hire=500,wage=45},
        new PetTownEmployeeRole{id="care",title="돌봄 담당",level=3,hire=600,wage=50},
        new PetTownEmployeeRole{id="groomer",title="미용 담당",level=5,hire=1500,wage=80},
        new PetTownEmployeeRole{id="chef",title="카페 요리사",level=4,hire=1200,wage=70},
        new PetTownEmployeeRole{id="vet",title="수의사",level=6,hire=2000,wage=100},
        new PetTownEmployeeRole{id="farmer",title="농장 관리사",level=4,hire=1000,wage=65},
        new PetTownEmployeeRole{id="rancher",title="목장 관리사",level=5,hire=1300,wage=75}};
    public static PetTownEmployeeRole Find(string id)=>Array.Find(All,r=>r.id==id);
}
[Serializable] public sealed class PetTownEmployeeRecord
{
    public string id,name,role,facilityId;public int level=1,appearance,completed,unpaid;public bool paused;
}
[Serializable] public sealed class PetTownDeliveryRecord
{
    public string id;public int kind,quantity,paid;public float remaining;
    public int species=-1,sex,appearance;
    public bool IsPet=>species>=0;
    public bool Arrived=>remaining<=0;
}
[Serializable] public sealed class PetTownPayrollRecord
{
    public int day,due,paid,arrears;
}
[Serializable] public sealed class PetTownActivityCounter {public string key;public int amount;}

[Serializable]
public sealed class PetTownShopOperationsState
{
    public int version=1,kits,sequence,lastPayrollDay;
    public PetTownDailyQuestState dailyQuests;
    public string groomingStationId;
    public List<PetTownEmployeeRecord> employees=new List<PetTownEmployeeRecord>();
    public List<PetTownDeliveryRecord> deliveries=new List<PetTownDeliveryRecord>();
    public List<PetTownPayrollRecord> payroll=new List<PetTownPayrollRecord>();
    public List<PetTownActivityCounter> activities=new List<PetTownActivityCounter>();
    public List<string> claimedQuests=new List<string>();
    public bool[] discovered=new bool[PetTownProgressionState.Species.Length];
    static readonly float[] Durations={0,3,2.4f,2,1.6f,1.3f,1.1f,.9f,.75f,.6f,.4f};
    static readonly float[] Wages={0,1,1.6f,2.4f,3.4f,4.6f,5.6f,6.7f,7.9f,9.2f,10.6f};
    static readonly float[] Upgrades={0,0,1.5f,3,6,10,15,21,28,36,45};
    public static int Round(float value)=>Mathf.FloorToInt(value+.5f);
    public static int HirePrice(PetTownEmployeeRole role,int existing)=>Round(role.hire*(1+existing*.6f)/10)*10;
    public static int Wage(PetTownEmployeeRecord p){var role=PetTownEmployeeRole.Find(p.role);return role==null?0:Round(role.wage*3*Wages[Mathf.Clamp(p.level,1,10)]/5)*5;}
    public static int UpgradePrice(PetTownEmployeeRecord p){var role=PetTownEmployeeRole.Find(p.role);return role==null||p.level>=10?0:Round(role.hire*Upgrades[Mathf.Clamp(p.level+1,2,10)]/10)*10;}
    public int Arrears {get{int n=0;foreach(var p in employees)n+=p.unpaid;return n;}}
    public int DailyWages {get{int n=0;foreach(var p in employees)if(!p.paused)n+=Wage(p);return n;}}
    public int Activity(string key){var c=activities.Find(x=>x.key==key);return c==null?0:c.amount;}
    public void Record(string key,int amount=1){if(amount<=0)return;var c=activities.Find(x=>x.key==key);if(c==null){c=new PetTownActivityCounter{key=key};activities.Add(c);}c.amount+=amount;}
    public void Normalize()
    {
        if(dailyQuests!=null)dailyQuests.Normalize();
        kits=Mathf.Max(0,kits);if(employees==null)employees=new List<PetTownEmployeeRecord>();employees.RemoveAll(x=>x==null);
        var ids=new HashSet<string>();foreach(var p in employees){if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id)){p.id=Guid.NewGuid().ToString("N");ids.Add(p.id);}p.level=Mathf.Clamp(p.level,1,10);p.unpaid=Mathf.Max(0,p.unpaid);p.completed=Mathf.Max(0,p.completed);p.appearance=Mathf.Max(0,p.appearance);}
        if(deliveries==null)deliveries=new List<PetTownDeliveryRecord>();deliveries.RemoveAll(x=>x==null);
        foreach(var p in deliveries){p.remaining=float.IsNaN(p.remaining)||float.IsInfinity(p.remaining)?30:Mathf.Max(0,p.remaining);}
        if(payroll==null)payroll=new List<PetTownPayrollRecord>();if(payroll.Count>50)payroll.RemoveRange(50,payroll.Count-50);
        if(activities==null)activities=new List<PetTownActivityCounter>();activities.RemoveAll(x=>x==null);if(claimedQuests==null)claimedQuests=new List<string>();
        if(discovered==null||discovered.Length!=PetTownProgressionState.Species.Length){var old=discovered;discovered=new bool[PetTownProgressionState.Species.Length];if(old!=null)Array.Copy(old,discovered,Math.Min(discovered.Length,old.Length));}
    }
    public bool Hire(string roleId,int playerLevel,bool stationReady,ref int money,out string reason)
    {
        var role=PetTownEmployeeRole.Find(roleId);reason="";if(role==null)return false;
        if(playerLevel<role.level){reason="사장 레벨 "+role.level+"부터 고용할 수 있어요.";return false;}
        if(role.id=="groomer"&&!stationReady){reason="먼저 미용 작업대를 지정해 주세요.";return false;}
        int count=employees.FindAll(p=>p.role==roleId).Count;if(count>=5){reason="같은 담당은 최대 5명입니다.";return false;}
        int price=HirePrice(role,count);if(money<price){reason="고용 골드가 부족합니다.";return false;}
        money-=price;sequence++;employees.Add(new PetTownEmployeeRecord{id=Guid.NewGuid().ToString("N"),role=roleId,name=role.title+" "+sequence,appearance=(sequence-1)%5});Record("hire");return true;
    }
    public bool Upgrade(string id,ref int money,out string reason)
    {
        reason="";var p=employees.Find(x=>x.id==id);if(p==null||p.level>=10){reason="더 이상 교육할 수 없어요.";return false;}
        if(p.unpaid>0){reason="미지급 급여를 먼저 정산해 주세요.";return false;}int cost=UpgradePrice(p);
        if(money<cost){reason="교육 골드가 부족합니다.";return false;}money-=cost;p.level++;return true;
    }
    public bool Fire(string id,out string reason)
    {
        reason="";var p=employees.Find(x=>x.id==id);if(p==null)return false;if(p.unpaid>0){reason="미지급 급여를 먼저 정산해 주세요.";return false;}employees.Remove(p);return true;
    }
    public int PayArrears(ref int money)
    {
        int total=0;foreach(var p in employees){int paid=Mathf.Min(Mathf.Max(0,money),p.unpaid);money-=paid;p.unpaid-=paid;total+=paid;}return total;
    }
    public PetTownPayrollRecord NewDay(int day,ref int money)
    {
        if(day<=lastPayrollDay)return null;lastPayrollDay=day;int due=0;
        foreach(var p in employees)if(!p.paused){int wage=Wage(p);p.unpaid+=wage;due+=wage;}
        int paid=PayArrears(ref money);var record=new PetTownPayrollRecord{day=day,due=due,paid=paid,arrears=Arrears};payroll.Insert(0,record);if(payroll.Count>50)payroll.RemoveRange(50,payroll.Count-50);return record;
    }
    public static int ProductPrice(int kind)=>kind==4?150:kind>=0&&kind<4?PetTownProgressionState.FoodPrice[kind]*5:0;
    public static int ProductQuantity(int kind)=>kind==4?3:5;
    public static int DeliveryPrice(int kind){int cost=ProductPrice(kind);return cost<=0?0:cost+Mathf.Max(5,Round(cost*.15f));}
    public bool Order(int kind,ref int money,out string reason)
    {
        reason="";if(kind<0||kind>4)return false;if(deliveries.Count>=20){reason="배송함을 먼저 비워 주세요.";return false;}
        int cost=DeliveryPrice(kind);if(money<cost){reason="주문 골드가 부족합니다.";return false;}money-=cost;
        deliveries.Add(new PetTownDeliveryRecord{id=Guid.NewGuid().ToString("N"),kind=kind,quantity=ProductQuantity(kind),paid=cost,remaining=30});return true;
    }
    public void Tick(float seconds){if(seconds<=0)return;foreach(var p in deliveries)p.remaining=Mathf.Max(0,p.remaining-seconds);}
    public const float PetDeliverySeconds=45f;
    public bool OrderPet(int species,int sex,int appearance,int cost,ref int money,out string reason)
    {
        reason="";if(species<0||species>=PetTownProgressionState.Species.Length||sex<1||sex>2)return false;
        if(deliveries.Count>=20){reason="배송함을 먼저 비워 주세요.";return false;}
        if(money<cost){reason="주문 골드가 부족합니다.";return false;}
        money-=cost;deliveries.Add(new PetTownDeliveryRecord{id=Guid.NewGuid().ToString("N"),kind=100,quantity=1,paid=cost,remaining=PetDeliverySeconds,species=species,sex=sex,appearance=appearance});return true;
    }
    public bool CollectPet(string id,PetTownProgressionState data,out PetTownPetCareRecord pet)
    {
        pet=null;var p=deliveries.Find(x=>x.id==id);if(p==null||!p.Arrived||!p.IsPet)return false;
        pet=data.AddChosenPet(p.species,p.sex,p.appearance);deliveries.Remove(p);Record("delivery");return true;
    }
    public bool Collect(string id,PetTownProgressionState data)
    {
        var p=deliveries.Find(x=>x.id==id);if(p==null||!p.Arrived||p.kind<0||p.kind>4||p.quantity<=0)return false;
        if(p.kind==4)kits+=p.quantity;else data.food[p.kind]+=p.quantity;deliveries.Remove(p);Record("delivery");return true;
    }
    public bool Groom(PetTownPetCareRecord p,bool stationReady,out string reason)
    {
        reason="";if(p==null||p.speciesKind<0||p.speciesKind>=PetTownProgressionState.Species.Length){reason="이 펫은 미용할 수 없어요.";return false;}
        if(p.groomed){reason="이미 미용을 마친 펫입니다.";return false;}if(p.growth<1){reason="성장이 끝난 뒤 미용할 수 있어요.";return false;}
        if(!stationReady){reason="펫샵 관리에서 미용 작업대를 지정해 주세요.";return false;}if(kits<=0){reason="미용 키트가 없어요. 가방에서 구매해 주세요.";return false;}
        kits--;p.groomed=true;p.cleanliness=100;p.stress=Mathf.Max(0,p.stress-10);p.boredom=Mathf.Max(0,p.boredom-10);Record("groom");return true;
    }
    public static float WorkDuration(int level)=>Durations[Mathf.Clamp(level,1,10)];
}
