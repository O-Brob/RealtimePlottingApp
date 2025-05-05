using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ReactiveUI;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.DataChannels;
using ScottPlot.Plottables;
using Timer = System.Timers.Timer;

namespace RealtimePlottingApp.Services.Plotting.LineGraph;

public class TriggerService : ITriggerService
{
    private int _triggerStartIndex = 0; // Represents the start index when trigger was enabled
    private int _lastTriggerIndex = -1; // index of most recent trigger
    private bool _plotTriggerView = false; // true when single trigger has occurred
    private TriggerMode _triggerMode = TriggerMode.Single_Trigger;

    public int LastTriggerIndex => _lastTriggerIndex;
    public bool PlotTriggerView => _plotTriggerView;

    public TriggerMode Mode
    {
        get => _triggerMode;
        set => _triggerMode = value;
    }

    public void EnableTrigger(int currentDataCount)
    {
        // Only look for trigger occurrences from here on forward.
        _triggerStartIndex = currentDataCount;
    }

    public void OnTriggerMoved(int currentDataCount)
    {
        // If we move the trigger point via mouse dragging,
        // we don't want values from when we enabled it to cause it to trigger.
        if ((_triggerStartIndex == currentDataCount) || _plotTriggerView)
            return;
        _triggerStartIndex = currentDataCount;
    }

    public void ResetTrigger()
    {
        _triggerStartIndex = 0;
        _lastTriggerIndex = -1;
        _plotTriggerView = false;
        MessageBus.Current.SendMessage(!_plotTriggerView, "TrigCheckboxEnabled");
    }

    public int CheckForTrigger(GraphDataModel graphData, ObservableCollection<IVariableModel>? plotConfigVariables,
        int uniqueVars, HorizontalLine? triggerLevel)
    {
        if (triggerLevel != null)
        {
            // Calculate start index to be used in loop below
            int startIndex = Math.Max(_triggerStartIndex - uniqueVars, 0);

            // Loop over each variable to check trigger for each variable individually
            for (int v = 0; v < uniqueVars; v++)
            {
                // Skip checking triggers on this variable if it is not set as triggerable
                if (plotConfigVariables != null && v < plotConfigVariables.Count && plotConfigVariables[v].IsTriggerable == false)
                    continue;

                // Loop over Y-data as IEnumberable after filtering for variable v.
                var prevValue = uint.MinValue; // Default to prevent issue when checking first value.

                foreach (var data in graphData.YData
                             .Skip(startIndex)
                             .Select((val, index) => new { GlobalIdx = startIndex + index, Value = val })
                             .Where(x => x.GlobalIdx % uniqueVars == v))
                {
                    if (prevValue < triggerLevel.Y && data.Value > triggerLevel.Y && prevValue < data.Value)
                    {
                        _lastTriggerIndex = data.GlobalIdx;
                        return _lastTriggerIndex;
                    }

                    // Update previous value for next comparison
                    prevValue = data.Value;
                }
            }
        }

        return -1;
    }

    public void HandleTrigger(IDataChannel? dataChannel, Timer timer, GraphDataModel graphData)
    {
        switch (_triggerMode)
        {
            case TriggerMode.Single_Trigger:
                // Enable TriggerView and notify MessageBus 
                _plotTriggerView = true;
                MessageBus.Current.SendMessage(!_plotTriggerView, "TrigCheckboxEnabled");
                
                new Thread(() =>
                {
                    Thread.Sleep(2000);
                    timer.Stop(); // Stop Graph UI updates
                    switch (dataChannel)
                    {
                        case UartDataChannel:
                            MessageBus.Current.SendMessage("UARTDisconnected");
                            break;
                        case CanDataChannel:
                            MessageBus.Current.SendMessage("CANDisconnected");
                            break;
                    }
                    dataChannel?.Disconnect();
                }).Start();
                break;

            case TriggerMode.Normal_Trigger:
                {
                    lock (graphData)
                    {
                        _triggerStartIndex = graphData.YData.Count;
                    }
                    break;
                }
        }
    }
}