using UnityEngine;
using UnityEngine.AI;

// Adjust the model wrapper only; the existing grounding/animation/navigation owners remain active.
[DefaultExecutionOrder(150)]
public sealed class PetTownPetGrowthVisual : MonoBehaviour
{
    Transform model;Vector3 adultScale;PetTownPetCareRecord record;PetTownResidentPetRecord resident;NavMeshAgent agent;float adultRadius,adultHeight,current=1;
    public float CurrentScale=>current;
    public void Initialize(Transform target,PetTownPetCareRecord pet)
    {
        if(!model){model=target;adultScale=target.localScale;agent=GetComponent<NavMeshAgent>();if(agent){adultRadius=agent.radius;adultHeight=agent.height;}current=PetTownPetCareRecord.SizeAtLevel(pet.Level);}
        record=pet;resident=null;Apply(0);
    }
    public void InitializeResident(Transform target,PetTownResidentPetRecord pet)
    {
        if(!model){model=target;adultScale=target.localScale;agent=GetComponent<NavMeshAgent>();if(agent){adultRadius=agent.radius;adultHeight=agent.height;}current=PetTownPetCareRecord.SizeAtLevel(pet.level);}
        resident=pet;record=null;Apply(0);
    }
    public void Apply(float seconds)
    {
        if(!model||(record==null&&resident==null))return;float desired=PetTownPetCareRecord.SizeAtLevel(record!=null?record.Level:resident.level);current=Mathf.MoveTowards(current,desired,Mathf.Max(0,seconds)*.3f);model.localScale=adultScale*current;
        if(agent){agent.radius=Mathf.Max(.10f,adultRadius*current);agent.height=Mathf.Max(.25f,adultHeight*current);}
    }
    void LateUpdate(){Apply(Time.deltaTime);}
}
