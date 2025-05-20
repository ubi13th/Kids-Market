using UnityEngine;

namespace _App.Services
{
    public interface IAvatarService
    {
        Sprite LoadAvatar(string avatarPath);
    }

}