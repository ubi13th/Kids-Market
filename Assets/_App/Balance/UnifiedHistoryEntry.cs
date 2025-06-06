using System;

namespace _App.Balance
{
    [Serializable]
    public class UnifiedHistoryEntry
    {
        public enum EntryType { Balance, Jar }
        public EntryType Type;
        public string Reason;
        public float Amount;
        public float BalanceAfter;
        public string JarName;
        public DateTime Timestamp;
        public bool IsCredit => Amount > 0;
    }
}