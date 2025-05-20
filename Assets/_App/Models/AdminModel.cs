using System;

[Serializable]
public class AdminModel : UserModel
{
    public string Email;
    public AppMode Mode; // Free or Premium
}