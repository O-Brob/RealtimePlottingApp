using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using RealtimePlottingApp.Models;
using ScottPlot;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.Services.Plotting.BlockDiagram;

public class BlockUiService :IBlockUiService
{
    // ===== Instance Variables ===== //
    // Palette for predictable & consistent color assignment regardless of Trigger lines, etc.
    // Uses a 25-color palette adapted from Tsitsulin's 12-color xgfs palette
    // Aims to help distinguishing the colors for people with color vision deficiency and when printed B&W.
    // https://tsitsul.in/blog/coloropt/
    private readonly IPalette _palette = new ScottPlot.Palettes.Tsitsulin();
    private double maxValueY;
    
    // ===== Constructor ===== //
    
    // ===== API Methods ===== //
    public AvaPlot? BlockPlot { get; set; }
    public ObservableCollection<IVariableModel>? PlotConfigVariables { get; set; }
        
    public void UpdateBlockUI(double[] data)
    {
        // Update UI asynchronously on Avalonia's UI Thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Return early if plot is not instantiated
            if (BlockPlot == null) return;
            
            // Clear previous blocks:
            BlockPlot.Plot.Clear();
            
            // Add new blocks using the given data, position them accordingly and configure visuals
            for (int i = 0; i < data.Length; i++)
            {
                var bar = BlockPlot.Plot.Add.Bar(0 + ( 0.85 * i ), data[i]);
                
                // Customize bar. Set color, legend, visibility..
                bar.Color = _palette.GetColor(i % _palette.Colors.Length); 
                bar.LegendText = PlotConfigVariables?.Count > i
                    ? PlotConfigVariables[i].Name : $"Var {i+1}"; // Fallback
                bar.Color = _palette.GetColor(i % _palette.Colors.Length); // Each var nr. has a preset color. 
                   
                // Do not plot the variable if visibility is unchecked by the user in the UI
                if (PlotConfigVariables?.Count > i && !PlotConfigVariables[i].IsChecked)
                    bar.IsVisible = false;

            }
            
            // Adjust the view-window automatically and refresh UI
            AdjustGraphView(data);
            BlockPlot.Refresh();
        });
    }

    // ===== UI Helper ===== //
    private void AdjustGraphView(double[] data)
    {
        // Check if a new high has been reached and set it as max on Y axis view.
        // Used to prevent axis from re-scaling too much.
        if (data.Max() > maxValueY)
            maxValueY = data.Max() * 1.1; // Adjust top of window to be 10% above max point

        // Update Axis Ranges
        BlockPlot?.Plot.Axes.SetLimitsY(-1, maxValueY);
        BlockPlot?.Plot.Axes.AutoScaleX();
    }
}