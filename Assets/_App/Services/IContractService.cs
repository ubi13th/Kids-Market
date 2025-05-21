using System;

namespace _App.Services
{
    public interface IContractService
    {
        void GetContractById(string contractId, Action<SmartContractModel> callback);
        void DeleteContract(string contractId, Action<bool> onComplete);
        void SaveContract(SmartContractModel contract, Action<bool> onComplete);

        // 🆕 New methods for per-day state handling
        void SetContractStateOnDate(string contractId, DateTime date, SmartContractState state, Action<bool> onComplete);
    }
}