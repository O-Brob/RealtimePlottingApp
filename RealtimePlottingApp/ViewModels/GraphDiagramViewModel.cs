using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.CAN;
using RealtimePlottingApp.Services.ConfigParsers;
using RealtimePlottingApp.Services.DataChannels;
using RealtimePlottingApp.Services.Plotting.LineGraph;
using RealtimePlottingApp.Services.UART;
using ScottPlot.Avalonia;
using Timer = System.Timers.Timer;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class GraphDiagramViewModel : ViewModelBase
    {
        // --- Data channels --- //
        private IDataChannel? _dataChannel; // Holds current data channel
        
        // --- Services --- //
        private readonly IConfigParser _configParser;
        private readonly IGraphDataService _graphDataService;
        private readonly ITriggerService _triggerService;
        private readonly IPlotUiService _plotUiService;
        
        // --- UI Timers --- //
        private readonly Timer _uiUpdateTimer;
        
        // --- Plot Assigner via View --- //
        public AvaPlot? LinePlot
        {
            get => _plotUiService.LinePlot;
            set => _plotUiService.LinePlot = value;
        }

        // --- Plotting modes & restraints --- //
        private const bool _enableDataGeneratorTesting = false;

        // =============== Constructor =============== //
        public GraphDiagramViewModel()
        {
            // Initialize services
            _configParser = new ConfigParser();
            _graphDataService = new GraphDataService();
            _triggerService = new TriggerService();
            _plotUiService = new PlotUiService();
            _plotUiService.LinePlot = LinePlot;

            // UI update timer
            _uiUpdateTimer = new Timer(100); // ms between UI updates
            _uiUpdateTimer.Elapsed += UpdatePlot;
            
            // --- Initialize MessageBuses for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                // Message telling us to connect to uart for obtaining graph data
                if (msg.StartsWith("ConnectUart:"))
                {
                    // Try to parse the config. If parsing goes well, connect.
                    if (_configParser.ParseUartConfig(msg, out string comPort, out int baudRate, 
                            out UARTDataPayloadSize dataSize, out int uniqueVars))
                    {
                        try
                        {
                            _dataChannel?.Disconnect(); // Ensure no channel exists for any medium
                            _triggerService.ResetTrigger();
                            _graphDataService.ClearData(); // Clear graph data to plot new connection's data
                            _graphDataService.UniqueVars = uniqueVars; // Set number of unique variables
                            _dataChannel = new UartDataChannel( // Set data channel to UART
                                new UARTSerialReader(), comPort, baudRate, dataSize, _graphDataService.GraphData
                            );
                            _dataChannel.Connect();
                            _graphDataService.SetFullHistory(false); // Allow progressive plotting.
                            _uiUpdateTimer.Start(); // Start UI updates
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
                        _dataChannel?.Disconnect();
                        MessageBus.Current.SendMessage("UARTDisconnected");
                        _graphDataService.SetFullHistory(true);
                    }
                    catch (Exception e)
                    {
                        MessageBus.Current.SendMessage($"UARTError: {e.Message}");
                    }
                }
                
                // Try to parse CAN config. If parsing goes well, connect.
                else if (msg.StartsWith("ConnectCan"))
                {
                    if (_configParser.ParseCanConfig(msg, out string canInterface, out string bitRate, 
                            out int canIdFilter, out string canDataPayloadMask))
                    {
                        try
                        {
                            _dataChannel?.Disconnect(); // Ensure no channel exists for any medium
                            _triggerService.ResetTrigger();
                            _graphDataService.ClearData(); // Clear graph data to plot new connection's data
                            
                            _dataChannel = new CanDataChannel( // Set Data Channel to CAN
                                ControllerAreaNetwork.Create(), canInterface,
                                // On Linux bitrate is not set via gui, but socketcan.
                                OperatingSystem.IsWindows() ? bitRate : null, 
                                canIdFilter, canDataPayloadMask, _graphDataService.GraphData
                            );
                            
                            // Extract digits from mask to a Set to find out how many unique variables there are.
                            _graphDataService.UniqueVars = new HashSet<char>(
                                canDataPayloadMask.Where(c => char.IsDigit(c))
                            ).Count;
                            
                            _dataChannel.Connect();

                            _graphDataService.SetFullHistory(false); // Allow progressive plotting.
                            _uiUpdateTimer.Start(); // Start UI updates
                            MessageBus.Current.SendMessage("CANConnected"); // Indicate success
                        }
                        catch (Exception e)
                        {
                            MessageBus.Current.SendMessage($"CANError: {e.Message}");
                        }
                    }
                }
                
                // Message telling us to disconnect CAN
                else if (msg.Equals("DisconnectCan"))
                {
                    try
                    {
                        _dataChannel?.Disconnect();
                        MessageBus.Current.SendMessage("CANDisconnected");
                        _graphDataService.SetFullHistory(true);
                    }
                    catch (Exception e)
                    {
                        MessageBus.Current.SendMessage($"CANError: {e.Message}");
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
                
                else if (msg.StartsWith("updateFrequency:"))
                {
                    // Change the UI timer's update interval on request.
                    // Substring the value following `:`
                    _uiUpdateTimer.Interval = Convert.ToDouble(msg[16..]);
                }
                
                else if (msg.StartsWith("variableAmount:"))
                {
                    // Change the WindowWidth on request.
                    // Substring the value following `:`
                    _graphDataService.WindowWidth = Convert.ToInt32(msg[15..]);
                    _plotUiService.WindowWidth = _graphDataService.WindowWidth;
                }
            });

            // Receive VariableList when number of variables or their properties change. 
            ObservableCollection<IVariableModel>? _previousVars = null;
            MessageBus.Current.Listen<ObservableCollection<IVariableModel>>("VariableList")
                .Subscribe((varList) =>
                {
                    // Unsubscribe from previous variable events if initialization has happened previously.
                    if (_previousVars != null)
                    {
                        foreach (var variable in _previousVars)
                        {
                            variable.PropertyChanged -= Variable_PropertyChanged;
                        }
                    }
        
                    _previousVars = varList;
                    _plotUiService.PlotConfigVariables = varList;
        
                    // Subscribe to each variable's PropertyChanged event.
                    foreach (var variable in varList)
                    {
                        variable.PropertyChanged += Variable_PropertyChanged;
                    }

                    return;

                    // Define local function to handle property changes.
                    void Variable_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
                    {
                        if ((_graphDataService.IsFullHistory || _triggerService.PlotTriggerView) &&
                            e.PropertyName == nameof(IVariableModel.IsChecked) || 
                            e.PropertyName == nameof(IVariableModel.Name))
                        {
                            // Request a UI update if a variable property changes in history mode,
                            // since the active UI timer is off and does not handle it.
                            Dispatcher.UIThread.InvokeAsync(() => UpdatePlot(null, null));
                        }
                    }
                });
            
            // Receive Trigger level enable/disable commands.
            MessageBus.Current.Listen<bool>("TrigChecked").Subscribe(trigEnabled =>
            {
                // Only look for trigger occurances from here on forward.
                _triggerService.EnableTrigger(_graphDataService.GraphData.XData.Count);
                _plotUiService.SetTriggerLevel(_graphDataService.GraphData, true, 
                    10, "Trigger", trigEnabled);
            });
            
            // Receive notice whenever trigger level is moved manually by mouse dragging.
            MessageBus.Current.Listen<string>("TriggerUpdate").Subscribe(_ =>
            {
                _triggerService.OnTriggerMoved(_graphDataService.GraphData.XData.Count);
            });
            
            // Receive notice whenever trigger mode has been updated:
            MessageBus.Current.Listen<string>("SelectedTriggerMode").Subscribe(newMode =>
            {
                _triggerService.Mode = newMode;
            });
            
            // Enable data generator for testing:
            if (_enableDataGeneratorTesting) // Enable data generator
            {
                _plotUiService.PlotConfigVariables =
                    [new VariableModel { Name = "Var 1", IsChecked = true, IsTriggerable = true }];
                _dataChannel = new DataGeneratorChannel(_graphDataService.GraphData);
                _uiUpdateTimer.Start();
                _dataChannel.Connect();
            }
        }
        
        // =============== Graph Update Loop =============== //
        private void UpdatePlot(object? sender, ElapsedEventArgs? e)
        {
            // Trigger index indicating whether trigger has occured.
            int triggerIndex = _triggerService.CheckForTrigger(
                _graphDataService.GraphData, _plotUiService.PlotConfigVariables,
                _graphDataService.UniqueVars, _plotUiService.TriggerLevel);
            
            if (triggerIndex >= 0)
            {
                // Trigger has occured, handle it accordingly.
                _triggerService.HandleTrigger(_dataChannel, _uiUpdateTimer, _graphDataService.GraphData);
            }
            
            // Get sub-arrays and a relative trigger index
            _graphDataService.GetSubData(
                out double[] xDataDouble,
                out double[] yDataDouble,
                out int subArrTriggerIndex,
                triggerIndex >= 0 ? triggerIndex : null,
                _triggerService.LastTriggerIndex >= 0 ? _triggerService.LastTriggerIndex : null,
                _triggerService.Mode
            );
            
            // Keep UI Service's flags up to date
            _plotUiService.PlotFullHistory = _graphDataService.IsFullHistory;
            _plotUiService.LockTriggerLevel = _triggerService.PlotTriggerView;

            // Update the graph's UI in regards to the extracted data and trigger indexes.
            _plotUiService.UpdateGraphUI(xDataDouble, yDataDouble, 
                subArrTriggerIndex, _triggerService.LastTriggerIndex);
            
            // Manual disconnect, disable UI updates (timer calls of this method).
            if (_graphDataService.IsFullHistory)
            {
                _uiUpdateTimer.Stop();
            }
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
