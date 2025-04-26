using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.Plotting.BlockDiagram;

/// <summary>
/// Buffers or receives the latest values for each variable,
/// exposes only a double array of values for extraction.
/// </summary>
public interface IBlockDataService
{
    /// <summary>
    /// The underlying data model that data is written to.
    /// </summary>
    GraphDataModel GraphData { get; }
    
    /// <summary>
    /// Represents the number of unique variables
    /// to be extracted from the underlying datastructure.
    /// </summary>
    int UniqueVars { get; set; }
    
    /// <summary>
    /// The most recent value for each variable,
    /// in display order (Var1, Var2...).
    /// </summary>
    double[] ExtractVariableValues();
    
    /// <summary>
    /// Clears all data in the underlying data structures.
    /// </summary>
    void ClearData();
}