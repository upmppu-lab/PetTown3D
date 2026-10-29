using TMPro;
using UnityEngine;

// Marks a building's entrance sign. Tapping the board opens that building's status window.
public sealed class PetTownSignTag : MonoBehaviour
{
    public GameObject building;
    public int kind;
    public Transform board;
    public Vector3 BoardPosition { get { return board ? board.position : transform.position + Vector3.up * 2f; } }
}

// The board always turns toward the camera so the building name stays readable from any view angle.
[DefaultExecutionOrder(310)]
public sealed class PetTownSignBoard : MonoBehaviour
{
    Camera cam;
    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;
        transform.rotation = cam.transform.rotation;
        float s = cam.orthographic ? Mathf.Clamp(cam.orthographicSize / 12f, .75f, 2.2f) : Mathf.Clamp(Vector3.Distance(cam.transform.position, transform.position) / 30f, .75f, 2.2f);
        transform.localScale = Vector3.one * s;
    }
}
