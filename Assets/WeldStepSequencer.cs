using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Anything that can report "this step is done" to a <see cref="WeldStepSequencer"/>.
/// Implemented by snap/weld/flip components so they can be dropped in as a step's completion clause.
/// </summary>
public interface IWeldStepCompletable
{
    /// <summary>True when the work this component represents is finished.</summary>
    bool IsStepComplete { get; }
}

/// <summary>
/// Strictly ordered step machine. Each step has a GRAB CLAUSE (<see cref="Step.onStepBegin"/> — cue actions
/// fired when the step becomes active, e.g. reveal a ghost, enable a Grabbable, recolor weld dots) and a
/// COMPLETION CLAUSE (<see cref="Step.completionSource"/> — a component whose <see cref="IWeldStepCompletable.IsStepComplete"/>
/// marks the step done, or a manual <see cref="CompleteCurrentStep"/> call). A step cannot begin until the
/// previous one completes, so nothing is cued out of order.
/// </summary>
public class WeldStepSequencer : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("Label for readability / status readout.")]
        public string label;

        [Tooltip("GRAB CLAUSE: actions fired when this step becomes active — cue the user (reveal ghost, enable Grabbable, recolor weld dots, etc.).")]
        public UnityEvent onStepBegin;

        [Tooltip("COMPLETION CLAUSE: component whose IsStepComplete marks this step done (weldbar/refpiece/weld sequences/flips/assembly). Leave empty to complete only via CompleteCurrentStep().")]
        public MonoBehaviour completionSource;

        [Tooltip("Fired the moment this step completes, right before the next begins (hide ghost, lock object, etc.).")]
        public UnityEvent onStepComplete;

        [NonSerialized] public IWeldStepCompletable resolvedCompletion;
        [NonSerialized] public bool manualComplete;
    }

    [Tooltip("Ordered steps. Step 0 begins on Start (if Auto Start is on); each completion cues the next.")]
    public Step[] steps;

    [Tooltip("Begin the first step automatically on Start. Turn off to trigger StartSequence() yourself.")]
    public bool autoStartFirstStep = true;

    [Tooltip("Fired once after the final step completes.")]
    public UnityEvent onAllStepsComplete;

    int _current = -1;
    bool _running;
    bool _allDone;

    void Start()
    {
        ResolveCompletionSources();
        if (autoStartFirstStep)
            BeginStep(0);
    }

    void ResolveCompletionSources()
    {
        if (steps == null)
            return;

        foreach (var s in steps)
        {
            if (s == null)
                continue;

            // Accept a completable directly, or any component on the same GameObject that implements it
            // (so dragging e.g. the Grabbable instead of the refpiece still resolves).
            s.resolvedCompletion = s.completionSource as IWeldStepCompletable;
            if (s.resolvedCompletion == null && s.completionSource != null)
                s.resolvedCompletion = s.completionSource.GetComponent<IWeldStepCompletable>();

            if (s.completionSource != null && s.resolvedCompletion == null)
                Debug.LogWarning(
                    $"{name}: step '{s.label}' completion source '{s.completionSource.GetType().Name}' (and its GameObject) has no IWeldStepCompletable; it will only complete via CompleteCurrentStep().",
                    this);
        }
    }

    void Update()
    {
        if (!_running || _allDone)
            return;

        if (steps == null || _current < 0 || _current >= steps.Length)
            return;

        Step step = steps[_current];
        if (step == null)
        {
            AdvanceStep();
            return;
        }

        bool complete = step.manualComplete ||
                        (step.resolvedCompletion != null && step.resolvedCompletion.IsStepComplete);
        if (complete)
            AdvanceStep();
    }

    void BeginStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Length)
        {
            FinishAll();
            return;
        }

        _current = index;
        _running = true;

        Step step = steps[index];
        if (step != null)
        {
            step.manualComplete = false;
            step.onStepBegin?.Invoke();
        }
    }

    void AdvanceStep()
    {
        Step step = (_current >= 0 && _current < steps.Length) ? steps[_current] : null;
        step?.onStepComplete?.Invoke();

        int next = _current + 1;
        if (next >= steps.Length)
            FinishAll();
        else
            BeginStep(next);
    }

    void FinishAll()
    {
        _running = false;
        _allDone = true;
        _current = steps != null ? steps.Length : 0;
        onAllStepsComplete?.Invoke();
    }

    /// <summary>(Re)start the sequence from the first step.</summary>
    public void StartSequence()
    {
        _allDone = false;
        ResolveCompletionSources();
        BeginStep(0);
    }

    /// <summary>Force the current step to complete (manual completion clause).</summary>
    public void CompleteCurrentStep()
    {
        if (!_running || _allDone)
            return;

        if (_current >= 0 && _current < steps.Length && steps[_current] != null)
            steps[_current].manualComplete = true;
    }

    /// <summary>Active step index, or steps.Length once finished.</summary>
    public int CurrentStepIndex => _current;

    /// <summary>True while a step is active and the sequence has not finished.</summary>
    public bool IsRunning => _running && !_allDone;

    /// <summary>True once every step has completed.</summary>
    public bool AllStepsComplete => _allDone;

    /// <summary>Label of the active step (empty when finished / not started).</summary>
    public string CurrentStepLabel =>
        (steps != null && _current >= 0 && _current < steps.Length && steps[_current] != null)
            ? steps[_current].label
            : string.Empty;
}
