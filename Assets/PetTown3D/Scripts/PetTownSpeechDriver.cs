using UnityEngine;

// Drives resident speech, entrance signs and touch inspection without touching the game's own Update loop.
public sealed class PetTownSpeechDriver : MonoBehaviour
{
    PetTownGame game;
    float nextSpeech, nextSign;

    public void Initialize(PetTownGame source) { game = source; }

    void Update()
    {
        if (!game) return;
        game.TouchTick();
        game.DecorTick();
        game.PetDeliveryTick();
        if (Time.unscaledTime >= nextSpeech) { nextSpeech = Time.unscaledTime + .6f; game.SpeechTick(); }
        if (Time.unscaledTime >= nextSign) { nextSign = Time.unscaledTime + 2f; game.SignTick(); }
    }
}
