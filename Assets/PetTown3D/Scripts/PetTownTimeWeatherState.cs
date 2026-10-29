using System;
using UnityEngine;

public enum PetTownWeather {Clear,Cloudy,Rain,Snow}
[Serializable] public sealed class PetTownTimeWeatherState
{
    public const float DaySeconds=120;
    public float elapsed=40; // Existing towns begin at08:00 when the optional clock is first added.
    public int festivalDay=-1,noticeDay=-1;public int weatherDay=-1;public PetTownWeather weather=PetTownWeather.Clear;
    public int MinuteOfDay=>Mathf.Clamp(Mathf.FloorToInt(elapsed/DaySeconds*1440),0,1439);
    public float Hour=>elapsed/DaySeconds*24;
    public string ClockText=>(MinuteOfDay/60).ToString("00")+":"+(MinuteOfDay%60).ToString("00");
    public bool Night=>Hour<6||Hour>=19;
    public string WeatherName=>weather==PetTownWeather.Rain?"비":weather==PetTownWeather.Snow?"눈":weather==PetTownWeather.Cloudy?"흐림":"맑음";
    public void Normalize(){if(!float.IsFinite(elapsed))elapsed=40;elapsed=Mathf.Repeat(elapsed,DaySeconds);if((int)weather<0||(int)weather>3)weather=PetTownWeather.Clear;}
    public bool Advance(float seconds)
    {
        if(!float.IsFinite(seconds)||seconds<=0)return false;elapsed+=Mathf.Min(seconds,.5f);if(elapsed<DaySeconds)return false;elapsed-=DaySeconds;return true;
    }
    public void SelectWeather(int day)
    {
        if(day<=weatherDay)return;weatherDay=day;uint seed=unchecked((uint)day*747796405u+2891336453u);seed=(seed^(seed>>16))*2246822519u;int value=(int)(seed%100);
        weather=value<55?PetTownWeather.Clear:value<80?PetTownWeather.Cloudy:PetTownFarmState.SeasonOf(day)==3?PetTownWeather.Snow:PetTownWeather.Rain;
    }
}
