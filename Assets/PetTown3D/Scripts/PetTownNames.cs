using System.Collections.Generic;

// 주민(한국식 이름, 모델 성별에 맞춤)과 펫의 자동 이름. 저장된 이름은 그대로 두고, 기본 이름만 바꿉니다.
public static class PetTownNames
{
    static readonly string[] Surnames={"김","이","박","최","정","강","조","윤","장","임","한","오","서","신","권","황","안","송","류","홍"};
    static readonly string[] MaleGiven={"민준","서준","도윤","시우","주원","하준","지호","지훈","준서","현우","건우","우진","태양","승민","재민","동현","성민","영호","지환","상우","준혁","민재","정우","승현"};
    static readonly string[] FemaleGiven={"서연","서윤","지우","하윤","민서","지유","채원","지민","수아","예린","다은","소율","윤서","하은","유진","지아","나연","은서","수빈","예은","소현","가은","하린","시연"};
    static readonly string[] OldMale={"영철","병철","만수","종수","기태","봉식"};
    static readonly string[] OldFemale={"순자","영자","말순","옥순","정희","춘자"};
    static readonly string[] PetNames={"초코","콩이","보리","구름","별이","몽이","두부","쿠키","밤비","하늘","모찌","라떼","곰이","달이","뭉치","망고","호두","코코","솜이","단추","보들","먹물","감자","까미","방울","둥이","해피","루나","토리","나비","복실","마루","하루","봄이","행운","푸딩","꼬미","바다","사랑","튼튼"};

    // Villager_01..18 중 남성 모델(0부터 센 인덱스): 02(할아버지) 03 10 11 13.
    public static bool IsMale(int appearance){int a=((appearance%18)+18)%18;return a==1||a==2||a==9||a==10||a==12;}
    static bool IsOld(int appearance){int a=((appearance%18)+18)%18;return a==1||a==15;}

    public static string Resident(int appearance,int seed,HashSet<string> used)
    {
        bool male=IsMale(appearance),old=IsOld(appearance);
        for(int i=0;i<600;i++)
        {
            int s=seed*7919+i*104729;if(s<0)s=-s;
            string sur=Surnames[(s/3)%Surnames.Length];
            string[] given=old?(male?OldMale:OldFemale):(male?MaleGiven:FemaleGiven);
            string name=sur+given[(s/11+i)%given.Length];
            if(used==null||used.Add(name))return name;
        }
        return Surnames[seed%Surnames.Length]+(male?MaleGiven:FemaleGiven)[seed%MaleGiven.Length]+seed;
    }

    public static string Pet(int seed,HashSet<string> used)
    {
        for(int i=0;i<PetNames.Length;i++)
        {
            string name=PetNames[((seed*7+i)%PetNames.Length+PetNames.Length)%PetNames.Length];
            if(used==null||used.Add(name))return name;
        }
        string baseName=PetNames[((seed%PetNames.Length)+PetNames.Length)%PetNames.Length];
        for(int n=2;n<1000;n++){string name=baseName+n;if(used==null||used.Add(name))return name;}
        return baseName;
    }

    public static bool IsDefaultResidentName(string name){return string.IsNullOrEmpty(name)||name=="주민"||(name.StartsWith("주민 ")&&name.Length>3);}
    public static bool IsDefaultPetName(string name,string[] species)
    {
        if(string.IsNullOrEmpty(name))return true;
        foreach(var s in species){if(!name.StartsWith(s+" "))continue;string rest=name.Substring(s.Length+1);if(rest.Length==0)continue;bool digits=true;foreach(char c in rest)if(c<'0'||c>'9'){digits=false;break;}if(digits)return true;}
        return false;
    }
}
