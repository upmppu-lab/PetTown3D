using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    public IEnumerable<GameObject> BuildingObjects { get { return buildings; } }
    // Residents' houses use the open, enterable procedural interior instead of the closed medieval model.
    const bool EnterableHouses = true;
}
