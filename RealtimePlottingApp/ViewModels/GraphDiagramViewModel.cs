using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using RealtimePlottingApp.Events;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services;
using RealtimePlottingApp.Services.CAN;
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
        private ISerialReader? _serialReader;
        private ICanBus? _canBus;
        private DataGenerator? _dataGenerator;
        
        // --- Models --- //
        private readonly GraphDataModel _graphData;
        
        // --- UI Timers --- //
        private readonly Timer _timer;
        
        // --- Graph elements from View --- //
        public AvaPlot? LinePlot { get; set; }  // Line-plot assigned from View
        private int _uniqueVars = 1; // 1 Variable as default. Could change alongside UI config.
        private string _canDataPayloadMask = String.Empty; // Variable mask for the data payload during CAN reading.
        private int _canIdFilter;
        private ObservableCollection<IVariableModel>? _plotConfigVariables;
        private HorizontalLine? _triggerLevel; // Holds trigger level when enabled, else null
        private string _triggerMode = "Single Trigger"; // Holds the selected trigger mode as a string.

        // --- Plotting modes & restraints --- //
        private const bool _enableDataGeneratorTesting = true;
        private bool _plotFullHistory; // false default
        private double WindowWidth = 75;
        private int _triggerStartIndex; // Represents the start index of when the trigger was *enabled!* (not triggered)
        private int _lastTriggerIndex = -1; // index of most recent trigger
        private bool _plotTriggerView; // true when trigger has occured. Used to prevent changing _triggerStartIndex
        
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
                    if (ParseUartConfig(msg, out string comPort, out int baudRate, 
                            out UARTDataPayloadSize dataSize, out _uniqueVars))
                    {
                        try
                        {
                            ResetDataChannels(); // Ensure no channel exists for any medium
                            ResetTrigger();
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
                
                // Try to parse CAN config. If parsing goes well, connect.
                else if (msg.StartsWith("ConnectCan"))
                {
                    if (ParseCanConfig(msg, out string canInterface, out string bitRate, 
                            out _canIdFilter, out _canDataPayloadMask))
                    {
                        try
                        {
                            ResetDataChannels(); // Ensure no channel exists for any medium
                            ResetTrigger();
                            _graphData.Clear(); // Clear graph data to plot new connection's data
                            _canBus = ControllerAreaNetwork.Create();
                            _canBus.MessageReceived += OnCanDataReceived;
                            
                            // Extract digits from mask to a Set to find out how many unique variables there are.
                            _uniqueVars = new HashSet<char>(
                                _canDataPayloadMask.Where(c => char.IsDigit(c))
                            ).Count;
                            
                            if(OperatingSystem.IsWindows()) // On windows the gui-provided bitrate is used
                                _canBus.Connect(canInterface, bitRate);
                            else if (OperatingSystem.IsLinux()) // On Linux bitrate is not set via gui, but socketcan.
                                _canBus.Connect(canInterface, null);
                            
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
                        ResetDataChannels();
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
            MessageBus.Current.Listen<AxisLine>("TriggerDragged").Subscribe(_ =>
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

            return _lastTriggerIndex != -1 ? _lastTriggerIndex : -1;
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
                        if (_serialReader != null)
                            MessageBus.Current.SendMessage("UARTDisconnected");
                        else if (_canBus != null)
                            MessageBus.Current.SendMessage("CANDisconnected");
                        _plotTriggerView = true;
                        ResetDataChannels();
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
                    ? totalPoints - (int)WindowWidth * _uniqueVars
                    : 0;
                
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
                    }
                }

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
                AdjustGraphView(xDataDouble, triggerIndex);

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

        private void OnCanDataReceived(object? sender, CanMessageReceivedEvent e)
        {
            if (_canIdFilter == e.CanId) // Filter by requested ID
            {
                List<uint> variables = ParseCanDataMask(_canDataPayloadMask, e.Data);
                lock (_graphData)
                {
                    foreach (var value in variables)
                    {
                        _graphData.AddPoint(e.Timestamp, value);
                    }
                }
            }
        }
        
        // =============== Data Channel Helper Methods =============== //
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
        
        private static bool ParseCanConfig(string message, out string canInterface, out string bitRate, out int canIdFilter, out string dataPayloadMask)
        {
            const string pattern = @"^ConnectCan:CanInterface:(?<canInterface>[^,]+),BitRate:(?<bitRate>[^,]+),CanIdFilter:(?<canIdFilter>\d+),DataPayloadMask:(?<dataPayloadMask>.+)$";
            Match match = Regex.Match(message, pattern);

            if (match.Success)
            {
                canInterface = match.Groups["canInterface"].Value;
                bitRate = match.Groups["bitRate"].Value;
                canIdFilter = int.Parse(match.Groups["canIdFilter"].Value);
                dataPayloadMask = match.Groups["dataPayloadMask"].Value;
                return true;
            }

            // No success, return defaults.
            canInterface = string.Empty;
            bitRate = string.Empty;
            canIdFilter = 0;
            dataPayloadMask = string.Empty;
            return false;
        }
        
        /// <summary>
        /// Parses a CAN data payload mask to extract the variables from the 8-byte data array.
        /// The mask is expected to be in the format "__:__:__:__:__:__:__:__", with each colon-seperated
        /// group corresponds to the first and second half of a byte (imagine it as a hex-representation of the payload).
        /// </summary>
        /// <param name="mask">The mask string</param>
        /// <param name="data">A byte-array of size 8 containing CAN data</param>
        /// <returns>List representing the extracted variables as unsigned integers indexed by numerical order.</returns>
        private static List<uint> ParseCanDataMask(string mask, byte[] data)
        {
            List<uint> variables = [];
            
            string[] groups = mask.Split(':');
            if (groups.Length != 8)
            {
                Console.WriteLine("Invalid mask format. Expected 8 groups.");
                // Return empty list
                return variables;
            }

            // For each possible variable number 1..9 which exists in the mask:
            for (int i = 1; mask.Contains(i.ToString()) && i <= 9; i++)
            {
                uint varI = 0;
                bool variableUpdated = false;
                // Look for number i in the mask, to construct variable i.
                for (int j = 0; j < groups.Length; j++)
                {
                    // Variable not masked in this group(byte), check next.
                    if (!groups[j].Contains(i.ToString())) continue;
                    
                    if (groups[j].Equals($"{i}{i}")) // Case: "ii"
                    {
                        // Shift left by 8 bits (full byte) and `|` with data to reconstruct that full byte of the data.
                        varI = (varI << 8) | data[j];
                        variableUpdated = true;
                    }
                    
                    else if (groups[j].StartsWith($"{i}")) // Case: "i_", where _ is wildcard.
                    {
                        uint highHalf = (uint)(data[j] >> 4) & 0x0F; // Extract high half
                        varI = (varI << 4) | highHalf; // Shift left by 4 bits and `|` with the half
                        variableUpdated = true;
                    }
                    
                    else if (groups[j].EndsWith($"{i}")) // Case "_i", where _ is wildcard.
                    {
                        uint lowHalf = (uint)data[j] & 0x0F; // Extract low half
                        varI = (varI << 4) | lowHalf; // Shift left by 4 bits and `|` with the half
                        variableUpdated = true;
                    }
                }
                // Finished constructing data from mask.
                if(variableUpdated)
                    variables.Add(varI);
            }

            return variables;
        }

        private void ResetDataChannels()
        {
            _dataGenerator?.Stop();
            _dataGenerator = null;
            
            // Stop ISerialReader if it's on, and nullify.
            _serialReader?.StopSerial();
            if(_serialReader != null)
                _serialReader.TimestampedDataReceived -= OnUartDataReceived;
            _serialReader = null;
            
            // Stop ICanBus if it's on, and nullify.
            _canBus?.Disconnect();
            if (_canBus != null)
                _canBus.MessageReceived -= OnCanDataReceived;
            _canBus = null;
        }

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
