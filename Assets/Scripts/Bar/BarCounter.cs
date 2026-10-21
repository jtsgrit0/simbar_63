using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarCounter : MonoBehaviour
{
    public List<Transform> servicePoints = new List<Transform>();
    public Transform stockFridge;
    public float serveRange = 2f;
    public LayerMask customerLayer;

    private List<Customer> waitingCustomers = new List<Customer>();

    private void Awake()
    {
        customerLayer = LayerMask.GetMask("Default");
    }

    private void Update()
    {
    }

    public void TryServeCustomer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, serveRange, customerLayer);
        if (hits.Length == 0) return;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Customer customer) && customer.currentState == Customer.CustomerState.Ordering)
            {
                if (customer.desiredDrink != null)
                {
                    customer.ReceiveDrink();
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(customer.desiredDrink.serveSound);
                    }
                    break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, serveRange);
    }
}
