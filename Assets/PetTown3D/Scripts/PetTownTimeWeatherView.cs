using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(80)]
public sealed class PetTownTimeWeatherView : MonoBehaviour
{
    PetTownGame game;Light sun;Quaternion originalRotation;Color originalColor,originalAmbient,originalSky;float originalIntensity,originalShadow;
    readonly Dictionary<Light,float> interior=new Dictionary<Light,float>();
    ParticleSystem rain,snow;Material rainMaterial;PetTownAmbience ambience;float nextLights;bool initialized;
    public ParticleSystem Rain=>rain;
    public float Daylight {get;private set;}
    public void Initialize(PetTownGame source)
    {
        game=source;foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){sun=light;break;}
        if(!sun)return;originalRotation=sun.transform.rotation;originalColor=sun.color;originalIntensity=sun.intensity;originalShadow=sun.shadowStrength;originalAmbient=RenderSettings.ambientLight;if(Camera.main)originalSky=Camera.main.backgroundColor;
        var shader=Resources.Load<Shader>("Environment/Weather/Rain");if(shader)
        {
            var node=new GameObject("PetTown_Rain");node.transform.SetParent(transform,false);rain=node.AddComponent<ParticleSystem>();rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rain.main;main.loop=true;main.playOnAwake=false;main.maxParticles=900;main.startLifetime=1.8f;main.startSpeed=0;main.startSize=.045f;main.startColor=new Color(.68f,.81f,1,.55f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=rain.emission;emission.rateOverTime=300;emission.enabled=false;var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(22,.1f,22);
            var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=-1.2f;velocity.y=-18;velocity.z=.5f;
            var snowNode=new GameObject("PetTown_Snow");snowNode.transform.SetParent(transform,false);snow=snowNode.AddComponent<ParticleSystem>();snow.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm=snow.main;sm.loop=true;sm.playOnAwake=false;sm.maxParticles=700;sm.startLifetime=7f;sm.startSpeed=0;sm.startSize=new ParticleSystem.MinMaxCurve(.07f,.14f);sm.startColor=new Color(1,1,1,.85f);sm.simulationSpace=ParticleSystemSimulationSpace.World;
            var se=snow.emission;se.rateOverTime=90;se.enabled=false;var ssh=snow.shape;ssh.shapeType=ParticleSystemShapeType.Box;ssh.scale=new Vector3(22,.1f,22);
            var sv=snow.velocityOverLifetime;sv.enabled=true;sv.space=ParticleSystemSimulationSpace.World;sv.x=-.4f;sv.y=-2.4f;sv.z=.2f;var sn=snow.noise;sn.enabled=true;sn.strength=.5f;sn.frequency=.4f;
            rainMaterial=new Material(shader){name="PetTown_Rain_Runtime"};var snowRenderer=snow.GetComponent<ParticleSystemRenderer>();snowRenderer.sharedMaterial=rainMaterial;snowRenderer.renderMode=ParticleSystemRenderMode.Billboard;snowRenderer.shadowCastingMode=ShadowCastingMode.Off;snowRenderer.receiveShadows=false;snowRenderer.lightProbeUsage=LightProbeUsage.Off;
            var renderer=rain.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=rainMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.06f;renderer.lengthScale=.7f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
        }
        ambience=gameObject.AddComponent<PetTownAmbience>();ambience.Initialize(game);gameObject.AddComponent<PetTownRoofGlass>().Initialize(game);gameObject.AddComponent<PetTownSpeechDriver>().Initialize(game);
        initialized=true;Apply();
    }
    PetTownInteriorState[] rooms=new PetTownInteriorState[0];float nextRooms;ParticleSystem.Particle[] buffer;
    void LateUpdate(){if(initialized&&game){Apply();Shield(rain);Shield(snow);}}
    // Precipitation never falls inside a building footprint: particles that enter an interior's floor area are removed.
    void Shield(ParticleSystem system)
    {
        if(!system||!system.isPlaying)return;
        if(Time.unscaledTime>=nextRooms){nextRooms=Time.unscaledTime+2;rooms=Object.FindObjectsByType<PetTownInteriorState>(FindObjectsSortMode.None);}
        if(rooms.Length==0)return;
        int max=system.main.maxParticles;if(buffer==null||buffer.Length<max)buffer=new ParticleSystem.Particle[max];
        int count=system.GetParticles(buffer);bool changed=false;
        for(int i=0;i<count;i++)
        {
            var world=buffer[i].position;
            foreach(var room in rooms)
            {
                if(!room)continue;var local=room.transform.InverseTransformPoint(world);var rect=room.LocalBounds;
                if(world.y>room.FloorY-.3f&&local.x>=rect.xMin-.3f&&local.x<=rect.xMax+.3f&&local.z>=rect.yMin-.3f&&local.z<=rect.yMax+.3f){buffer[i].remainingLifetime=0;changed=true;break;}
            }
        }
        if(changed)system.SetParticles(buffer,count);
    }
    public void Apply()
    {
        if(!game||!sun)return;var state=game.TimeWeather;float solar=Mathf.Sin((state.Hour-6)/24*Mathf.PI*2);Daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.10f,.35f,solar));
        float cloud=state.weather==PetTownWeather.Rain?.55f:state.weather==PetTownWeather.Cloudy?.76f:state.weather==PetTownWeather.Snow?.8f:1;
        sun.transform.rotation=Quaternion.Euler(solar>0?Mathf.Max(5,solar*64):30,Mathf.Repeat(35+(state.Hour-8)*6,360),0);
        sun.intensity=Mathf.Lerp(.42f,1.1f,Daylight)*cloud;sun.color=Color.Lerp(new Color(.65f,.75f,1),Color.Lerp(new Color(1,.64f,.40f),new Color(1,.94f,.84f),Mathf.Clamp01(solar*2)),Daylight);sun.shadowStrength=Mathf.Lerp(.45f,.78f,Daylight);
        RenderSettings.ambientLight=Color.Lerp(new Color(.42f,.47f,.59f),new Color(.47f,.49f,.51f),Daylight)*Mathf.Lerp(.9f,1,cloud);
        var camera=Camera.main;if(camera)camera.backgroundColor=Color.Lerp(new Color(.08f,.12f,.21f),new Color(.66f,.83f,.94f),Daylight)*Mathf.Lerp(.82f,1,cloud);
        var fog=PetTownDistanceFog.Current;if(fog)fog.SetAtmosphereColor(Color.Lerp(new Color(.19f,.25f,.34f),state.weather==PetTownWeather.Rain?new Color(.65f,.72f,.74f):new Color(.78f,.84f,.79f),Daylight));
        if(Time.unscaledTime>=nextLights){nextLights=Time.unscaledTime+2;foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Point&&light.name=="WarmInteriorLight"&&!interior.ContainsKey(light))interior[light]=light.intensity;}
        foreach(var pair in interior)if(pair.Key)pair.Key.intensity=pair.Value*Mathf.Lerp(1.5f,.5f,Daylight);
        if(rain)
        {
            bool enabled=state.weather==PetTownWeather.Rain;var emission=rain.emission;emission.enabled=enabled;
            if(enabled&&!rain.isPlaying)rain.Play();if(!enabled&&rain.isPlaying)rain.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            Vector3 focus=fog?fog.Focus:game.transform.position;if(camera&&!fog){var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));float t;if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out t))focus=ray.GetPoint(t);}
            rain.transform.position=focus+Vector3.up*18;var shape=rain.shape;float size=camera?Mathf.Clamp(camera.orthographicSize*2.5f,18,55):22;shape.scale=new Vector3(size,.1f,size);
        }
        if(snow)
        {
            bool on=state.weather==PetTownWeather.Snow;var em=snow.emission;em.enabled=on;if(on&&!snow.isPlaying)snow.Play();if(!on&&snow.isPlaying)snow.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            Vector3 sf=fog?fog.Focus:game.transform.position;snow.transform.position=sf+Vector3.up*18;var ss=snow.shape;float sz=camera?Mathf.Clamp(camera.orthographicSize*2.5f,18,55):22;ss.scale=new Vector3(sz,.1f,sz);
        }
    }
    void OnDestroy()
    {
        if(!initialized)return;if(sun){sun.transform.rotation=originalRotation;sun.color=originalColor;sun.intensity=originalIntensity;sun.shadowStrength=originalShadow;}RenderSettings.ambientLight=originalAmbient;if(Camera.main)Camera.main.backgroundColor=originalSky;foreach(var pair in interior)if(pair.Key)pair.Key.intensity=pair.Value;if(rain)Destroy(rain.gameObject);if(snow)Destroy(snow.gameObject);if(rainMaterial)Destroy(rainMaterial);if(ambience)Destroy(ambience);
    }
}
