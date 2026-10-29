using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Residents talk above their heads. The top-centre message stays for important notices only.
public partial class PetTownGame
{
    readonly Dictionary<string, float> speechNext = new Dictionary<string, float>();
    readonly Dictionary<string, int> speechKey = new Dictionary<string, int>();
    readonly Dictionary<string, float> speechEventAt = new Dictionary<string, float>();
    readonly Dictionary<string, float> shopTalkAt = new Dictionary<string, float>();

    static readonly string[] ImportantWords =
    {
        "퀘스트", "의뢰", "레벨", "Lv.", "저장", "Save", "축제", "시작됐어요", "태어났", "입주", "이사", "도착했어요", "증축", "건설", "Cannot",
        "Not enough", "Road", "opened", "upgraded", "Choose", "Select", "built", "Customer", "골드가 부족", "부족합니다", "취소", "실패", "새로운 하루", "해금"
    };
    static readonly string[] RoutineWords =
    {
        "주었어요", "심었어요", "구매했어요", "보충했어요", "안내했어요", "지정했어요", "변경했어요", "주문했어요", "조리를 시작", "수납 완료",
        "정리했어요", "맞이했어요", "판매했어요", "보냈어요", "알을 품기", "교배를 시작", "자동 교배를", "예약했어요", "분양받았어요", "배송 물품을 받았어요",
        "만들었어요", "물을 주", "먹이를", "미용 키트", "물품을 주문", "안내를 시작", "소모품", "사료"
    };

    // Returns true when a routine confirmation was turned into floating text instead of the top banner.
    bool RoutineMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || mode == Mode.Start) return false;
        foreach (var word in ImportantWords) if (message.Contains(word)) return false;
        bool routine = false;
        foreach (var word in RoutineWords) if (message.Contains(word)) { routine = true; break; }
        if (!routine) return false;
        var state = Progression != null ? Progression.residents : null;
        if (state != null)
        {
            foreach (var person in state.people)
            {
                GameObject actor;
                if (!string.IsNullOrEmpty(person.name) && message.StartsWith(person.name) && residentActors.TryGetValue(person.id, out actor) && actor)
                {
                    PetTownSpeech.Say(actor, message, PetTownSpeech.Tone.Happy, 4.2f);
                    return true;
                }
            }
        }
        if (!hero) return false;
        PetTownSpeech.Say(hero, message, PetTownSpeech.Tone.Normal, 3.6f);
        return true;
    }

    static PetTownSpeech.Tone ToneFor(SpeechKind kind)
    {
        switch (kind)
        {
            case SpeechKind.Happy: case SpeechKind.CafeEat: case SpeechKind.FishCaught: case SpeechKind.Festival: case SpeechKind.Adopt: case SpeechKind.Playground: case SpeechKind.Rich:
            case SpeechKind.ClinicDone: case SpeechKind.CafeDone: return PetTownSpeech.Tone.Happy;
            case SpeechKind.Angry: return PetTownSpeech.Tone.Angry;
            case SpeechKind.Sick: case SpeechKind.ClinicTreat: return PetTownSpeech.Tone.Sick;
            case SpeechKind.Sad: case SpeechKind.Poor: case SpeechKind.ShopEmpty: return PetTownSpeech.Tone.Sad;
        }
        return PetTownSpeech.Tone.Normal;
    }

    // What a resident is doing right now: speech situation, a readable status line and a key that changes whenever the activity changes.
    void ResidentActivity(PetTownResidentRecord person, GameObject actor, out SpeechKind kind, out string status, out int key)
    {
        kind = SpeechKind.Idle; status = "마을을 둘러보는 중"; key = 0;
        var agent = actor ? actor.GetComponent<NavMeshAgent>() : null;
        bool moving = agent != null && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > .04f;
        if (moving) { kind = SpeechKind.Walking; status = "마을을 산책하는 중"; key = 1; }
        else { status = "잠시 서서 쉬는 중"; }
        ResidentCompanion companion;
        if (residentCompanions.TryGetValue(person.id, out companion) && companion != null && companion.root) status = "펫과 함께 산책하는 중";
        if (DecorVisitStatus(person.id, ref kind, ref status, ref key)) return;
        if (Progression == null) return;
        var visits = Progression.Facilities != null ? Progression.Facilities.visits : null;
        if (visits != null)
        {
            var visit = visits.Find(v => v.residentId == person.id);
            if (visit != null)
            {
                bool cafe = visit.kind == 3;
                key = 100 + visit.kind * 20 + (int)visit.stage;
                switch (visit.stage)
                {
                    case PetTownFacilityStage.Entrance: kind = cafe ? SpeechKind.CafeEnter : SpeechKind.ClinicEnter; status = cafe ? "카페로 들어가는 중" : "병원으로 들어가는 중"; break;
                    case PetTownFacilityStage.Reception: case PetTownFacilityStage.Registering: kind = cafe ? SpeechKind.CafeOrder : SpeechKind.ClinicEnter; status = cafe ? "카페에서 주문하는 중" : "병원에서 접수하는 중"; break;
                    case PetTownFacilityStage.ToWork: case PetTownFacilityStage.Waiting: kind = cafe ? SpeechKind.CafeWait : SpeechKind.ClinicWait; status = cafe ? "음식을 기다리는 중" : "진료를 기다리는 중"; break;
                    case PetTownFacilityStage.Working: kind = cafe ? SpeechKind.CafeWait : SpeechKind.ClinicTreat; status = cafe ? "음식이 나오길 기다리는 중" : "펫이 진료를 받는 중"; break;
                    case PetTownFacilityStage.Eating: kind = SpeechKind.CafeEat; status = "카페에서 식사하는 중"; break;
                    case PetTownFacilityStage.ToPay: kind = SpeechKind.Pay; status = "계산하러 가는 중"; break;
                    case PetTownFacilityStage.Payment: kind = SpeechKind.PayAmount; status = "계산하는 중"; break;
                    default: kind = cafe ? SpeechKind.CafeDone : SpeechKind.ClinicDone; status = cafe ? "카페에서 나가는 중" : "병원에서 나가는 중"; break;
                }
                return;
            }
        }
        var community = Progression.Community;
        if (community != null)
        {
            var visit = community.visits.Find(v => v.residentId == person.id);
            if (visit != null)
            {
                var lot = community.lots.Find(l => l.id == visit.buildingId);
                int lotKind = lot != null ? lot.kind : 5;
                bool inUse = visit.stage == 2;
                key = 300 + lotKind * 10 + (inUse ? visit.slot + 1 : 0);
                if (lotKind == 6)
                {
                    if (!inUse) { kind = SpeechKind.Lake; status = "호수로 가는 중"; }
                    else if (visit.slot < PetTownCommunityState.FishingSlots) { kind = SpeechKind.Fishing; status = "호수에서 낚시하는 중"; }
                    else if (visit.slot == 2) { kind = SpeechKind.Throwing; status = "호수에서 돌을 던지는 중"; }
                    else { kind = SpeechKind.Lake; status = "호숫가에서 쉬는 중"; }
                }
                else if (lotKind == 7) { kind = SpeechKind.Zoo; status = inUse ? "동물원을 구경하는 중" : "동물원으로 가는 중"; }
                else { kind = inUse ? (visit.slot % 2 == 0 ? SpeechKind.Playground : SpeechKind.Sitting) : SpeechKind.Park; status = inUse ? "공원에서 쉬는 중" : "공원으로 가는 중"; }
                return;
            }
        }
        if (TimeWeather != null && TimeWeather.Night && !moving) { kind = SpeechKind.Night; status = "집에서 쉬는 중"; key = 2; }
    }

    public string ResidentStatusText(PetTownResidentRecord person)
    {
        GameObject actor;
        residentActors.TryGetValue(person.id, out actor);
        if (!actor) return "외출 준비 중";
        SpeechKind kind; string status; int key;
        ResidentActivity(person, actor, out kind, out status, out key);
        return status;
    }

    SpeechKind PickAmbient(PetTownResidentRecord person, bool hasPet)
    {
        float r = Random.value;
        var weather = TimeWeather;
        PetTownResidentProfile.Ensure(person);
        if (weather != null && weather.festivalDay == day && r < .25f) return SpeechKind.Festival;
        if (person.happiness < 25 && r < .45f) return Random.value < .6f ? SpeechKind.Angry : SpeechKind.Sad;
        if (person.happiness > 72 && r < .3f) return SpeechKind.Happy;
        if (person.money < 60 && r < .2f) return SpeechKind.Poor;
        if (person.money > 900 && r < .12f) return SpeechKind.Rich;
        if (hasPet && r < .3f) return SpeechKind.WithPet;
        if (weather != null)
        {
            if (weather.weather == PetTownWeather.Rain && r < .5f) return SpeechKind.Rain;
            if (weather.weather == PetTownWeather.Snow && r < .5f) return SpeechKind.Snow;
            if (weather.weather == PetTownWeather.Cloudy && r < .2f) return SpeechKind.Cloudy;
            float hour = weather.Hour;
            if (weather.Night && r < .5f) return Random.value < .3f ? SpeechKind.HomeBound : SpeechKind.Night;
            if (hour < 10 && r < .5f) return SpeechKind.Morning;
            if (hour >= 17 && r < .5f) return SpeechKind.Evening;
            if (hour >= 11 && hour < 16 && r < .4f) return SpeechKind.Afternoon;
            if (weather.weather == PetTownWeather.Clear && r < .55f) return SpeechKind.Clear;
        }
        float pick = Random.value;
        if (pick < .15f) return SpeechKind.Tired;
        if (pick < .27f) return SpeechKind.Hungry;
        if (pick < .4f) return SpeechKind.Greeting;
        if (pick < .7f) return SpeechKind.Walking;
        return SpeechKind.Idle;
    }

    // Called a couple of times a second by PetTownSpeechDriver.
    public void SpeechTick()
    {
        if (loadingTown || expansionPending || mode == Mode.Start || Progression == null) return;
        var state = EnsureResidents();
        float now = Time.time;
        foreach (var person in state.people)
        {
            GameObject actor;
            if (!residentActors.TryGetValue(person.id, out actor) || !actor || !actor.activeInHierarchy || IsInVillagerChat(actor)) continue;
            SpeechKind kind; string status; int key;
            ResidentActivity(person, actor, out kind, out status, out key);
            string petName = null;
            ResidentCompanion companion;
            bool hasPet = residentCompanions.TryGetValue(person.id, out companion) && companion != null && companion.root;
            if (hasPet) { var record = person.pets.Find(p => p.id == companion.petId); petName = record != null ? record.name : null; }
            else if (person.pets.Count > 0) petName = person.pets[0].name;

            float next;
            if (!speechNext.TryGetValue(person.id, out next)) { speechNext[person.id] = now + Random.Range(2f, 12f); continue; }
            int previous;
            bool changed = speechKey.TryGetValue(person.id, out previous) && previous != key && key >= 100;
            speechKey[person.id] = key;
            var speech = actor.GetComponent<PetTownSpeech>();
            bool busy = speech && speech.Active;
            if (changed && !busy)
            {
                float last;
                if (!speechEventAt.TryGetValue(person.id, out last) || now - last > 3f)
                {
                    speechEventAt[person.id] = now;
                    if (Random.value < .85f)
                    {
                        PetTownSpeech.Say(actor, PetTownSpeechLines.Pick(kind, petName), ToneFor(kind));
                        speechNext[person.id] = now + Random.Range(7f, 14f);
                        continue;
                    }
                }
            }
            // Shop visit with nothing to adopt.
            if (!busy && Vector3.Distance(actor.transform.position, shopPosition) < 9f)
            {
                float last;
                if (!shopTalkAt.TryGetValue(person.id, out last) || now - last > 45f)
                {
                    shopTalkAt[person.id] = now;
                    bool anyPet = Progression.care != null && Progression.care.pets.Exists(p => p.Adoptable);
                    PetTownSpeech.Say(actor, PetTownSpeechLines.Pick(anyPet ? SpeechKind.ShopVisit : SpeechKind.ShopEmpty, petName), anyPet ? PetTownSpeech.Tone.Normal : PetTownSpeech.Tone.Sad);
                    speechNext[person.id] = now + Random.Range(8f, 15f);
                    continue;
                }
            }
            if (now < next || busy) continue;
            speechNext[person.id] = now + Random.Range(9f, 22f);
            if (key >= 100 && Random.value < .6f)
            {
                PetTownSpeech.Say(actor, PetTownSpeechLines.Pick(kind, petName), ToneFor(kind));
                continue;
            }
            var ambient = PickAmbient(person, hasPet);
            PetTownSpeech.Say(actor, PetTownSpeechLines.Pick(ambient, petName), ToneFor(ambient));
        }
    }
}
