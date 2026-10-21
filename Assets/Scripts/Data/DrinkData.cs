using UnityEngine;

[CreateAssetMenu(fileName = "NewDrink", menuName = "SimBar/Drink Data")]
public class DrinkData : ScriptableObject
{
    public string drinkId = "new_drink";
    public string drinkName = "Unnamed Drink";
    public string displayName = "Unnamed Drink";
    public string description = "A delicious drink";
    public Sprite drinkIcon;
    public float price = 10f;
    public float prepareTime = 3f;
    public float prepTime = 3f; // Legacy support for old code
    public AudioClip serveSound;
}