using UnityEngine;
using UnityEngine.UI;

namespace _App.Subscription
{
    public class SubscriptionHandler : MonoBehaviour
    {
        [SerializeField] private Button buySubscriptionButton;
        [SerializeField] private Button skipBuySubscriptionButton;

        private void Start()
        {
            buySubscriptionButton.onClick.AddListener(ActivateSubscription);
            skipBuySubscriptionButton.onClick.AddListener(SkipBuySubscription);
        }

        private void ActivateSubscription()
        {
            SubscriptionManager.ActivatePremiumManually();
        }

        private void SkipBuySubscription()
        {
            SceneLoader.LoadHomeScene();
        }
    }
}