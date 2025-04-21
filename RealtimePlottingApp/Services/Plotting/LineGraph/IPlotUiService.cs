using System.Collections.ObjectModel;
using RealtimePlottingApp.Models;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace RealtimePlottingApp.Services.Plotting.LineGraph;

/// <summary>
/// Encapsulates direct manipulation of the ScottPlot UI:
/// drawing signals, markers, legends, axises, and trigger lines.
/// </summary>
public interface IPlotUiService
{
    /// <summary>
    /// The ScottPlot plot assigned from the View.
    /// </summary>
    AvaPlot? LinePlot { get; set; }

    /// <summary>
    /// The current list of variable models.
    /// </summary>
    ObservableCollection<IVariableModel>? PlotConfigVariables { get; set; }

    /// <summary>
    /// True when full‐history mode is on.
    /// </summary>
    bool PlotFullHistory { get; set; }

    /// <summary>
    /// X-Axis width when not in history mode.
    /// </summary>
    double WindowWidth { get; set; }
    
    /// <summary>
    /// Horizontal line representing the trigger level, if it has been set.
    /// </summary>
    HorizontalLine? TriggerLevel { get; }
    
    /// <summary>
    /// Locks the trigger level to some capacity (dragging, resetting, etc.)
    /// </summary>
    bool LockTriggerLevel { get; set; }

    /// <summary>
    /// Redraws the entire graph (signals + trigger marker (if one exists) + legend).
    /// </summary>
    void UpdateGraphUI(double[] xDataDouble, double[] yDataDouble, 
        int triggerIndex, int globalTriggerIndex);

    /// <summary>
    /// Adjusts axis limits or autoscale depending on history mode, such that
    /// the view updates to show the relevant data at a given time.
    /// </summary>
    void AdjustGraphView(double[] xDataDouble, int triggerIndex);

    /// <summary>
    /// Adds, moves or removes the draggable trigger line.
    /// </summary>
    void SetTriggerLevel(GraphDataModel graphData, bool placeTriggerAbove, 
        double offset, string startText, bool isEnabled);
}