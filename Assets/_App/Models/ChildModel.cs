using System;

[Serializable]
public class ChildModel : UserModel
{
    public string AdminUID;
    public float Balance;
    public RewardType RewardPreference = RewardType.Money;
}

public enum RewardType
{
    Money,
    Points,
    Event,
    None
}