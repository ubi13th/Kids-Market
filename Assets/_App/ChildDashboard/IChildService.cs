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
        void SaveChildProfile(ChildModel child, Action<bool> callback);
        void DeleteChild(string childId, Action<bool> callback);
        void GetAdminProfile(string adminUid, Action<UserModel> callback);
        void StopListening();
        
        ChildModel GetChildModel();
    }
}