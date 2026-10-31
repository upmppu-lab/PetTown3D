using UnityEngine;

public partial class PetTownGame
{
    public bool PlacementActive { get { return mode == Mode.Build && selected >= 0; } }
    public void CancelPlacement() { SetMode(Mode.Play); Message("건설을 취소했어요"); }
}
