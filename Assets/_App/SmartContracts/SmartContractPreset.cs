using UnityEngine;

[CreateAssetMenu(fileName = "SmartContractPreset", menuName = "KidsMarket/SmartContractPreset")]
public class SmartContractPreset : ScriptableObject
{
    public string title;
    [TextArea]
    public string description;
    public string iconPath;
    public float defaultReward;
}