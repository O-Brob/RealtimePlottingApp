﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services;
using RealtimePlottingApp.Services.UART;
using ScottPlot.Avalonia;

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
        private readonly Timer _historyEnableTimer;
        
        // --- Graph elements from View --- //
        public AvaPlot? LinePlot { get; set; }  // Line-plot assigned from View

        // --- Plotting modes & restraints --- //
        private const bool _enableDataGeneratorTesting = true;
        private bool _plotFullHistory; // false default
        private const int _plotFullHistoryMsTimer = 15000;
        private const double WindowWidth = 1000;

        // =============== Constructor =============== //
        public GraphDiagramViewModel()
        {
            // --- Initialize MessageBus for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                if (msg.StartsWith("ConnectUart:"))
                {
                    // Try to parse the config. If parsing goes well, connect.
                    if (ParseUartConfig(msg, out string comPort, out int baudRate, out UARTDataPayloadSize dataSize))
                    {
                        try
                        {
                            ResetDataChannels(); // Ensure no channel exists for any medium
                            _serialReader = new UARTSerialReader();
                            _serialReader.TimestampedDataReceived += OnUartDataReceived;
                            _serialReader.StartSerial(comPort, baudRate, dataSize);
                            // TODO: Give visual "UART: Connected" feedback
                        }
                        catch (Exception e)
                        {
                            // TODO: Display on application?
                            Console.WriteLine(e.Message);
                        }
                    }
                }
            });
            
            _graphData = new GraphDataModel();

            // UI update timer
            _timer = new Timer(100); // 100ms between UI updates
            _timer.Elapsed += UpdatePlot;
            _timer.Start();

            // Timer to enable full history mode after given amount of seconds (for preliminary testing)
            _historyEnableTimer = new Timer(_plotFullHistoryMsTimer);
            _historyEnableTimer.Elapsed += EnableFullHistory;
            _historyEnableTimer.AutoReset = false; // Run only once
            _historyEnableTimer.Start();
            
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
                _dataGenerator.Start();
            }
        }
        
        // =============== Graph Update Methods =============== //

        private void UpdatePlot(object? sender, ElapsedEventArgs e)
        {
            double[] xDataDouble, yDataDouble;
            // Lock the graph data while reading it for plotting
            lock (_graphData)
            {
                var totalPoints = _graphData.XData.Count;
                int startIndex = (!_plotFullHistory && totalPoints > 1000) ? totalPoints - 1000 : 0;
                xDataDouble = _graphData.XData.Skip(startIndex).Select(val => (double)val).ToArray();
                yDataDouble = _graphData.YData.Skip(startIndex).Select(val => (double)val).ToArray();
            }

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

            // Stop further updates if full-history mode is active.
            if (_plotFullHistory)
            {
                _timer.Stop();
            }
        }

        private void EnableFullHistory(object? sender, ElapsedEventArgs e)
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
        private static bool ParseUartConfig(string message, out string comPort, out int baudRate, out UARTDataPayloadSize dataSize)
        {
            const string pattern = @"^ConnectUart:ComPort:(?<comPort>[^,]+),BaudRate:(?<baudRate>\d+),DataSize:(?<dataSize>\d+ bits)$";
            Match match = Regex.Match(message, pattern);

            if (match.Success)
            {
                comPort = match.Groups["comPort"].Value;
                baudRate = int.Parse(match.Groups["baudRate"].Value);
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
    }
}
