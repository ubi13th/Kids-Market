namespace _App.Services
{
    public static class ContractIdHelper
    {
        public static bool TryNormalizeVisualContractId(
            string visualId,
            out string realId,
            out string queueKey)
        {
            realId = visualId;
            queueKey = null;

            if (string.IsNullOrEmpty(visualId))
                return false;

            if (visualId.Contains("_"))
            {
                var parts = visualId.Split('_');

                // ContractId_2025-05-26#2 → contractId, queueKey
                if (parts.Length == 2)
                {
                    realId = parts[0];
                    queueKey = parts[1];
                    return true;
                }

                return false;
            }

            return true; // valid ID with no queueKey suffix
        }
    }
}