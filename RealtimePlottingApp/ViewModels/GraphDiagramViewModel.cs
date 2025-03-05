using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services;
using RealtimePlottingApp.Services.UART;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class GraphDiagramViewModel : ViewModelBase
    {
        // --- Data channels --- //
        private ISerialReader? _serialReader;
        private DataGenerator? _dataGenerator;
        
        // --- Models --- //
        private readonly GraphDataModel _graphData;
        
        // --- UI Timers --- //
        private readonly Timer _timer;
        
        // --- Graph elements from View --- //
        public AvaPlot? LinePlot { get; set; }  // Line-plot assigned from View
        private int _uniqueVars = 1; // 1 Variable as default. Could change alongside UI config.

        // --- Plotting modes & restraints --- //
        private const bool _enableDataGeneratorTesting = true;
        private bool _plotFullHistory; // false default
        private const double WindowWidth = 75;

        // =============== Constructor =============== //
        public GraphDiagramViewModel()
        {
            // Create a GraphDataModel to hold incoming data which can be plotted.
            _graphData = new GraphDataModel();

            // UI update timer
            _timer = new Timer(100); // 100ms between UI updates
            _timer.Elapsed += UpdatePlot;
            
            // --- Initialize MessageBus for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                // Message telling us to connect to uart for obtaining graph data
                if (msg.StartsWith("ConnectUart:"))
                {
                    // Try to parse the config. If parsing goes well, connect.
                    if (ParseUartConfig(msg, out string comPort, out int baudRate, 
                            out UARTDataPayloadSize dataSize, out _uniqueVars))
                    {
                        try
                        {
                            ResetDataChannels(); // Ensure no channel exists for any medium
                            _graphData.Clear(); // Clear graph data to plot new connection's data
                            _serialReader = new UARTSerialReader();
                            _serialReader.TimestampedDataReceived += OnUartDataReceived;
                            _serialReader.StartSerial(comPort, baudRate, dataSize);
                            _plotFullHistory = false; // Allow progressive plotting.
                            _timer.Start(); // Start UI updates
                            MessageBus.Current.SendMessage("UARTConnected"); // Indicate success
                        }
                        catch (Exception e)
                        {
                            MessageBus.Current.SendMessage($"UARTError: {e.Message}");
                        }
                    }
                }
                
                // Message telling us to disconnect UART
                else if (msg.Equals("DisconnectUart"))
                {
                    try
                    {
                        _serialReader?.StopSerial();
                        ResetDataChannels();
                        MessageBus.Current.SendMessage("UARTDisconnected");
                        EnableFullHistory();
                    }
                    catch (Exception e)
                    {
                        MessageBus.Current.SendMessage($"UARTError: {e.Message}");
                    }
                }
                
                // Toggle Graph 1 (LineGraph)
                else if (msg.Equals("ToggleLineGraph"))
                {
                    _plot1Visible = !_plot1Visible;
                    this.RaisePropertyChanged(nameof(Plot1Visible));
                    this.RaisePropertyChanged(nameof(Row1Height));
                }
                
                // Toggle Graph 2 (BlockDiagram)
                else if (msg.Equals("ToggleBlockDiagram"))
                {
                    _plot2Visible = !_plot2Visible;
                    this.RaisePropertyChanged(nameof(Plot2Visible));
                    this.RaisePropertyChanged(nameof(Row2Height));
                }
            });
            
            // Enable data generator for testing:
            if (_enableDataGeneratorTesting) // Enable data generator
            {
                _dataGenerator = new DataGenerator();
                _dataGenerator.DataAvailable += () =>
                {
                    lock (_graphData)
                    {
                        _graphData.AddPoint(_dataGenerator.XData.Last(), _dataGenerator.YData.Last());
                    }
                };
                _timer.Start();
                _dataGenerator.Start();
            }
        }
        
        // =============== Graph Update Methods =============== //

        private void UpdatePlot(object? sender, ElapsedEventArgs e)
        {
            double[] xDataDouble, yDataDouble;
            
            // Lock the graph data while reading it to ensure consistency
            lock (_graphData)
            {
                int totalPoints = _graphData.XData.Count;
                // Plot last ~1000 points. Performance seems great,
                // and trying to fit more on screen during plotting is unreasonable.
                int candidate = (!_plotFullHistory && totalPoints > 1000) ? totalPoints - 1000 : 0;
                
                // Ensures sub-array will start at a position that aligns with the interleaved data structure
                int startIndex = candidate - (candidate % _uniqueVars);
                xDataDouble = _graphData.XData
                    .Skip(startIndex)
                    .Select(val => (double)val)
                    .ToArray();
                yDataDouble = _graphData.YData
                    .Skip(startIndex)
                    .Select(val => (double)val)
                    .ToArray();
            }

            // Update UI asynchronously on Avalonia's UI Thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LinePlot == null)
                    return;
                
                LinePlot.Plot.Clear();
        
                // Loop over each variable and extract its data.
                for (int v = 0; v < _uniqueVars; v++)
                {
                    // Use LINQ's index-aware overload of the Where method to filter data by % operations on index.
                    double[] xVar = xDataDouble.Where((_, idx) => idx % _uniqueVars == v).ToArray();
                    double[] yVar = yDataDouble.Where((_, idx) => idx % _uniqueVars == v).ToArray();

                    if (xVar.Length <= 0) continue;
                    
                    // SignalXY Preconditions, which when met allows for greater performance than ScatterLine Plotting:
                    // "New data points must have an X value that is greater to or equal to the previous one."
                    SignalXY signal = LinePlot.Plot.Add.SignalXY(xVar, yVar);
                    signal.LegendText = $"Var {v+1}";
                }
        
                if (!_plotFullHistory && xDataDouble.Length > 0)
                {
                    // Make X-Axis limit & "follow" the plotting while not in "history mode"
                    double lastX = xDataDouble.Last();
                    LinePlot.Plot.Axes.SetLimitsX(lastX - WindowWidth, lastX);
                }
                else
                {
                    LinePlot.Plot.Axes.AutoScale();
                }
        
                // Add legend so each variable is identifiable.
                LinePlot.Plot.ShowLegend();
                LinePlot.Refresh();
            });

            // Stop further updates if full-history mode is active.
            if (_plotFullHistory)
            {
                _timer.Stop();
            }
        }

        private void EnableFullHistory()
        {
            _plotFullHistory = true;
            UpdatePlot(null, null!); // Plot the full history one last time before stopping updates
        }
        
        //================ Data receive handlers =============== //
        private void OnUartDataReceived(object? sender, TimestampedDataReceivedEvent e)
        {
            lock (_graphData)
            {
                foreach (var package in e.Packages)
                {
                    // Add timestamp and accompanied data.
                    _graphData.AddPoint(package.Time, package.Data);
                }
            }
        }
        
        // =============== Private Helper Methods =============== //
        private static bool ParseUartConfig(string message, out string comPort, out int baudRate, out UARTDataPayloadSize dataSize, out int uniqueVars)
        {
            const string pattern = @"^ConnectUart:ComPort:(?<comPort>[^,]+),BaudRate:(?<baudRate>\d+),DataSize:(?<dataSize>\d+ bits),UniqueVars:(?<uniqueVars>\d+)$";
            Match match = Regex.Match(message, pattern);

            if (match.Success)
            {
                comPort = match.Groups["comPort"].Value;
                baudRate = int.Parse(match.Groups["baudRate"].Value);
                uniqueVars = int.Parse(match.Groups["uniqueVars"].Value);
                dataSize = match.Groups["dataSize"].Value switch
                {
                    "8 bits" => UARTDataPayloadSize.UART_PAYLOAD_8,
                    "16 bits" => UARTDataPayloadSize.UART_PAYLOAD_16,
                    "32 bits" => UARTDataPayloadSize.UART_PAYLOAD_32,
                    _ => throw new FormatException("Invalid data size format")
                };
                return true;
            }

            // No success, return defaults.
            comPort = string.Empty;
            baudRate = 0;
            uniqueVars = 0;
            dataSize = default;
            return false;
        }

        private void ResetDataChannels()
        {
            _dataGenerator?.Stop();
            _dataGenerator = null;
            
            // Stop ISerialReader if it's on, and nullify.
            _serialReader?.StopSerial();
            _serialReader = null;
        }
        
        // =============== Handle graph visibility =============== //
        private bool _plot1Visible = true;
        private bool _plot2Visible; // false default

        public bool Plot1Visible
        {
            get => _plot1Visible;
            set => this.RaiseAndSetIfChanged(ref _plot1Visible, value);
        }

        public bool Plot2Visible
        {
            get => _plot2Visible;
            set => this.RaiseAndSetIfChanged(ref _plot2Visible, value);
        }

        // Computed properties for row heights:
        public GridLength Row1Height => Plot1Visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        public GridLength Row2Height => Plot2Visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        
    }
}
