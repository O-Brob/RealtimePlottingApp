using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.Plotting.LineGraph;

/// <summary>
/// Manages buffering & splitting of data into subarrays
/// for plotting, to determine how much of the total data
/// should be displayed, and when.
/// </summary>
public interface IGraphDataService
{
    /// <summary>
    /// The underlying data model that data is written to.
    /// </summary>
    GraphDataModel GraphData { get; }

    /// <summary>
    /// The number of interleaved variables in the graph data arrays.
    /// </summary>
    int UniqueVars { get; set; }

    /// <summary>
    /// Width for the X-axis, for dynamic scaling of the view window
    /// </summary>
    double WindowWidth { get; set; }

    /// <summary>
    /// Flag indicating whether the entire data history should show.
    /// True once SetFullHistory() has been set true.
    /// </summary>
    bool IsFullHistory { get; }

    /// <summary>
    /// Clears all data.
    /// </summary>
    void ClearData();

    /// <summary>
    /// Sets full‑history mode (no more sliding window,
    /// but instead display entire data history).
    /// </summary>
    void SetFullHistory(bool fullHistory);

    /// <summary>
    /// Produces X/Y sub-arrays to hand off to the plot‑UI.
    /// Additionally provides a local trigger index representing the index in the
    /// subarray for which a trigger has occurred (or -1 if no trigger found)
    /// </summary>
    void GetSubData(out double[] xDataDouble, out double[] yDataDouble, out int localTriggerIndex,
        int? currentTriggerIndex, int? lastTriggerIndex, TriggerMode triggerMode);
}