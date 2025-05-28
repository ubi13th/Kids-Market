using System;
using System.Collections.Generic;

[Serializable]
public class FamilyModel
{
    public string AdminUid;
    public List<ChildModel> Kids = new();
    public List<UserModel> Adults = new();
}