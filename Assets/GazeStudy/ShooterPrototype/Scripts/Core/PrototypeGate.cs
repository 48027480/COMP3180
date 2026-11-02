using COMP3180.GazeStudy;
using UnityEngine;

/// Blocks the corridor to the prototype area until the player has the gun.
public class PrototypeGate : MonoBehaviour
{
    [SerializeField] GazePickupSelector selector;
    [SerializeField] GameObject barrier;

    void Update()
    {
        bool closed = !selector.GunEquipped;
        if (barrier.activeSelf != closed) barrier.SetActive(closed);
    }
}
