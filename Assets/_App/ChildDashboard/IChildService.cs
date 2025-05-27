using System;
using System.Collections.Generic;

namespace _App.ChildDashboard
{
    public interface IChildService
    {
        void ListenToChildren(string adminUID, Action<List<ChildModel>> onChanged);
        void AddNewChild(ChildModel child, Action<bool> onComplete);
        void GetChildById(string childId, Action<ChildModel> callback);
        void UpdateBalance(string childUid, float newBalance, Action<bool> callback);
    }
}