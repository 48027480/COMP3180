using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

/// Playground 9. A panel with number buttons, a slider and a keyboard grid. Each block gives one task
/// ("select 3 > 7 > 1", "type GAZE", "set the slider to 6") with one input method and one button size:
///   Dwell                  -> hold the gaze on a button
///   Gaze + click           -> look at a button and press grab
///   Controller ray + click -> aim the controller laser and press grab (headset only)
/// Logs every selection with its time, whether it was right, and the method and button size in use.
public class GazeMenuArea : GazeTestArea
{
    enum Method { Dwell, GazeClick, ControllerClick }
    enum Task { Numbers, Word, Slider }

    const float HoverSeconds = 0.25f;
    const float KeyDepth = 0.05f;
    const int LettersPerRow = 7;
    const int SliderStops = 11;
    static readonly string[] MethodNames = { "Dwell", "Gaze + click", "Controller ray + click" };
    static readonly string[] Words = { "GAZE", "VIEW", "SIGHT", "FOCUS" };
    static readonly Color KeyColor = new Color(0.15f, 0.4f, 0.65f);
    static readonly Color SliderColor = new Color(0.45f, 0.35f, 0.7f);

    [SerializeField] float distance = 3.5f;
    [SerializeField] float dwellSeconds = 0.8f;
    [SerializeField] float raise = 0.2f;
    [SerializeField] Vector2 largeKey = new Vector2(0.26f, 0.06f); // size, gap
    [SerializeField] Vector2 smallKey = new Vector2(0.16f, 0.04f);

    readonly List<Method> methods = new List<Method>();
    readonly List<string> sequence = new List<string>();
    TextMeshPro prompt;
    Method method;
    Task task;
    Vector2 key;
    string taskText;
    bool gazeBefore;
    int block, step, selections, errors, tasksDone;
    float taskStart, lastSelect;

    protected override void Begin()
    {
        gazeBefore = selector.GazeEnabled;
        methods.Clear();
        methods.Add(Method.Dwell);
        methods.Add(Method.GazeClick);
        if (XRSettings.isDeviceActive) methods.Add(Method.ControllerClick); // desktop has no controller ray

        block = selections = errors = tasksDone = 0;
        StartBlock();
    }

    protected override void End() => selector.SetGazeEnabled(gazeBefore);

    void StartBlock()
    {
        method = methods[block % methods.Count];
        key = (block / methods.Count) % 2 == 0 ? largeKey : smallKey;
        task = (Task)((block + block / 3) % 3);
        selector.SetGazeEnabled(method != Method.ControllerClick);

        BuildPanel();

        sequence.Clear();
        step = 0;
        switch (task)
        {
            case Task.Numbers:
                while (sequence.Count < 3)
                {
                    string id = "N" + Random.Range(1, 10);
                    if (!sequence.Contains(id)) sequence.Add(id);
                }
                taskText = "Select " + Display(" > ", sequence.Count);
                break;
            case Task.Word:
                foreach (char letter in Words[Random.Range(0, Words.Length)]) sequence.Add("K" + letter);
                taskText = "Type " + Display("", sequence.Count);
                break;
            default:
                sequence.Add("S" + Random.Range(0, SliderStops));
                taskText = "Set the slider to " + Display("", 1);
                break;
        }

        taskStart = lastSelect = Time.time;
        UpdatePrompt();
    }

    void BuildPanel()
    {
        ClearStimuli();
        float pitch = key.x + key.y;
        float top = raise + 2.5f * pitch;
        prompt = Label("", Point(0f, top + pitch + 0.3f, distance), 2f);

        for (int i = 1; i <= 9; i++)
            Key("N" + i, i.ToString(), (i - 5) * pitch, top, key.x, KeyColor);

        for (int i = 0; i < SliderStops; i++)
            Key("S" + i, i.ToString(), (i - (SliderStops - 1) * 0.5f) * pitch, top - pitch, key.x * 0.5f, SliderColor);

        for (int i = 0; i < 26; i++)
        {
            int row = i / LettersPerRow, column = i % LettersPerRow;
            int inRow = Mathf.Min(LettersPerRow, 26 - row * LettersPerRow);
            char letter = (char)('A' + i);
            Key("K" + letter, letter.ToString(), (column - (inRow - 1) * 0.5f) * pitch, top - (2 + row) * pitch, key.x, KeyColor);
        }
    }

    void Key(string id, string text, float x, float y, float height, Color color)
    {
        Spawn(id, PrimitiveType.Cube, Point(x, y, distance), new Vector3(key.x, height, KeyDepth), color);
        Label(text, Point(x, y, distance - KeyDepth), Mathf.Min(key.x, height * 1.6f) * 5f);
    }

    protected override void Tick()
    {
        GazeTarget picked;
        if (method == Method.Dwell) picked = TrackDwell(dwellSeconds);
        else
        {
            TrackDwell(HoverSeconds); // only for the highlight
            picked = Confirm ? Looked : null;
        }
        if (picked == null) return;

        string expected = sequence[step];
        bool correct = picked.Id == expected;
        selections++;
        if (!correct) errors++;

        Trial(correct);
        Log("Select", picked.Id, picked.transform.position, correct, Time.time - lastSelect, Difficulty(), $"expected={expected};step={step}");
        lastSelect = Time.time;
        if (!correct) return;

        step++;
        if (step < sequence.Count)
        {
            UpdatePrompt();
            return;
        }

        tasksDone++;
        Log("TaskDone", "", null, true, Time.time - taskStart, Difficulty());
        block++;
        StartBlock();
    }

    string Difficulty() => $"method={method};keySize={F(key.x)};gap={F(key.y)};task={task}";

    /// The first count items of the sequence as the player sees them, without the id letter.
    string Display(string separator, int count)
    {
        string text = "";
        for (int i = 0; i < count; i++) text += (i > 0 ? separator : "") + sequence[i].Substring(1);
        return text;
    }

    void UpdatePrompt() => prompt.text = $"{MethodNames[(int)method]}:  {taskText}\nDone: {Display(" ", step)}";

    protected override string Summary() => $"tasksDone={tasksDone};selections={selections};errors={errors}";
}
