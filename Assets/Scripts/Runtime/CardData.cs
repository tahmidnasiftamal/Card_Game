using System.Collections.Generic;
using UnityEngine;
namespace CrimsonDynasty
{
    [CreateAssetMenu(fileName = "Card_NewCharacter", menuName = "Crimson Dynasty/Character Card", order = 0)]
    public class CardData : ScriptableObject
    {
        [SerializeField] private Sprite portrait;
        [SerializeField] private string characterName;
        [SerializeField] private string title;
        [SerializeField] private string house;
        [SerializeField] private string loyalty;
        [SerializeField] private int age;
        [SerializeField] private int health = 100;
        [SerializeField] private int xp;
        [SerializeField] private int influenceRate;
        [SerializeField] private string specialAbility;
        [SerializeField] private List<string> oathSlots = new List<string>();
        [SerializeField] private List<string> status = new List<string>();
        public Sprite Portrait => portrait;
        public string CharacterName => characterName;
        public string Title => title;
        public string House => house;
        public string Loyalty => loyalty;
        public int Age => age;
        public int Health => health;
        public int XP => xp;
        public int InfluenceRate => influenceRate;
        public string SpecialAbility => specialAbility;
        public IReadOnlyList<string> OathSlots => oathSlots;
        public IReadOnlyList<string> Status => status;
    }
}
