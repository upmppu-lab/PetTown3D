using UnityEngine;

// Job and pocket money for each resident. Assigned deterministically from the resident id, so old saves get a stable profile.
public static class PetTownResidentProfile
{
    static readonly string[] Jobs =
    {
        "회사원", "교사", "요리사", "화가", "농부", "간호사", "프로그래머", "작가",
        "사진작가", "택배 기사", "음악가", "소방관", "제빵사", "수의사 보조", "도서관 사서", "정원사"
    };
    static readonly int[] Pay = { 70, 62, 58, 44, 50, 66, 84, 40, 48, 56, 36, 72, 54, 68, 52, 46 };

    public static int JobCount { get { return Jobs.Length; } }
    public static string JobName(int index) { return Jobs[Mathf.Clamp(index, 0, Jobs.Length - 1)]; }

    static int Hash(string text)
    {
        unchecked { int h = 17; if (text != null) foreach (char c in text) h = h * 31 + c; return h & 0x7fffffff; }
    }

    public static void Ensure(PetTownResidentRecord person)
    {
        if (person == null) return;
        if (string.IsNullOrEmpty(person.job)) person.job = Jobs[Hash(person.id) % Jobs.Length];
        if (person.money < 0) person.money = 180 + Hash(person.id + "m") % 620;
    }

    public static int Salary(PetTownResidentRecord person)
    {
        Ensure(person);
        int index = System.Array.IndexOf(Jobs, person.job);
        return index >= 0 ? Pay[index] : 50;
    }

    // One game day of work and living costs. Pets cost a little more to keep.
    public static void Daily(PetTownResidentState state)
    {
        if (state == null) return;
        foreach (var person in state.people)
        {
            Ensure(person);
            int cost = 28 + person.pets.Count * 14;
            person.money = Mathf.Max(0, person.money + Salary(person) - cost);
        }
    }

    public static string MoodLabel(float happiness)
    {
        if (happiness >= 80) return "아주 행복해요";
        if (happiness >= 60) return "기분 좋아요";
        if (happiness >= 40) return "평범해요";
        if (happiness >= 25) return "조금 우울해요";
        return "화가 나 있어요";
    }
}
