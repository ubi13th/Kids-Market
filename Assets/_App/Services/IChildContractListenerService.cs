using System;
using System.Collections.Generic;

namespace _App.Services
{
    public interface IChildContractListenerService
    {
        void ListenToChildContracts(string childUID, Action<List<SmartContractModel>> onChanged);
        void StopListening();
    }
}