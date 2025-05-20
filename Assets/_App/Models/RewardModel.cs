using System;

[Serializable]
public class RewardModel
{
    public string ChildUid;
    public RewardType Type; // Money, Points, Event
    public string Description; // "Trip to the zoo", "Movie night", etc.
    public int Amount; // Used for money/points rewards
}