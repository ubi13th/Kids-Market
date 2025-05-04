public static class SmartContractStateExtensions
{
    public static string ToDisplayString(this SmartContractState state) =>
        state switch
        {
            SmartContractState.ReadyToSell => "Sell",
            SmartContractState.PendingConfirmation => "Pending",
            SmartContractState.Completed => "Done",
            _ => "Unknown"
        };
}