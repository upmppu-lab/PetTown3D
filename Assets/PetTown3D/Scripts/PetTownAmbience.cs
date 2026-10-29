using UnityEngine;

// Procedurally generated ambience (no audio assets): rain hiss, winter wind, night crickets and daytime bird chirps.
// Everything is kept quiet and fades with the weather, season and time of day.
public sealed class PetTownAmbience : MonoBehaviour
{
    const int Rate=22050;
    PetTownGame game;AudioSource rainSource,windSource,nightSource,chirpSource;AudioClip rainClip,windClip,nightClip,chirpClip;
    float rainV,windV,nightV,nextChirp;
    public void Initialize(PetTownGame source)
    {
        game=source;
        rainClip=Noise("PetTownRain",3f,.55f,0,0);windClip=Noise("PetTownWind",3f,.04f,1,1);nightClip=Crickets();chirpClip=Chirp();
        rainSource=Source(rainClip,true);windSource=Source(windClip,true);nightSource=Source(nightClip,true);chirpSource=Source(chirpClip,false);
        nextChirp=Time.unscaledTime+4;
    }
    AudioSource Source(AudioClip clip,bool loop){var s=gameObject.AddComponent<AudioSource>();s.clip=clip;s.loop=loop;s.playOnAwake=false;s.spatialBlend=0;s.volume=0;return s;}
    static AudioClip Noise(string name,float seconds,float smooth,int modulation,int cycles)
    {
        int n=Mathf.RoundToInt(seconds*Rate);var data=new float[n];var rng=new System.Random(name.GetHashCode());float last=0;
        for(int i=0;i<n;i++)
        {
            float white=(float)(rng.NextDouble()*2-1);last+=(white-last)*smooth;float amp=modulation==1?.65f+.35f*Mathf.Sin(2*Mathf.PI*cycles*i/n):1;data[i]=last*amp*(modulation==1?2.4f:.8f);
        }
        var clip=AudioClip.Create(name,n,1,Rate,false);clip.SetData(data,0);return clip;
    }
    static AudioClip Crickets()
    {
        int n=Rate*2;var data=new float[n];
        for(int i=0;i<n;i++){float t=(float)i/Rate;float gate=(t*3%1)<.45f?1:0;float pulse=.5f+.5f*Mathf.Sin(2*Mathf.PI*40*t);data[i]=Mathf.Sin(2*Mathf.PI*4300*t)*pulse*gate*.5f;}
        var clip=AudioClip.Create("PetTownCrickets",n,1,Rate,false);clip.SetData(data,0);return clip;
    }
    static AudioClip Chirp()
    {
        int n=Mathf.RoundToInt(.22f*Rate);var data=new float[n];float phase=0;
        for(int i=0;i<n;i++){float t=(float)i/n;float f=Mathf.Lerp(2800,4400,t)+Mathf.Sin(t*38)*220;phase+=2*Mathf.PI*f/Rate;data[i]=Mathf.Sin(phase)*Mathf.Sin(Mathf.PI*t)*.6f;}
        var clip=AudioClip.Create("PetTownChirp",n,1,Rate,false);clip.SetData(data,0);return clip;
    }
    void Update()
    {
        if(!game||!Application.isFocused)return;
        var state=game.TimeWeather;int season=PetTownFarmState.SeasonOf(game.CurrentFarmDay);bool night=state.Night;
        float rainT=state.weather==PetTownWeather.Rain?.16f:0;
        float windT=state.weather==PetTownWeather.Snow?.14f:season==3?.05f:0;
        float nightT=night&&state.weather!=PetTownWeather.Rain&&(season==1||season==2)?.05f:0;
        float dt=Time.unscaledDeltaTime*.2f;
        rainV=Mathf.MoveTowards(rainV,rainT,dt);windV=Mathf.MoveTowards(windV,windT,dt);nightV=Mathf.MoveTowards(nightV,nightT,dt);
        Apply(rainSource,rainV);Apply(windSource,windV);Apply(nightSource,nightV);
        if(!night&&state.weather==PetTownWeather.Clear&&(season==0||season==1)&&Time.unscaledTime>=nextChirp)
        {
            chirpSource.pitch=Random.Range(.85f,1.25f);chirpSource.PlayOneShot(chirpClip,.07f);nextChirp=Time.unscaledTime+Random.Range(4f,10f);
        }
    }
    static void Apply(AudioSource s,float v){s.volume=v;if(v>.001f&&!s.isPlaying)s.Play();else if(v<=.001f&&s.isPlaying)s.Stop();}
    void OnDestroy(){if(rainClip)Destroy(rainClip);if(windClip)Destroy(windClip);if(nightClip)Destroy(nightClip);if(chirpClip)Destroy(chirpClip);}
}
