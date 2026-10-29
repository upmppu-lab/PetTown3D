// One place that lists which player level opens which feature. The values come from the systems themselves.
public static class PetTownUnlocks
{
    public struct Row{public int level;public string title,detail;}
    public static int FarmExpandLevel(int expansion)=>expansion<=0?3:6;
    public static Row[] Table()
    {
        return new[]
        {
            new Row{level=1,title="기본 운영",detail="펫샵·농장·목장·카페·병원 기본 기능"},
            new Row{level=PetTownFarmUnlock(0),title="밭 넓히기 1단계",detail="밭 9칸 → 11칸"},
            new Row{level=PetTownCafeRecipes.UnlockLevel(3),title=PetTownCafeRecipes.Name(3)+" 메뉴",detail=PetTownCafeRecipes.Needs(3)},
            new Row{level=PetTownCommunityState.KeeperLevel,title="동물원 전담 사육사",detail="매일 먹이 주기와 청소를 대신 해요"},
            new Row{level=PetTownClinic.UnlockLevel(2),title="병원 "+PetTownClinic.ServiceName(2),detail=PetTownClinic.ServiceNote(2)},
            new Row{level=PetTownBreedingState.AutoLevel,title="펫 자동 교배",detail="등급이 높은 짝을 자동으로 이어줘요"},
            new Row{level=PetTownCafeRecipes.UnlockLevel(4),title=PetTownCafeRecipes.Name(4)+" 메뉴",detail=PetTownCafeRecipes.Needs(4)},
            new Row{level=PetTownClinic.UnlockLevel(3),title="병원 "+PetTownClinic.ServiceName(3),detail=PetTownClinic.ServiceNote(3)},
            new Row{level=PetTownFarmUnlock(1),title="밭 넓히기 2단계",detail="밭 11칸 → 12칸"}
        };
    }
    static int PetTownFarmUnlock(int expansion)=>FarmExpandLevel(expansion);
}
