using System;
using UnityEngine;

public static class GalleryPicker
{
    public static async void PickImage(Action<string> onImagePicked)
    {
        if (onImagePicked == null) return;
        
        var hasPermission = NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        if (!hasPermission)
        {
            var result = await NativeGallery.RequestPermissionAsync(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
            if (result != NativeGallery.Permission.Granted)
            {
                Debug.LogWarning("Permission not granted.");
                onImagePicked?.Invoke(null);
                return;
            }
        }

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("No image selected.");
                onImagePicked?.Invoke(null);
            }
            else
            {
                Debug.Log("Image path: " + path);
                onImagePicked?.Invoke(path);
            }
        }, "Select an Avatar");
    }
}