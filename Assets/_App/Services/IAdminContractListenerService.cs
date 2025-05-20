using System;
using System.Collections.Generic;

namespace _App.Services
{
    public interface IAdminContractListenerService
    {
        void ListenToAdminContracts(string adminUID, Action<List<SmartContractModel>> onChanged);
        void StopListening();
    }

}