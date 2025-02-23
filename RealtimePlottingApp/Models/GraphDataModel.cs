using System.Collections.Generic;

namespace RealtimePlottingApp.Models;

/// <summary>
/// Model representing data lists for a 2D plotting of X and Y values.
/// </summary>
public class GraphDataModel
{
    // Private instance variables which stores the data
    private List<uint> _XData;
    private List<uint> _YData;
    
    /// <summary>
    /// Returns access to a read-only representation of the X values.
    /// </summary>
    public IReadOnlyList<uint> XData => _XData.AsReadOnly();
    
    /// <summary>
    /// Returns access to a read-only representation of the Y values.
    /// </summary>
    public IReadOnlyList<uint> YData => _YData.AsReadOnly();

    /// <summary>
    /// Constructor for a new graph data model
    /// </summary>
    public GraphDataModel()
    {
        _XData = new List<uint>();
        _YData = new List<uint>();
    }

    /// <summary>
    /// Clone constructor for a graph data model 
    /// </summary>
    /// <param name="xData">A list of data values for the x-axis</param>
    /// <param name="yData">A list of data values for the y-axis</param>
    public GraphDataModel(List<uint> xData, List<uint> yData)
    {
        _XData = new List<uint>(xData);
        _YData = new List<uint>(yData);
    }

    /// <summary>
    /// Adds a new data point to both the X and Y collections of data
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public void AddPoint(uint x, uint y)
    {
        _XData.Add(x);
        _YData.Add(y);
    }

    public void Clear()
    {
        _XData.Clear();
        _YData.Clear();
    }
}