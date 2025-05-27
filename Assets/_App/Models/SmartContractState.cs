using System;

[Serializable]
public enum SmartContractState
{
    ReadyToSell = 0,
    ReadyToBuy = 1,
    ReadyToConfirm = 2,
    Completed = 3,
    Purchased = 4
}