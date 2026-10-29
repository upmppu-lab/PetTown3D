using UnityEngine;

public partial class PetTownGame
{
    PetTownPlayerControls explorationPlayer;
    float nextExploration;
    public PetTownExplorationState Exploration
    {
        get{var state=Progression;if(state.exploration==null)state.exploration=new PetTownExplorationState();state.exploration.EnsureBounds(WorldHalfExtent);return state.exploration;}
    }
    public void UpdateWorldExploration(bool force=false)
    {
        if(loadingTown||expansionPending||!Application.isPlaying||(!force&&Time.unscaledTime<nextExploration))return;
        nextExploration=Time.unscaledTime+.35f;var state=Exploration;bool changed=false;
        if(!explorationPlayer)explorationPlayer=Object.FindFirstObjectByType<PetTownPlayerControls>();
        if(explorationPlayer&&explorationPlayer.isActiveAndEnabled)changed|=state.Reveal(explorationPlayer.transform.position,24);
        foreach(var building in buildings)
        {
            if(!building||building.GetComponentInChildren<PetTownRoadPath>())continue;
            var room=building.GetComponent<PetTownInteriorState>();var metadata=building.GetComponent<PetTownBuildingPlacement>();
            Vector3 center=building.transform.position;float radius=12;
            if(room){var b=room.LocalBounds;center=room.transform.TransformPoint(new Vector3(b.center.x,0,b.center.y));radius+=b.size.magnitude*.5f;}
            else if(metadata&&metadata.Record!=null)radius+=metadata.Record.WorldFootprint.magnitude*.5f;
            changed|=state.Reveal(center,radius);
        }
        if(changed)progressionDirty=true;
    }
    void CreateExpandedOutskirts()
    {
        if(WorldHalfExtent<=110)return;
        var random=new System.Random(1217);
        for(int i=0;i<180;i++)
        {
            float x=(float)(random.NextDouble()*2-1)*(WorldHalfExtent-8),z=(float)(random.NextDouble()*2-1)*(WorldHalfExtent-8);
            if(Mathf.Abs(x)<110&&Mathf.Abs(z)<110||IsWater(x,z))continue;
            var p=new Vector3(x,Ground(x,z),z);
            if(i%4==0)MakeTree(p);else if(i%11==0)MakeRock(p);
        }
    }
}
