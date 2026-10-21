using System;
using UnityEngine;

[Serializable]
public class Order
{
    public Customer customer;
    public DrinkData drink;
    public float waitTime;
    public float maxWaitTime = 120f;
    public bool isCompleted;
    public bool isExpired;

    public Order(Customer customer, DrinkData drink)
    {
        this.customer = customer;
        this.drink = drink;
        this.waitTime = 0f;
    }
}
