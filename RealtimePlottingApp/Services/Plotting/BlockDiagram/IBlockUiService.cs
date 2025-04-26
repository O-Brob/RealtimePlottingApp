using System.Collections.ObjectModel;
using RealtimePlottingApp.Models;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.Services.Plotting.BlockDiagram;

/// <summary>
/// Encapsulates direct manipulation of the ScottPlot UI:
/// drawing blocks, adjusting axises...
/// </summary>
public interface IBlockUiService
{
    /// <summary>
    /// The ScottPlot plot assigned from the View.
    /// </summary>
    AvaPlot? BlockPlot { get; set; }
    
    /// <summary>
    /// The current list of variable models.
    /// </summary>
    ObservableCollection<IVariableModel>? PlotConfigVariables { get; set; }
    
    /// <summary>
    /// Redraws the block diagram (blocks + legend).
    /// </summary>
    void UpdateBlockUI(double[] data);
}