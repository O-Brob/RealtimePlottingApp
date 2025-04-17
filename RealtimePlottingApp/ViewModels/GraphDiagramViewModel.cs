using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.CAN;
using RealtimePlottingApp.Services.ConfigParsers;
using RealtimePlottingApp.Services.DataChannels;
using RealtimePlottingApp.Services.UART;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using Timer = System.Timers.Timer;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class GraphDiagramViewModel : ViewModelBase
    {
        // --- Data channels --- //
        private IDataChannel? _dataChannel; // Holds current data channel
        
        // --- Models --- //
        private readonly GraphDataModel _graphData;
        
        // --- Services --- //
        private readonly IConfigParser _configParser;
        
        // --- UI Timers --- //
        private readonly Timer _timer;
        
        // --- Graph elements from View --- //
        public AvaPlot? LinePlot { get; set; }  // Line-plot assigned from View
        private int _uniqueVars = 1; // 1 Variable as default. Could change alongside UI config.
        private ObservableCollection<IVariableModel>? _plotConfigVariables;
        private HorizontalLine? _triggerLevel; // Holds trigger level when enabled, else null
        private string _triggerMode = "Single Trigger"; // Holds the selected trigger mode as a string.

        // --- Plotting modes & restraints --- //
        private const bool _enableDataGeneratorTesting = false;
        private bool _plotFullHistory; // false default
        private double WindowWidth = 75;
        private int _triggerStartIndex; // Represents the start index of when the trigger was *enabled!* (not triggered)
        private int _lastTriggerIndex = -1; // index of most recent trigger
        private bool _plotTriggerView; // true when single trigger has occured, as we want to lock onto that single trig.
        
        // Palette for predictable & consistent color assignment regardless of Trigger lines, etc.
        // Uses a 25-color palette adapted from Tsitsulin's 12-color xgfs palette
        // Aims to help distinguishing the colors for people with color vision deficiency and when printed B&W.
        // https://tsitsul.in/blog/coloropt/
        private readonly IPalette _palette = new ScottPlot.Palettes.Tsitsulin();

        // =============== Constructor =============== //
        public GraphDiagramViewModel()
        {
            // Create a GraphDataModel to hold incoming data which can be plotted.
            _graphData = new GraphDataModel();
            
            _configParser = new ConfigParser();

            // UI update timer
            _timer = new Timer(100); // ms between UI updates
            _timer.Elapsed += UpdatePlot;
            
            // --- Initialize MessageBuses for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                // Message telling us to connect to uart for obtaining graph data
                if (msg.StartsWith("ConnectUart:"))
                {
                    // Try to parse the config. If parsing goes well, connect.
                    if (_configParser.ParseUartConfig(msg, out string comPort, out int baudRate, 
                            out UARTDataPayloadSize dataSize, out _uniqueVars))
                    {
                        try
                        {
                            _dataChannel?.Disconnect(); // Ensure no channel exists for any medium
                            ResetTrigger();
                            _graphData.Clear(); // Clear graph data to plot new connection's data
                            _dataChannel = new UartDataChannel( // Set data channel to UART
                                new UARTSerialReader(), comPort, baudRate, dataSize, _graphData
                            );
                            _dataChannel.Connect();
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
                        _dataChannel?.Disconnect();
                        MessageBus.Current.SendMessage("UARTDisconnected");
                        EnableFullHistory();
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
                            ResetTrigger();
                            _graphData.Clear(); // Clear graph data to plot new connection's data
                            
                            _dataChannel = new CanDataChannel( // Set Data Channel to CAN
                                ControllerAreaNetwork.Create(), canInterface,
                                // On Linux bitrate is not set via gui, but socketcan.
                                OperatingSystem.IsWindows() ? bitRate : null, 
                                canIdFilter, canDataPayloadMask, _graphData
                            );
                            
                            // Extract digits from mask to a Set to find out how many unique variables there are.
                            _uniqueVars = new HashSet<char>(
                                canDataPayloadMask.Where(c => char.IsDigit(c))
                            ).Count;
                            
                            _dataChannel.Connect();
                            
                            
                            _plotFullHistory = false; // Allow progressive plotting.
                            _timer.Start(); // Start UI updates
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
                        EnableFullHistory();
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
                    _timer.Interval = Convert.ToDouble(msg[16..]);
                }
                
                else if (msg.StartsWith("variableAmount:"))
                {
                    // Change the WindowWidth on request.
                    // Substring the value following `:`
                    WindowWidth = Convert.ToInt32(msg[15..]);
                }
            });

            // Receive VariableList when number of variables or their properties change. 
            MessageBus.Current.Listen<ObservableCollection<IVariableModel>>("VariableList")
                .Subscribe((varList) =>
                {
                    // Unsubscribe from previous variable events if initialization has happened previously.
                    if (_plotConfigVariables != null)
                    {
                        foreach (var variable in _plotConfigVariables)
                        {
                            variable.PropertyChanged -= Variable_PropertyChanged;
                        }
                    }
        
                    _plotConfigVariables = varList;
        
                    // Subscribe to each variable's PropertyChanged event.
                    foreach (var variable in _plotConfigVariables)
                    {
                        variable.PropertyChanged += Variable_PropertyChanged;
                    }

                    return;

                    // Define local function to handle property changes.
                    void Variable_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
                    {
                        if ((_plotFullHistory || _plotTriggerView) &&
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
                _triggerStartIndex = _graphData.XData.Count; // Only look for trigger occurances from here on forward.
                SetTriggerLevel(ref _triggerLevel, true, 10, "Trigger", trigEnabled);
            });
            
            // Receive notice whenever trigger level is moved manually by mouse dragging.
            MessageBus.Current.Listen<string>("TriggerUpdate").Subscribe(_ =>
            {
                if ((_triggerStartIndex == _graphData.XData.Count) || _plotTriggerView)
                    return; // No need to write value. Graph hasn't changed, or triggerview has already been enabled.
                // If we move the trigger point via mouse dragging,
                // we don't want values from when we enabled it to casuse it to trigger.
                _triggerStartIndex = _graphData.XData.Count;
            });
            
            // Receive notice whenever trigger mode has been updated:
            MessageBus.Current.Listen<string>("SelectedTriggerMode").Subscribe(newMode =>
            {
                _triggerMode = newMode;
            });
            
            // Enable data generator for testing:
            if (_enableDataGeneratorTesting) // Enable data generator
            {
                _plotConfigVariables = [];
                _dataChannel = new DataGeneratorChannel(_graphData);
                _timer.Start();
                _dataChannel.Connect();
            }
        }
        
        // =============== Graph Update Methods =============== //

        private void UpdatePlot(object? sender, ElapsedEventArgs? e)
        {
            // Extract graph data, such as which data to plot,
            // and a trigger index indicating whether trigger has occured.
            ExtractGraphData(out double[] xDataDouble, out double[] yDataDouble, out int triggerIndex);

            if (triggerIndex >= 0)
            {
                // Trigger has occured, handle it accordingly.
                HandleTrigger();
            }

            // Update the graph's UI in regards to the extracted data and trigger indexes.
            UpdateGraphUI(xDataDouble, yDataDouble, triggerIndex, _lastTriggerIndex);
    
            // Manual disconnect, disable UI updates (timer calls of this method).
            if (_plotFullHistory)
            {
                _timer.Stop();
            }
        }

        // =============== Graph Update Helpers =============== //
        private void EnableFullHistory()
        {
            _plotFullHistory = true;
            UpdatePlot(null, null); // Plot the full history one last time before stopping updates
        }
        
        private int CheckForTrigger()
        {
            if (_triggerLevel != null)
            {
                // Loop over each varable to check trigger for each variable individually
                for (int v = 0; v < _uniqueVars; v++)
                {
                    // Skip checking triggers on this variable if it is not set as triggerable
                    if (_plotConfigVariables != null && v < _plotConfigVariables.Count && _plotConfigVariables[v].IsTriggerable == false) continue;
                    
                    // Filter only Y-values for variable v since triggerStartIndex - uniqueVars (to ensure we don't miss a rising edge)
                    // Additionally create simple data tuples for yData which holds a global index and valeue.
                    var yData = _graphData.YData
                        .Skip(Math.Max(_triggerStartIndex - _uniqueVars, 0))
                        .Select((val, idx) => new { GlobalIdx = Math.Max(_triggerStartIndex - _uniqueVars, 0) + idx, Value = val })
                        .Where(x => x.GlobalIdx % _uniqueVars == v)
                        .ToList();

                    for (int i = 1; i < yData.Count; i++)
                    {
                        uint prev = yData[i - 1].Value;
                        uint curr = yData[i].Value;

                        if (curr > (uint)_triggerLevel.Y &&
                            curr > prev &&
                            prev < (uint)_triggerLevel.Y)
                        {
                            // Use the global index of the current point
                            _lastTriggerIndex = yData[i].GlobalIdx;
                            return _lastTriggerIndex;
                        }
                    }
                }
            }

            return -1;
        }
        
        private void HandleTrigger()
        {
            switch (_triggerMode)
            {
                case "Single Trigger":
                    new Thread(() =>
                    {
                        Thread.Sleep(2000);
                        _timer.Stop(); // Stop Graph UI updates
                        switch (_dataChannel)
                        {
                            case UartDataChannel:
                                MessageBus.Current.SendMessage("UARTDisconnected");
                                break;
                            case CanDataChannel:
                                MessageBus.Current.SendMessage("CANDisconnected");
                                break;
                        }
                        _plotTriggerView = true;
                        _dataChannel?.Disconnect();
                    }).Start();
                    break;
                
                case "Normal Trigger":
                {
                    lock (_graphData)
                    {
                        _triggerStartIndex = _graphData.YData.Count;
                    }

                    break;
                }
            }
        }
        
        private void ExtractGraphData(out double[] xDataDouble, out double[] yDataDouble, out int triggerIndex)
        {
            // Lock the graph data while reading it to ensure consistency
            lock(_graphData)
            { 
                int totalPoints = _graphData.XData.Count;
                // Determine candidate for where to start plotting data from on this UI update.
                int candidate = (!_plotFullHistory && totalPoints > (int)WindowWidth * _uniqueVars)
                    ? totalPoints - ((int)WindowWidth * _uniqueVars + 1)
                    : 0;

                // Indexes to determine subarray ranges for xDataDouble and yDataDouble
                int startIndex;
                int endIndex;
                
                // Check whether trigger point has been reached.
                // If it has, set candidate to 0 such that all historical points are included. 
                triggerIndex = CheckForTrigger();
                if (triggerIndex >= 0 && !_plotFullHistory)
                {
                    switch (_triggerMode)
                    {
                        case "Single Trigger":
                            candidate = 0;
                            break;
                        case "Normal Trigger":
                            // Ensure candidate is always >= 0
                            candidate = Math.Max(triggerIndex - ((int)WindowWidth * _uniqueVars), 0);
                            // Since trigger occured and we do not use full array,
                            // adjust trigger index relative to the subarray we provide.
                            triggerIndex -= candidate;
                            break;
                    }
                    
                    // Set range to from candidate until end of full data array
                    startIndex = candidate - (candidate % _uniqueVars);
                    endIndex = totalPoints;
                }
                else if (!_plotFullHistory)
                {
                    // Check if a last trigger exists, normal trigger is enabled:
                    if (_lastTriggerIndex >= 0 && _triggerMode == "Normal Trigger")
                    {
                        // Set candidate to the most recent trigger, such that the subarray x/yDataDouble
                        // will be the most recent trigger and forward. 
                        candidate = Math.Max(_lastTriggerIndex - ((int)WindowWidth * _uniqueVars),0);
                        // set triggerindex relative to the subarray from the last trigger.
                        triggerIndex = _lastTriggerIndex - candidate;
                        
                        // Limit number of points when no new trigger occurs to stay performant
                        startIndex = candidate - (candidate % _uniqueVars);
                        if (totalPoints >= startIndex + ((int)WindowWidth * _uniqueVars))
                        {
                            endIndex = startIndex + (2 * (int)WindowWidth * _uniqueVars);
                            endIndex = Math.Min(endIndex, totalPoints - (totalPoints % _uniqueVars));
                        }
                        else
                            endIndex = totalPoints;
                    }
                    else // Fallback for no trigger & no history mode
                    {
                        startIndex = candidate - (candidate % _uniqueVars);
                        endIndex = totalPoints;
                    }
                }
                else
                {
                    // Full history mode, ensure indexes are set.
                    startIndex = candidate - (candidate % _uniqueVars);
                    endIndex = totalPoints;
                    
                    // if no new trigger but _lastTriggerIndex is set,
                    // update triggerIndex to be _lastTriggerIndex.
                    if (triggerIndex < 0 && _lastTriggerIndex >= 0 && _triggerMode == "Normal Trigger")
                    {
                        // Subtraction by candidate (0) in full-history mode
                        triggerIndex = _lastTriggerIndex;
                    }
                }
                
                xDataDouble = _graphData.XData
                    .Skip(startIndex)
                    .Take(endIndex - startIndex)
                    .Select(val => (double)val)
                    .ToArray();
                
                yDataDouble = _graphData.YData
                    .Skip(startIndex)
                    .Take(endIndex - startIndex)
                    .Select(val => (double)val)
                    .ToArray();
            }
        }

        private void UpdateGraphUI(double[] xDataDouble, double[] yDataDouble, int triggerIndex, int globalTriggerIndex)
        {
            // Update UI asynchronously on Avalonia's UI Thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LinePlot == null) return;

                LinePlot.Plot.Clear<SignalXY>();
                LinePlot.Plot.Clear<Scatter>();

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
                    signal.LegendText = _plotConfigVariables?.Count > v 
                        ? _plotConfigVariables[v].Name : $"Var {v+1}"; // Fallback
                    signal.Color = _palette.GetColor(v % _palette.Colors.Length); // Each var nr. has a preset color.

                    // Do not plot the variable if visibility is unchecked by the user in the UI
                    if (_plotConfigVariables?.Count > v && !_plotConfigVariables[v].IsChecked)
                        signal.IsVisible = false;

                    // If a triggerIndex is plotted, mark it such that the user can see which point triggered.
                    if (triggerIndex >= 0 && globalTriggerIndex % _uniqueVars == v)
                    {
                        // (Local Index for aligning index to interleaved arrays)
                        int localIndex = _uniqueVars == 1 ? triggerIndex : triggerIndex / _uniqueVars;

                        if (localIndex > 0 && localIndex < xVar.Length)
                        {
                            // Triggered point
                            double triggerX = xVar[localIndex];
                            double triggerY = yVar[localIndex];
                            
                            // Point before trigger (for calculating (x,y) interpolations)
                            double xBefore = xVar[localIndex - 1];
                            double yBefore = yVar[localIndex - 1];

                            // Check whether we can find intersection/interpolaton of rising edge and triggerlevel
                            if (_triggerLevel != null &&
                                _triggerLevel.Y > yBefore &&
                                _triggerLevel.Y < triggerY &&
                                (triggerX - xBefore) != 0)
                            {
                                // Find intersection of triggerLevel and the line between trigger point and prev. point.
                                double slope = (triggerY - yBefore) / (triggerX - xBefore);
                                double intersectionX = triggerX + (_triggerLevel.Y - triggerY) / slope;

                                // Place trigger point marker on the rising edge where the triggerLevel was passed
                                // for better visual representation.
                                var marker = LinePlot.Plot.Add.Scatter(intersectionX, _triggerLevel.Y, Colors.Black);
                                marker.MarkerShape = MarkerShape.FilledDiamond;
                                marker.MarkerSize = 6;
                            }
                            else // Fallback
                            {
                                // Add the marker at the trigger point
                                var marker = LinePlot.Plot.Add.Scatter(triggerX, triggerY, color: Colors.Black);
                                marker.MarkerShape = MarkerShape.FilledDiamond;
                                marker.MarkerSize = 6;
                            }
                        }
                    }
                }

                // Call helper to adjust the graph view for the new data (or trigger points)
                AdjustGraphView(xDataDouble, _plotTriggerView ? globalTriggerIndex : triggerIndex);

                // Add legend so each variable is identifiable.
                LinePlot.Plot.ShowLegend();
                LinePlot.Refresh();
            });
        }
        
        private void AdjustGraphView(double[] xDataDouble, int triggerIndex)
        {
            if (!_plotFullHistory && xDataDouble.Length > 0)
            {
                if (triggerIndex >= 0 && triggerIndex < xDataDouble.Length)
                {
                    // Center the plot around trigger point
                    double triggerX = xDataDouble[triggerIndex];
                    LinePlot?.Plot.Axes.SetLimitsX(triggerX - WindowWidth / 2, triggerX + WindowWidth / 2);
                }
                else
                {
                    // Make X-Axis limit & "follow" the plotting while not in "history mode"
                    double lastX = xDataDouble.Last();
                    LinePlot?.Plot.Axes.SetLimitsX(lastX - WindowWidth, lastX);
                }
            }
            else
            {
                // History mode entered, don't set limits and autoscale the plot.
                LinePlot?.Plot.Axes.AutoScale();
            }
        }
        
        // Manage the trigger levels
        private void SetTriggerLevel(ref HorizontalLine? triggerLevel, bool placeTriggerAbove, 
            double offset, string startText, bool isEnabled)
        {
            // If _graphData.YData is empty, we set the trigger level to a default value
            // otherwise we place it depending on the max & minimum values, to reduce
            // risk of accidental "instant trigger"
            double triggerPosition;

            if (_graphData.YData.Count == 0)
            {
                // Place it at 10 if placing above or at -10 if placing below
                triggerPosition = placeTriggerAbove ? 10 : -10;
            }
            else
            {
                // Calculate the trigger level position based on the input parameters
                lock(_graphData)
                {
                    if (placeTriggerAbove)
                    {
                        // Set the trigger level slightly above the maximum Y value in the data
                        triggerPosition = _graphData.YData.Max() + offset;
                    }
                    else
                    {
                        // Set the trigger level slightly below the minimum Y value in the data
                        triggerPosition = _graphData.YData.Min() - offset;
                    }
                }
            }

            // Set the trigger level only if it's enabled
            if (isEnabled)
            {
                // Add the horizontal line if it's enabled and doesn't already exist
                if (triggerLevel == null)
                {
                    triggerLevel = LinePlot?.Plot.Add.HorizontalLine(triggerPosition);
                    if (triggerLevel != null)
                    {
                        triggerLevel.IsDraggable = true;
                        triggerLevel.IsVisible = true;
                        triggerLevel.Text = startText;
                        triggerLevel.LinePattern = LinePattern.Dashed;
                        triggerLevel.LineColor = Color.FromHex("#2987CC"); // Same blue as UI
                        triggerLevel.LabelBackgroundColor = Color.FromHex("#2987CC"); // -::-
                    }
                }
            }
            else
            {
                // Remove the horizontal line if it's disabled and it exists
                if (triggerLevel != null)
                {
                    LinePlot?.Plot.Remove(triggerLevel);
                    triggerLevel = null; // Indicate that the triggerLevel does not exist anymore.
                }
            }

            // Refresh the plot to reflect the changes
            LinePlot?.Refresh();
        }
        
        //================ Internal state Handlers =============== //
        // Used to reset any internal state used for internal trigger logic,
        // such that a new connection is not affected by triggers of previosu connections.
        private void ResetTrigger()
        {
            _triggerStartIndex = 0;
            _lastTriggerIndex = -1;
            _plotTriggerView = false;
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
