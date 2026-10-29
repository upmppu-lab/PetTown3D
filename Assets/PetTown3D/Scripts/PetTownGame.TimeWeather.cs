using UnityEngine;

public partial class PetTownGame
{
    int lastRainDay=-1,lastRainRevision=-1;
    public PetTownTimeWeatherState TimeWeather=>Progression.Environment(day);
    public string GameClockText=>"Day "+day+" · "+TimeWeather.ClockText;
    bool AdvanceWorldTime(float seconds)
    {
        if(!Application.isFocused||seconds<=0)return false;ApplyRainWatering();bool changed=TimeWeather.Advance(seconds);dayTimer=TimeWeather.elapsed;progressionDirty=true;return changed;
    }
    void BeginWorldDay(){TimeWeather.SelectWeather(day);ProgressionNewDay();DecorNewDay();ApplyRainWatering();SeasonAndFestival();}
    void ApplyRainWatering()
    {
        if(TimeWeather.weather!=PetTownWeather.Rain||Progression.farm==null)return;var farm=Progression.farm;
        if(lastRainDay==day&&lastRainRevision==farm.revision)return;bool changed=false;
        foreach(var view in FarmRoots())
        {
            var lot=farm.lots.Find(l=>l.id==view.Id);if(lot==null)continue;for(int i=0;i<lot.plots.Count;i++)changed|=farm.Water(lot,i,day);
        }
        lastRainDay=day;lastRainRevision=farm.revision;if(changed)progressionDirty=true;
    }
}
