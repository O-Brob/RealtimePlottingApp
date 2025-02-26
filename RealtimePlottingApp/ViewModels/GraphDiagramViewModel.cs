using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class GraphDiagramViewModel : ViewModelBase
    {
        private readonly Timer _timer;
        private readonly Timer _historyEnableTimer;
        private readonly DataGenerator _dataGenerator;
        private readonly GraphDataModel _graphData;
        public AvaPlot? LinePlot { get; set; }  // Assigned from View

        private bool _plotFullHistory = false;
        private const double WindowWidth = 1000;

        public GraphDiagramViewModel()
        {
            // --- Initialize MessageBus for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                if (msg.StartsWith("ConnectUart:"))
                {
                    ParseUartConfig(msg);
                }
            });
            
            _dataGenerator = new DataGenerator();
            _dataGenerator.Start();
            _graphData = new GraphDataModel();

            // UI update timer
            _timer = new Timer(100);
            _timer.Elapsed += UpdatePlot;
            _timer.Start();

            // Timer to enable full history mode after given amount of seconds (for preliminary testing)
            _historyEnableTimer = new Timer(15000);
            _historyEnableTimer.Elapsed += EnableFullHistory;
            _historyEnableTimer.AutoReset = false; // Run only once
            _historyEnableTimer.Start();
        }

        private void UpdatePlot(object? sender, ElapsedEventArgs e)
        {
            if (_dataGenerator.DataReady)
            {
                lock (_dataGenerator)
                {
                    int existingCount = _graphData.XData.Count;
                    for (int i = existingCount; i < _dataGenerator.XData.Count; i++)
                    {
                        _graphData.AddPoint(_dataGenerator.XData[i], _dataGenerator.YData[i]);
                    }
                }

                // Determine whether to plot recent points or full history
                int totalPoints = _graphData.XData.Count;
                int startIndex = (!_plotFullHistory && totalPoints > 1000) ? totalPoints - 1000 : 0;

                double[] xDataDouble = _graphData.XData.Skip(startIndex).Select(val => (double)val).ToArray();
                double[] yDataDouble = _graphData.YData.Skip(startIndex).Select(val => (double)val).ToArray();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (LinePlot == null)
                        return;
                    LinePlot.Plot.Clear();
                    LinePlot.Plot.Add.ScatterLine(xDataDouble, yDataDouble);

                    if (!_plotFullHistory && xDataDouble.Length > 0)
                    {
                        double lastX = xDataDouble.Last();
                        LinePlot.Plot.Axes.SetLimitsX(lastX - WindowWidth, lastX);
                    }
                    else
                    {
                        LinePlot.Plot.Axes.AutoScale();
                    }

                    LinePlot.Refresh();
                });

                _dataGenerator.ResetDataReadyFlag();
            }

            // If full history mode is active, stop further updates
            // (also stop reading CAN/UART in the future when we make use of that here)
            if (_plotFullHistory)
            {
                _timer.Stop();
            }
        }

        private void EnableFullHistory(object? sender, ElapsedEventArgs e)
        {
            _plotFullHistory = true;
            UpdatePlot(null, null); // Plot the full history one last time before stopping updates
        }
        
        // ---------- Private helper methods ---------- //
        private void ParseUartConfig(string message)
        {
            try
            {
                // Updated regex pattern that allows "ConnectUart:" at the start
                const string pattern = @"^ConnectUart:ComPort:(?<comPort>[^,]+),BaudRate:(?<baudRate>\d+),DataSize:(?<dataSize>\d+ bits)$";

                // Match against the input string
                Match match = Regex.Match(message, pattern);

                if (match.Success)
                {
                    string comPort = match.Groups["comPort"].Value;
                    int baudRate = int.Parse(match.Groups["baudRate"].Value);
                    int dataSize = match.Groups["dataSize"].Value switch
                    { // TODO: Change to proper enum representation
                        "8 bits" => 8,
                        "16 bits" => 16,
                        "32 bits" => 32,
                        _ => throw new FormatException("Invalid data size format")
                    };

                    // Output the extracted values
                    Console.WriteLine($"ComPort: {comPort}, BaudRate: {baudRate}, DataSize: {dataSize}");
                }
                else
                {
                    Console.WriteLine("Message format is incorrect.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing message: {ex.Message}");
            }
        }

        
    }
}
