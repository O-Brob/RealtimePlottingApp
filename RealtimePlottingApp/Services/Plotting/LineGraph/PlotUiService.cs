using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using RealtimePlottingApp.Models;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace RealtimePlottingApp.Services.Plotting.LineGraph;

public class PlotUiService : IPlotUiService
{
    // --- Graph element references & configuration --- //
    public AvaPlot? LinePlot { get; set; }
    public ObservableCollection<IVariableModel>? PlotConfigVariables { get; set; }
    public bool PlotFullHistory { get; set; }
    public double WindowWidth { get; set; } = 75;
    public bool LockTriggerLevel { get; set; } = false;

    // Holds trigger level when enabled, else null
    private HorizontalLine? _triggerLevel;

    // Palette for predictable & consistent color assignment regardless of Trigger lines, etc.
    // Uses a 25-color palette adapted from Tsitsulin's 12-color xgfs palette
    // Aims to help distinguishing the colors for people with color vision deficiency and when printed B&W.
    // https://tsitsul.in/blog/coloropt/
    private readonly IPalette _palette = new ScottPlot.Palettes.Tsitsulin();

    public HorizontalLine? TriggerLevel
    {
        get => _triggerLevel;
    }

    public void UpdateGraphUI(double[] xDataDouble, double[] yDataDouble, 
        int triggerIndex, int globalTriggerIndex)
    {
        // Update UI asynchronously on Avalonia's UI Thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (LinePlot == null) return;
            
            // Check if trigger should toggle between locked/unlocked
            if (_triggerLevel != null)
                _triggerLevel.IsDraggable = !LockTriggerLevel;
            
            LinePlot.Plot.Clear<SignalXY>();
            LinePlot.Plot.Clear<Scatter>();

            // Loop over each variable and extract its data.
            for (int v = 0; v < (PlotConfigVariables?.Count ?? 1); v++)
            {
                // Use LINQ's index-aware overload of the Where method to filter data by % operations on index.
                double[] xVar = xDataDouble.Where((_, idx) => idx % (PlotConfigVariables?.Count ?? 1) == v).ToArray();
                double[] yVar = yDataDouble.Where((_, idx) => idx % (PlotConfigVariables?.Count ?? 1) == v).ToArray();

                if (xVar.Length <= 0) continue;

                // SignalXY Preconditions, which when met allows for greater performance than ScatterLine Plotting:
                // "New data points must have an X value that is greater to or equal to the previous one."
                SignalXY signal = LinePlot.Plot.Add.SignalXY(xVar, yVar);
                signal.LegendText = PlotConfigVariables?.Count > v
                    ? PlotConfigVariables[v].Name : $"Var {v+1}"; // Fallback
                signal.Color = _palette.GetColor(v % _palette.Colors.Length); // Each var nr. has a preset color.

                // Do not plot the variable if visibility is unchecked by the user in the UI
                if (PlotConfigVariables?.Count > v && !PlotConfigVariables[v].IsChecked)
                    signal.IsVisible = false;

                // If a triggerIndex is plotted, mark it such that the user can see which point triggered.
                if (triggerIndex >= 0 && globalTriggerIndex % (PlotConfigVariables?.Count ?? 1) == v)
                {
                    // (Local Index for aligning index to interleaved arrays)
                    int localIndex = (PlotConfigVariables == null || PlotConfigVariables.Count == 1)
                        ? triggerIndex
                        : triggerIndex / PlotConfigVariables.Count;

                    if (localIndex > 0 && localIndex < xVar.Length)
                    {
                        // Triggered point
                        double triggerX = xVar[localIndex];
                        double triggerY = yVar[localIndex];

                        // Point before trigger (for calculating (x,y) interpolations)
                        double xBefore = xVar[localIndex - 1];
                        double yBefore = yVar[localIndex - 1];

                        // Check whether we can find intersection/interpolaton of rising edge and triggerlevel
                        if (_triggerLevel != null &&
                            _triggerLevel.Y > yBefore &&
                            _triggerLevel.Y < triggerY &&
                            (triggerX - xBefore) != 0)
                        {
                            // Find intersection of triggerLevel and the line between trigger point and prev. point.
                            double slope = (triggerY - yBefore) / (triggerX - xBefore);
                            double intersectionX = triggerX + (_triggerLevel.Y - triggerY) / slope;

                            // Place trigger point marker on the rising edge where the triggerLevel was passed
                            // for better visual representation.
                            var marker = LinePlot.Plot.Add.Scatter(intersectionX, _triggerLevel.Y, Colors.Black);
                            marker.MarkerShape = MarkerShape.FilledDiamond;
                            marker.MarkerSize = 6;
                        }
                        else // Fallback
                        {
                            // Add the marker at the trigger point
                            var marker = LinePlot.Plot.Add.Scatter(triggerX, triggerY, color: Colors.Black);
                            marker.MarkerShape = MarkerShape.FilledDiamond;
                            marker.MarkerSize = 6;
                        }
                    }
                }
            }

            // Call helper to adjust the graph view for the new data (or trigger points)
            AdjustGraphView(xDataDouble, triggerIndex);

            // Add legend so each variable is identifiable.
            LinePlot.Plot.ShowLegend();
            LinePlot.Refresh();
        });
    }

    public void AdjustGraphView(double[] xDataDouble, int triggerIndex)
    {
        if (!PlotFullHistory && xDataDouble.Length > 0)
        {
            if (triggerIndex >= 0 && triggerIndex < xDataDouble.Length)
            {
                // Center the plot around trigger point
                double triggerX = xDataDouble[triggerIndex];
                LinePlot?.Plot.Axes.SetLimitsX(triggerX - WindowWidth / 2, triggerX + WindowWidth / 2);
            }
            else
            {
                // Make X-Axis limit & "follow" the plotting while not in "history mode"
                double lastX = xDataDouble.Last();
                LinePlot?.Plot.Axes.SetLimitsX(lastX - WindowWidth, lastX);
            }
        }
        else
        {
            // History mode entered, don't set limits and autoscale the plot.
            LinePlot?.Plot.Axes.AutoScale();
        }
    }

    public void SetTriggerLevel(GraphDataModel graphData, bool placeTriggerAbove, 
        double offset, string startText, bool isEnabled)
    {
        // If graphData.YData is empty, we set the trigger level to a default value
        // otherwise we place it depending on the max & minimum values, to reduce
        // risk of accidental "instant trigger"
        double triggerPosition;

        if (graphData.YData.Count == 0)
        {
            // Place it at 10 if placing above or at -10 if placing below
            triggerPosition = placeTriggerAbove ? 10 : -10;
        }
        else
        {
            // Calculate the trigger level position based on the input parameters
            lock (graphData)
            {
                if (placeTriggerAbove)
                {
                    // Set the trigger level slightly above the maximum Y value in the data
                    triggerPosition = graphData.YData.Max() + offset;
                }
                else
                {
                    // Set the trigger level slightly below the minimum Y value in the data
                    triggerPosition = graphData.YData.Min() - offset;
                }
            }
        }

        // Set the trigger level only if it's enabled
        if (isEnabled)
        {
            // Add the horizontal line if it's enabled and doesn't already exist
            if (_triggerLevel == null)
            {
                _triggerLevel = LinePlot?.Plot.Add.HorizontalLine(triggerPosition);
                if (_triggerLevel != null)
                {
                    _triggerLevel.IsDraggable = true;
                    _triggerLevel.IsVisible = true;
                    _triggerLevel.Text = startText;
                    _triggerLevel.LinePattern = LinePattern.Dashed;
                    _triggerLevel.LineColor = Color.FromHex("#2987CC"); // Same blue as UI
                    _triggerLevel.LabelBackgroundColor = Color.FromHex("#2987CC"); // -::-
                }
            }
        }
        else
        {
            // Remove the horizontal line if it's disabled and it exists
            if (_triggerLevel != null)
            {
                LinePlot?.Plot.Remove(_triggerLevel);
                _triggerLevel = null; // Indicate that the triggerLevel does not exist anymore.
            }
        }

        // Refresh the plot to reflect the changes
        LinePlot?.Refresh();
    }
}