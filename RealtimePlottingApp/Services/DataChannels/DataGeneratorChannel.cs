using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.DataChannels;

/// <summary>
/// Data Channel for generated data points to be used for generating
/// test data for plotting easily without the need of hardware.
/// </summary>
public class DataGeneratorChannel : IDataChannel
{
    // State variables for connections
    private readonly DataGenerator _generator;

    // Reference to model which data should be added to.
    private readonly GraphDataModel _graphDataModel;

    // ---------- Constructor ---------- //
    /// <summary>
    /// Creates a DataGeneratorChannel, which will generate data
    /// for the given GraphDataModel.
    /// </summary>
    /// <param name="graphDataModel">The GraphDataModel to add data to</param>
    public DataGeneratorChannel(GraphDataModel graphDataModel)
    {
        _generator = new DataGenerator();
        _graphDataModel = graphDataModel;
        _generator.DataAvailable += OnTestDataReceived;
    }

    // ---------- IDataChannel Methods ---------- //
    public void Connect()
    {
        _generator.Start();
    }

    public void Disconnect()
    {
        _generator.Stop();
        _generator.DataAvailable -= OnTestDataReceived;
    }
    
    // ---------- Implementation-specific helper methods ---------- //
    private void OnTestDataReceived()
    {
        lock (_graphDataModel)
        {
            // Add last X- and Y-data point via indexing from the end.
            _graphDataModel.AddPoint(_generator.XData[^1], _generator.YData[^1]);
        }
    }
}