using System.IO;
using System.Collections.Generic;
using UnityEngine;

public static class PresetStorage
{
    private static readonly string PresetFolder = Path.Combine(Application.persistentDataPath, "Presets");

    public static void SavePreset(SmartContractCustomPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.title))
        {
            Debug.LogWarning("❌ Cannot save preset: title is empty.");
            return;
        }

        string folderPath = Path.Combine(Application.persistentDataPath, "Presets");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string sanitizedTitle = string.Concat(preset.title.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(sanitizedTitle))
        {
            Debug.LogWarning("❌ Cannot save preset: sanitized title is invalid.");
            return;
        }

        string path = Path.Combine(folderPath, $"{sanitizedTitle}.json");

        File.WriteAllText(path, JsonUtility.ToJson(preset, true));
        Debug.Log($"✅ Saved preset to: {path}");
    }

    public static List<SmartContractCustomPreset> LoadAllPresets()
    {
        List<SmartContractCustomPreset> presets = new();

        if (!Directory.Exists(PresetFolder))
            return presets;

        foreach (string file in Directory.GetFiles(PresetFolder, "*.json"))
        {
            string json = File.ReadAllText(file);
            SmartContractCustomPreset customPreset = JsonUtility.FromJson<SmartContractCustomPreset>(json);
            if (customPreset != null)
                presets.Add(customPreset);
        }

        return presets;
    }

    public static void DeletePreset(string title)
    {
        string filePath = Path.Combine(PresetFolder, $"{SanitizeFileName(title)}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"🗑️ Deleted preset: {filePath}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}