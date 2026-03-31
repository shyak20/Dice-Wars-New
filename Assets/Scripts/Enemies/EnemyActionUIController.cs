using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    public class EnemyActionUIController : MonoBehaviour
    {
        [SerializeField] private Image _actionIcon;
        [SerializeField] private TMP_Text _actionValue;

        [SerializeField] private EnemyController _enemyController;
        
        void Start()
        {
            _enemyController.CurrentIntent
                .Subscribe(OnCurrentIntentChanged)
                .AddTo(this);
        }

        private void OnCurrentIntentChanged(EnemyActionSO intent)
        {
            _actionIcon.sprite = intent.icon;
            if (intent.armor > 0)
            {
                _actionValue.text = intent.armor.ToString();
            } 
            else if (intent.damage > 0)
            {
                _actionValue.text = intent.damage.ToString();
                if (intent.numberOfAttacks > 0)
                {
                    _actionValue.text += $"x{intent.numberOfAttacks}";
                }
            }
        }
    }
}