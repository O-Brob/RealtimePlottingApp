using System.Collections.ObjectModel;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.DataChannels;
using ScottPlot.Plottables;
using Timer = System.Timers.Timer;

namespace RealtimePlottingApp.Services.Plotting.LineGraph;

/// <summary>
/// Contains trigger detection and control logic based on data conditions and trigger-modes.
/// </summary>
public interface ITriggerService
{
    /// <summary>
    /// The last global index at which a trigger occurred (or –1 if none has occurred).
    /// </summary>
    int LastTriggerIndex { get; }

    /// <summary>
    /// True once a single‑trigger has fired and entered "the trigger view".
    /// </summary>
    bool PlotTriggerView { get; }

    /// <summary>
    /// Set trigger mode.
    /// TODO: Make into ENUM in Models/ rather than string
    /// </summary>
    string Mode { get; set; }

    /// <summary>
    /// Called when the user enables/disables the trigger checkbox.
    /// Initializes the start index.
    /// </summary>
    void EnableTrigger(int currentDataCount);

    /// <summary>
    /// Called when the trigger line is manually moved by dragging.
    /// </summary>
    void OnTriggerMoved(int currentDataCount);

    /// <summary>
    /// Resets all internal trigger states.
    /// </summary>
    void ResetTrigger();

    /// <summary>
    /// Checks for a rising‑edge trigger. Returns the global index for it or –1 if not found.
    /// </summary>
    int CheckForTrigger(
        GraphDataModel graphData, ObservableCollection<IVariableModel>? plotConfigVariables,
        int uniqueVars, HorizontalLine? triggerLevel);

    /// <summary>
    /// Executes the trigger behavior.
    /// </summary>
    void HandleTrigger(IDataChannel? dataChannel, Timer timer, GraphDataModel graphData);
}