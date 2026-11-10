using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// Each play session that runs a gaze test gets its own Save/GazeLog{number}.csv, with one row per
/// event and per gaze sample. Rows are held in memory and written when a test ends.
public static class GazeLogCsv
{
    const string FileName = "GazeLog";
    const string Header = "Timestamp,TestSeconds,Area,Event,AimSource," +
                          "GazeOriginX,GazeOriginY,GazeOriginZ,GazeDirX,GazeDirY,GazeDirZ," +
                          "TargetId,TargetX,TargetY,TargetZ,Hit,ReactionTimeSeconds,AngularErrorDeg,Difficulty,Measures";

    static readonly StringBuilder buffer = new StringBuilder();
    static string path;

    // Needed when Enter Play Mode Options has Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        buffer.Clear();
        path = null;
    }

    /// Negative reaction times and errors, and null values, are written as empty cells.
    public static void Add(string area, string eventName, string aimSource, float testSeconds, Ray? gaze,
        string targetId, Vector3? target, bool? hit, float reaction, float errorDeg, string difficulty, string measures)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        buffer.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", c)).Append(',');
        buffer.Append(testSeconds.ToString("0.###", c)).Append(',');
        buffer.Append('"').Append(area).Append("\",").Append(eventName).Append(',').Append(aimSource).Append(',');
        Append(gaze.HasValue ? gaze.Value.origin : (Vector3?)null);
        Append(gaze.HasValue ? gaze.Value.direction : (Vector3?)null);
        buffer.Append(targetId).Append(',');
        Append(target);
        buffer.Append(hit.HasValue ? hit.Value.ToString() : "").Append(',');
        buffer.Append(reaction >= 0f ? reaction.ToString("0.###", c) : "").Append(',');
        buffer.Append(errorDeg >= 0f ? errorDeg.ToString("0.##", c) : "").Append(',');
        buffer.Append('"').Append(difficulty).Append("\",\"").Append(measures).Append('"').Append(Environment.NewLine);
    }

    public static void Flush()
    {
        if (buffer.Length == 0) return;
        if (path == null) NewFile();

        try
        {
            File.AppendAllText(path, buffer.ToString());
        }
        catch (IOException)
        {
            // Excel locks a file it has open, and the log must not be lost.
            string locked = path;
            NewFile();
            File.AppendAllText(path, buffer.ToString());
            Debug.LogWarning($"{locked} is in use, so the gaze log now goes to {path}");
        }
        buffer.Clear();
        Debug.Log("[GazeTest] Log saved to " + path);
    }

    static void NewFile()
    {
        path = TestResultsCsv.NextFile(FileName);
        File.WriteAllText(path, Header + Environment.NewLine);
    }

    static void Append(Vector3? v)
    {
        if (!v.HasValue)
        {
            buffer.Append(",,,");
            return;
        }
        CultureInfo c = CultureInfo.InvariantCulture;
        buffer.Append(v.Value.x.ToString("0.####", c)).Append(',')
              .Append(v.Value.y.ToString("0.####", c)).Append(',')
              .Append(v.Value.z.ToString("0.####", c)).Append(',');
    }
}
