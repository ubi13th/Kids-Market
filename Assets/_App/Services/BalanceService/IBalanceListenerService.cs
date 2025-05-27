using System;

namespace _App.Services.BalanceService
{
    public interface IBalanceListenerService
    {
        void ListenToBalance(string childUid, Action<float> onBalanceChanged);
        void StopListening(string childUid);
    }
}