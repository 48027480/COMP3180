using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// Appends one row per test to Save/TestResults.csv. The folder sits next to Assets in the editor and
/// next to the executable in a build (persistent data on a standalone headset).
public static class TestResultsCsv
{
    const string FileName = "TestResults";
    const string Header = "DateTime,Area,Shots,Hits,AccuracyPercent,DurationSeconds,Completed";

    public static string Save(string area, int shots, int hits, float accuracy, float duration, bool completed)
    {
        string root = Application.isMobilePlatform ? Application.persistentDataPath : Path.Combine(Application.dataPath, "..");
        string folder = Path.GetFullPath(Path.Combine(root, "Save"));
        Directory.CreateDirectory(folder);

        string row = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss},\"{1}\",{2},{3},{4:0.##},{5:0.##},{6}",
            DateTime.Now, area, shots, hits, accuracy, duration, completed);

        string path = Path.Combine(folder, FileName + ".csv");
        try
        {
            Append(path, row);
        }
        catch (IOException)
        {
            // Excel locks a file it has open, and the result must not be lost.
            path = Path.Combine(folder, $"{FileName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            Append(path, row);
            Debug.LogWarning("TestResults.csv is in use, so this result was saved to " + path);
        }
        return path;
    }

    static void Append(string path, string row)
    {
        bool isNew = !File.Exists(path);
        using (var writer = new StreamWriter(path, true))
        {
            if (isNew) writer.WriteLine(Header);
            writer.WriteLine(row);
        }
    }
}
