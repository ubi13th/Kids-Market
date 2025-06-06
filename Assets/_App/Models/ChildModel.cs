using System;
using System.Collections.Generic;
using _App.Models;

[Serializable]
public class ChildModel : UserModel
{
    public string AdminUID;
    public float Balance;
    public RewardType RewardPreference = RewardType.Money;
    public List<SavingJarModel> SavingJars = new();
}