using System.Collections.Generic;
using System.Timers;
using Avalonia.Threading;
using RealtimePlottingApp.Services;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.ViewModels;

// ViewModelBase inherits from ReactiveObject.
public class GraphDiagramViewModel : ViewModelBase
{
    private readonly Timer _timer;
    private readonly DataGenerator _dataGenerator;
    public AvaPlot? LinePlot { get; set; }  // Assigned from View

    public GraphDiagramViewModel()
    {
            
        // Initialize and start the data generator
        _dataGenerator = new DataGenerator();
        _dataGenerator.Start();

        // Set up the timer for UI updates
        _timer = new Timer(100);
        _timer.Elapsed += UpdatePlot;
        _timer.Start();
            
    }

    private void UpdatePlot(object? sender, ElapsedEventArgs e)
    {
        if (_dataGenerator.DataReady)
        {
            List<double> xDataCopy;
            List<double> yDataCopy;

            // Lock and copy the data to prevent race conditions
            lock (_dataGenerator)
            {
                xDataCopy = new List<double>(_dataGenerator.XData);
                yDataCopy = new List<double>(_dataGenerator.YData);
            }

            // Update the plot on the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LinePlot == null)
                    return;
                LinePlot.Plot.Clear();
                LinePlot.Plot.Add.Signal(yDataCopy.ToArray());
                LinePlot.Refresh();
            });

            _dataGenerator.ResetDataReadyFlag(); // Reset flag
        }
    }
}