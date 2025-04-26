using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.Plotting.BlockDiagram;

public class BlockDataService : IBlockDataService
{
    // ===== Instance Variables ===== //
    private readonly GraphDataModel _graphDataModel;
    private int _uniqueVars = 1;
    
    // ===== Constructor ===== //
    public BlockDataService(GraphDataModel graphDataModel)
    {
        // Take an instance of a graph data model via constructor injection
        _graphDataModel = graphDataModel;
    }
    
    // ===== API Methods ===== //
    public GraphDataModel GraphData
    {
        // ReSharper disable once InconsistentlySynchronizedField <-- Rider IDE comment, ignore.
        get => _graphDataModel;
    }

    public int UniqueVars
    {
        get => _uniqueVars;
        set => _uniqueVars = value;
    }
    
    public double[] ExtractVariableValues()
    {
        // List to contain latest value for each variable:
        // [Var1, Var2, ... Var_uniqueVars]
        double[] variableValues = new double[_uniqueVars];

        // Track which variables we've found so we can stop early.
        bool[] varFound = new bool[_uniqueVars];
        int remaining = _uniqueVars; // Number of remaining variables to find value for.
        
        // Lock datamodel to extract
        lock (_graphDataModel)
        {
            // Go backwards in the data array to find most recent
            for (int i = _graphDataModel.YData.Count - 1; i >= 0 && remaining > 0; i--)
            {
                int varIndex = i % _uniqueVars;
                
                // If variable already found ,skip
                if (varFound[varIndex]) continue;
                
                // Variable's last value found. Add into result and mark as found.
                variableValues[varIndex] = _graphDataModel.YData[i];
                varFound[varIndex] = true;
                remaining--;
            }
        }
        
        // Return the resulting list
        return variableValues;
    }
    
    public void ClearData()
    {
        lock(_graphDataModel)
            _graphDataModel.Clear();
    }
}