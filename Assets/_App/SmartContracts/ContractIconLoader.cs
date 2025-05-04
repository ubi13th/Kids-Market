using UnityEngine;
using System.IO;

public static class ContractIconLoader
{
    private const string ContractIconsFolder = "Icons/ContractIcons";

    public static Sprite Load(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        if (IsGalleryPath(iconPath))
        {
            return LoadFromGallery(iconPath);
        }

        return Resources.Load<Sprite>($"{ContractIconsFolder}/{iconPath}");
    }

    public static Sprite[] LoadAllIcons()
    {
        return Resources.LoadAll<Sprite>(ContractIconsFolder);
    }

    private static bool IsGalleryPath(string path)
    {
        return path.StartsWith("/") || path.Contains("://");
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
}