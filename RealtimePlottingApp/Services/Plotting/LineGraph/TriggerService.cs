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
    private string _triggerMode = "Single Trigger";

    public int LastTriggerIndex => _lastTriggerIndex;
    public bool PlotTriggerView => _plotTriggerView;

    public string Mode
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
    }

    public int CheckForTrigger(GraphDataModel graphData, ObservableCollection<IVariableModel>? plotConfigVariables,
        int uniqueVars, HorizontalLine? triggerLevel)
    {
        if (triggerLevel != null)
        {
            // Loop over each variable to check trigger for each variable individually
            for (int v = 0; v < uniqueVars; v++)
            {
                // Skip checking triggers on this variable if it is not set as triggerable
                if (plotConfigVariables != null && v < plotConfigVariables.Count && plotConfigVariables[v].IsTriggerable == false)
                    continue;

                // Filter only Y-values for variable v since triggerStartIndex - uniqueVars (to ensure we don't miss a rising edge)
                // Additionally create simple data tuples for yData which holds a global index and valeue.
                var yData = graphData.YData
                    .Skip(Math.Max(_triggerStartIndex - uniqueVars, 0))
                    .Select((val, idx) => new { GlobalIdx = Math.Max(_triggerStartIndex - uniqueVars, 0) + idx, Value = val })
                    .Where(x => x.GlobalIdx % uniqueVars == v)
                    .ToList();

                for (int i = 1; i < yData.Count; i++)
                {
                    uint prev = yData[i - 1].Value;
                    uint curr = yData[i].Value;

                    if (curr > triggerLevel.Y &&
                        curr > prev &&
                        prev < triggerLevel.Y)
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

    public void HandleTrigger(IDataChannel? dataChannel, Timer timer, GraphDataModel graphData)
    {
        switch (_triggerMode)
        {
            case "Single Trigger":
                _plotTriggerView = true;
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

            case "Normal Trigger":
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