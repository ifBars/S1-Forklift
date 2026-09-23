using System;
using System.IO;
using System.Reflection;
using UnityEngine;

#if IL2CPP
using S1DateTime = Il2CppSystem.DateTime;
using S1DateTimeData = Il2CppScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using S1MetaData = Il2CppScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = Il2CppScheduleOne.Persistence.SaveInfo;
#else
using S1DateTime = System.DateTime;
using S1DateTimeData = ScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
using S1MetaData = ScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = ScheduleOne.Persistence.SaveInfo;
#endif

namespace S1Forklift;

internal static class DisposableSmokeSave
{
    internal static void EnableDebugMode(S1LoadManager loadManager)
    {
        var property = typeof(S1LoadManager).GetProperty(
            nameof(S1LoadManager.DebugMode),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var setter = property?.GetSetMethod(true)
                     ?? throw new MissingMethodException(
                         typeof(S1LoadManager).FullName,
                         "set_DebugMode");
        setter.Invoke(loadManager, new object[] { true });
        if (!loadManager.DebugMode)
            throw new InvalidOperationException("LoadManager debug mode did not activate.");
    }

    internal static S1SaveInfo Create(string outputDirectory, string? sourceSavePath = null)
    {
        var savePath = Path.Combine(outputDirectory, "SaveGame_S1ForkliftSmoke");
        var templatePath = string.IsNullOrWhiteSpace(sourceSavePath)
            ? Path.Combine(Application.streamingAssetsPath, "DefaultSave")
            : Path.GetFullPath(sourceSavePath);
        if (!Directory.Exists(templatePath))
            throw new DirectoryNotFoundException($"Smoke save source folder not found: {templatePath}");

        if (Directory.Exists(savePath))
            Directory.Delete(savePath, true);
        CopyDirectory(templatePath, savePath);

        if (string.IsNullOrWhiteSpace(sourceSavePath))
        {
            File.WriteAllText(
                Path.Combine(savePath, "Game.json"),
                "{\"DataType\":\"GameData\",\"DataVersion\":0,\"GameVersion\":\"" +
                Application.version +
                "\",\"OrganisationName\":\"S1 Forklift Smoke\",\"Seed\":7337," +
                "\"Settings\":{\"ConsoleEnabled\":false}}");
            File.WriteAllText(
                Path.Combine(savePath, "Metadata.json"),
                "{\"DataType\":\"MetaData\",\"DataVersion\":0,\"GameVersion\":\"" +
                Application.version +
                "\",\"CreationDate\":null,\"LastPlayedDate\":null,\"CreationVersion\":\"" +
                Application.version +
                "\",\"LastSaveVersion\":\"" +
                Application.version +
                "\",\"PlayTutorial\":false}");
        }

        var metadata = new S1MetaData(
            (S1DateTimeData?)null,
            (S1DateTimeData?)null,
            Application.version,
            Application.version,
            false);

        return new S1SaveInfo(
            savePath,
            -1,
            "S1 Forklift Smoke",
            GetNow(),
            GetNow(),
            0f,
            Application.version,
            metadata);
    }

    private static S1DateTime GetNow()
    {
#if IL2CPP
        return new S1DateTime(DateTime.Now.Ticks);
#else
        return DateTime.Now;
#endif
    }

    private static void CopyDirectory(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(targetPath);
        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourcePath, targetPath));

        foreach (var file in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                File.Copy(file, file.Replace(sourcePath, targetPath), true);
        }
    }
}
