namespace COMP3180.GazeStudy
{
    public enum Technique { Controller, GazeTrigger }
    public enum TargetSize { Small, Large }

    /// <summary>Which pose the selection ray is cast from.</summary>
    public enum RayMode { Eye, Controller, Head }

    /// <summary>What actually produced the ray (logged per trial so the data stays honest).</summary>
    public enum GazeSource { EyeTracker, HeadFallback, DesktopMouse, ControllerRay }

    public enum TrialOutcome { Correct, WrongTarget, Miss }

    public class BlockSpec
    {
        public Technique Technique;
        public TargetSize Size;
        public bool IsPractice;
        public int Trials;
        public int DisplayNumber; // 1..4 for real blocks, 0 for practice
    }
}
