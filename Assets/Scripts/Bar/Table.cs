using UnityEngine;

public class Table : MonoBehaviour
{
    public bool isOccupied;
    public Transform seatTransform;
    public Transform customerTransform;

    private void Awake()
    {
        EnsureSeatTransform();
    }

    private void Reset()
    {
        EnsureSeatTransform();
    }

    public void EnsureSeatTransform()
    {
        if (seatTransform == null)
        {
            var seat = new GameObject(name + "_Seat");
            seat.transform.SetParent(transform);
            seat.transform.localPosition = new Vector3(0f, 0.5f, 1.2f);
            seatTransform = seat.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 1f);
        if (seatTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(seatTransform.position, 0.3f);
        }
    }
}
