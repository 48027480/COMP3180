using UnityEngine;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// A floor button. Stepping on it returns every pickup and the gun to where they started and stops
    /// the playtest areas. Goes by the head's position, since room-scale steps don't move the rig.
    /// </summary>
    public class ResetPad : MonoBehaviour
    {
        [SerializeField] GazePickupSelector selector;
        [SerializeField] PlaytestManager playtests;
        [SerializeField] Transform head;
        [SerializeField] Transform button;
        [SerializeField] float radius = 0.6f;
        [SerializeField] float pressDepth = 0.03f;

        Vector3 buttonUp;
        bool stepping;

        void Start()
        {
            buttonUp = button.localPosition;
            stepping = IsPlayerOnPad();
        }

        void Update()
        {
            bool onPad = IsPlayerOnPad();
            if (onPad && !stepping)
            {
                selector.ResetAll();
                playtests.StopAll();
            }
            stepping = onPad;
            button.localPosition = onPad ? buttonUp + Vector3.down * pressDepth : buttonUp;
        }

        bool IsPlayerOnPad()
        {
            Vector3 offset = head.position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= radius;
        }
    }
}
