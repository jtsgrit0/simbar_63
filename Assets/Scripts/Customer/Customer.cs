using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Customer : MonoBehaviour
{
    public enum CustomerState { Entering, Waiting, Ordering, Drinking, Leaving, Exited }

    public CustomerState currentState;
    public string customerName;
    public float patience = 60f;
    public float currentPatience;
    public float satisfaction = 0f;
    public DrinkData desiredDrink;
    public Table assignedTable;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform targetSeat;

    public float stateTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (agent == null)
        {
            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        switch (currentState)
        {
            case CustomerState.Entering:
                if (!agent.pathPending && agent.remainingDistance < 0.1f)
                {
                    currentState = CustomerState.Waiting;
                    stateTimer = 0f;
                }
                break;

            case CustomerState.Waiting:
                stateTimer += Time.deltaTime;
                currentPatience = patience - stateTimer;
                if (currentPatience <= 0f)
                {
                    Leave(false);
                }
                break;

            case CustomerState.Ordering:
                if (desiredDrink != null)
                {
                    currentState = CustomerState.Drinking;
                    stateTimer = 0f;
                }
                break;

            case CustomerState.Drinking:
                stateTimer += Time.deltaTime;
                if (stateTimer > 15f)
                {
                    Leave(true);
                }
                break;

            case CustomerState.Leaving:
                if (!agent.pathPending && agent.remainingDistance < 0.1f)
                {
                    currentState = CustomerState.Exited;
                    Destroy(gameObject, 2f);
                }
                break;
        }
    }

    public void EnterPub(Transform entryPoint, Table table)
    {
        assignedTable = table;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null)
        {
            return;
        }

        agent.enabled = true;
        transform.position = entryPoint.position;
        EnsureAgentOnNavMesh(transform.position);

        if (table != null)
        {
            table.EnsureSeatTransform();
            targetSeat = table.seatTransform;
        }

        Vector3 destination = targetSeat != null ? targetSeat.position : entryPoint.position + new Vector3(2f, 0f, 0f);
        if (!EnsureAgentOnNavMesh(destination))
        {
            return;
        }

        agent.SetDestination(destination);
        currentState = CustomerState.Entering;
    }

    private bool EnsureAgentOnNavMesh(Vector3 worldPosition)
    {
        if (agent == null)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
            return true;
        }

        return false;
    }

    public void PlaceOrder(DrinkData drink)
    {
        desiredDrink = drink;
        currentState = CustomerState.Ordering;
    }

    public void ReceiveDrink()
    {
        if (currentState != CustomerState.Ordering) return;
        currentState = CustomerState.Drinking;
        stateTimer = 0f;
        satisfaction = Mathf.Clamp01(currentPatience / patience);
    }

    public void Leave(bool satisfied)
    {
        currentState = CustomerState.Leaving;

        if (agent != null && GameManager.Instance != null)
        {
            agent.SetDestination(GameManager.Instance.transform.position);
        }

        if (assignedTable != null)
        {
            assignedTable.isOccupied = false;
            assignedTable = null;
        }

        if (desiredDrink == null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.reputation = Mathf.Max(0, GameManager.Instance.reputation - 1);
            }
            return;
        }

        float tip = satisfied ? desiredDrink.price * (0.1f + satisfaction * 0.2f) : 0f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.money += desiredDrink.price + tip;
            if (!satisfied)
            {
                GameManager.Instance.reputation = Mathf.Max(0, GameManager.Instance.reputation - 1);
            }
            else
            {
                GameManager.Instance.reputation += 1;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
