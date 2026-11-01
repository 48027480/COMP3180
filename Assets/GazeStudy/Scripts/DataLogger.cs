using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace COMP3180.GazeStudy
{
    public struct TrialRecord
    {
        public int Participant;
        public int BlockIndex;          // 0-based position in the session, practice included
        public Technique Technique;
        public TargetSize Size;
        public bool Practice;
        public int TrialInBlock;        // 1-based
        public int TargetIndex;
        public int SelectedIndex;       // -1 when nothing was hovered
        public TrialOutcome Outcome;
        public double TimeMs;           // target onset -> trigger press
        public float AngularErrorDeg;   // angle between ray and the cued target centre at press time
        public GazeSource Source;
        public bool EyeDeviceAvailable;
        public float TargetSizeDeg;
        public float DistanceM;
    }

    /// <summary>One CSV per session, flushed after every trial so a crash loses nothing.</summary>
    public class DataLogger : MonoBehaviour
    {
        const string Header =
            "participant,timestamp,block_index,technique,target_size,practice,trial_in_block," +
            "target_index,selected_index,outcome,time_ms,angular_error_deg,gaze_source," +
            "eye_device_available,target_size_deg,distance_m";

        StreamWriter writer;
        public string FilePath { get; private set; } = "";
        public string Folder => Path.Combine(Application.persistentDataPath, "COMP3180_Data");

        public void Begin(int participant)
        {
            End();
            Directory.CreateDirectory(Folder);
            FilePath = Path.Combine(Folder, $"P{participant:00}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            writer.WriteLine(Header);
            writer.Flush();
            Debug.Log("[DataLogger] Writing to " + FilePath);
        }

        public void Log(TrialRecord r)
        {
            if (writer == null) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(160);
            sb.Append(r.Participant).Append(',')
              .Append(DateTime.Now.ToString("o", inv)).Append(',')
              .Append(r.BlockIndex).Append(',')
              .Append(r.Technique).Append(',')
              .Append(r.Size).Append(',')
              .Append(r.Practice ? 1 : 0).Append(',')
              .Append(r.TrialInBlock).Append(',')
              .Append(r.TargetIndex).Append(',')
              .Append(r.SelectedIndex).Append(',')
              .Append(r.Outcome).Append(',')
              .Append(r.TimeMs.ToString("F1", inv)).Append(',')
              .Append(r.AngularErrorDeg.ToString("F3", inv)).Append(',')
              .Append(r.Source).Append(',')
              .Append(r.EyeDeviceAvailable ? 1 : 0).Append(',')
              .Append(r.TargetSizeDeg.ToString("F2", inv)).Append(',')
              .Append(r.DistanceM.ToString("F2", inv));
            writer.WriteLine(sb.ToString());
            writer.Flush();
        }

        public void End()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        [ContextMenu("Open data folder")]
        void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace("\\", "/"));
        }

        void OnDestroy() { End(); }
    }
}
