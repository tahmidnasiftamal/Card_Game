using System.Collections.Generic;
using UnityEngine;
namespace CrimsonDynasty
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Crimson Dynasty/Card Database", order = 10)]
    public class CardDatabase : ScriptableObject
    {
        public List<CardData> cards = new List<CardData>();
    }
}
