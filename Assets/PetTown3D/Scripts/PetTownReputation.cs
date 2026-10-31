using UnityEngine;

// 명성 등급: 등급이 오를수록 분양가가 조금씩 올라간다(2D 게임의 평판 등급과 같은 역할).
public static class PetTownReputation
{
    static readonly int[] Need={0,20,60,150,400};
    static readonly string[] Names={"신생","동네","인기","유명","전설"};
    public static int GradeIndex(int rep){int g=0;for(int i=0;i<Need.Length;i++)if(rep>=Need[i])g=i;return g;}
    public static string Grade(int rep)=>Names[GradeIndex(rep)];
    public static float PriceMultiplier(int rep)=>1f+.04f*GradeIndex(rep);
    public static int NeedOf(int grade){return Need[Mathf.Clamp(grade,0,Need.Length-1)];}
    public static string NameOf(int grade){return Names[Mathf.Clamp(grade,0,Names.Length-1)];}
    public static int NextNeed(int rep){int g=GradeIndex(rep);return g+1<Need.Length?Need[g+1]:-1;}
}
