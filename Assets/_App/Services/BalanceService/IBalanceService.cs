using System;

namespace _App.Services.BalanceService
{
    public interface IBalanceService
    {
        void AdjustBalance(string childUid, float delta, string reason, bool recordHistory = true, Action<bool> onComplete = null);
        void GetBalance(string childUid, Action<float> onResult);
        void ListenToBalance(string childUid, Action<float> onChanged);
        void AddBalanceHistory(string childUid, float delta, string reason);
    }
}