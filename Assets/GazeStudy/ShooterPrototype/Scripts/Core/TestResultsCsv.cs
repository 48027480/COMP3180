using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// Each play session gets its own Save/TestResult{number}.csv, numbered one above the largest already
/// there, with one row per test. The folder sits next to Assets in the editor and next to the
/// executable in a build (persistent data on a standalone headset).
public static class TestResultsCsv
{
    const string FileName = "TestResult";
    const string Header = "DateTime,Area,Shots,Hits,AccuracyPercent,DurationSeconds,Completed";

    static string sessionPath;

    public static void BeginSession()
    {
        string root = Application.isMobilePlatform ? Application.persistentDataPath : Path.Combine(Application.dataPath, "..");
        string folder = Path.GetFullPath(Path.Combine(root, "Save"));
        Directory.CreateDirectory(folder);

        sessionPath = Path.Combine(folder, $"{FileName}{LargestNumber(folder) + 1}.csv");
        File.WriteAllText(sessionPath, Header + Environment.NewLine);
    }

    public static string Save(string area, int shots, int hits, float accuracy, float duration, bool completed)
    {
        string row = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss},\"{1}\",{2},{3},{4:0.##},{5:0.##},{6}",
            DateTime.Now, area, shots, hits, accuracy, duration, completed);

        try
        {
            File.AppendAllText(sessionPath, row + Environment.NewLine);
        }
        catch (IOException)
        {
            // Excel locks a file it has open, and the result must not be lost.
            string locked = sessionPath;
            BeginSession();
            File.AppendAllText(sessionPath, row + Environment.NewLine);
            Debug.LogWarning($"{locked} is in use, so results now go to {sessionPath}");
        }
        return sessionPath;
    }

    static int LargestNumber(string folder)
    {
        int largest = 0;
        foreach (string file in Directory.GetFiles(folder, FileName + "*.csv"))
        {
            Match match = Regex.Match(Path.GetFileName(file), $@"^{FileName}(\d+)\.csv$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number)) largest = Mathf.Max(largest, number);
        }
        return largest;
    }
}
