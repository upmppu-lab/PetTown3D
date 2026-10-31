using System;
using UnityEngine;
using TMPro;

[Serializable]
public sealed class PetTownArtBuilding
{
    public int key, price;
    public string title, category, description, size;
    public GameObject model;
    public Texture2D thumbnail;
}

// A read-only snapshot for design review; never writes the town's save.
public sealed class PetTownArtPreviewData : ScriptableObject
{
    public int gold, day, residents, reputation, level, pets;
    public Texture2D portrait, iconAtlas;
    public TMP_FontAsset font;
    public Sprite paper, header, card, selectedCard, greenButton, lightButton, coralButton, shadow, circle;
    public Sprite barTrack,barFill;
    public Sprite hudCircle,hudChip,hudMessage;
    public PetTownArtBuilding[] buildings;
}
