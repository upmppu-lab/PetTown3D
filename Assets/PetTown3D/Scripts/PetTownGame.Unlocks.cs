using System.Collections.Generic;

public partial class PetTownGame
{
    void SeasonAndFestival()
    {
        if(loadingTown||!shop)return;var tw=TimeWeather;if(tw.noticeDay>=day)return;tw.noticeDay=day;progressionDirty=true;
        int season=PetTownFarmState.SeasonOf(day);
        if(day>1&&season!=PetTownFarmState.SeasonOf(day-1))Message(PetTownFarmState.SeasonNames[season]+"이 시작됐어요! 제철 작물: "+PetTownFarmState.SeasonCrops(day));
        if(day>=14&&day%14==0&&tw.festivalDay<day)
        {
            tw.festivalDay=day;int reward=150+20*Progression.playerLevel;money+=reward;
            foreach(var p in EnsureResidents().people)p.happiness=UnityEngine.Mathf.Min(100,p.happiness+3);
            SaveProgressionSilent();Message("마을 축제 날이에요! 모든 주민 행복도 +3 · 축제 지원금 "+reward+" 골드");
        }
    }
    void OpenUnlockTable()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();int level=Progression.playerLevel;var rows=PetTownUnlocks.Table();
        for(int i=0;i<rows.Length;i++)
        {
            var r=rows[i];bool open=level>=r.level;
            entries.Add(OperationsEntry(i,"Lv."+r.level+" · "+r.title,open?"해금됨":(r.level-level)+"레벨 더 필요","내 레벨 Lv."+level,open?"확인":"잠김",r.detail,5));
        }
        {var civic=new System.Text.StringBuilder();int pop=EnsureResidents().Population;foreach(var d in PetTownDecorDefs.All)if(d.unlock>0)civic.AppendLine((pop>=d.unlock?"✓ ":"· ")+d.name+" — 주민 "+d.unlock+"명");entries.Add(OperationsEntry(900,"상점·공공건물 해금","주민 "+pop+"명","주민 수가 늘면 새 건물이 열려요","확인",civic.ToString(),5));}
catalogWindow.Show("레벨 해금표",entries,e=>OpenDailyQuests(),()=>{},()=>{});
    }
}
