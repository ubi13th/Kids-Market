using UnityEngine;
using System.IO;

public static class AvatarLoader
{
    private const string AvatarIconsFolder = "Icons/UserAvatars";
    private const string DefaultAvatarName = "Avatar0";

    public static Sprite LoadAvatar(string avatarPath)
    {
        if (string.IsNullOrEmpty(avatarPath))
            return LoadDefault();

        if (IsGalleryPath(avatarPath))
        {
            return LoadFromGallery(avatarPath) ?? LoadDefault();
        }

        return LoadFromResources(avatarPath) ?? LoadDefault();
    }

    private static bool IsGalleryPath(string path)
    {
        return path.StartsWith("/") || path.Contains("://");
    }

    private static Sprite LoadFromResources(string iconName)
    {
        return Resources.Load<Sprite>($"{AvatarIconsFolder}/{iconName}");
    }

    private static Sprite LoadFromGallery(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        byte[] bytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        if (!texture.LoadImage(bytes)) return null;

        Rect rect = new Rect(0, 0, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
    }

    private static Sprite LoadDefault()
    {
        return Resources.Load<Sprite>($"{AvatarIconsFolder}/{DefaultAvatarName}");
    }
}