using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace CrimsonDynasty
{
    public class CardView : MonoBehaviour
    {
        [SerializeField] private CardData data;
        [Header("UI Bindings")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text houseText;
        public CardData Data
        {
            get => data;
            set { data = value; BindNow(); }
        }
        private void Awake()
        {
            if (data != null) BindNow();
        }
        public void BindNow()
        {
            if (data == null) return;
            if (portraitImage) portraitImage.sprite = data.Portrait;
            if (nameText) nameText.text = data.CharacterName;
            if (titleText) titleText.text = data.Title;
            if (houseText) houseText.text = data.House;
        }
    }
}
