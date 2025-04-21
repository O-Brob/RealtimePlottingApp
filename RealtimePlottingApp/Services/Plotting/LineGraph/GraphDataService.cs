using System;
using System.Linq;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.Plotting.LineGraph
{
    public class GraphDataService : IGraphDataService
    {
        // --- Models --- //
        private readonly GraphDataModel _graphData;
        
        // --- Plotting modes & restraints --- //
        private int _uniqueVars = 1; // 1 Variable as default. Could change alongside UI config.
        private bool _plotFullHistory; // False default
        private double _windowWidth = 75; // X-Axis unit width (how many points to show) 

        public GraphDataService()
        {
            // Create a GraphDataModel to hold graph data.
            _graphData = new GraphDataModel();
        }

        public GraphDataModel GraphData => _graphData;

        public int UniqueVars
        {
            get => _uniqueVars;
            set => _uniqueVars = value;
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set => _windowWidth = value;
        }

        public bool IsFullHistory => _plotFullHistory;

        public void ClearData()
        {
            _graphData.Clear();
        }

        public void SetFullHistory(bool fullHistory)
        {
            // Set full history mode, indicates we should plot entire data history.
            _plotFullHistory = fullHistory;
        }

        public void GetSubData(out double[] xDataDouble, out double[] yDataDouble, out int localTriggerIndex, 
            int? currentTriggerIndex, int? lastTriggerIndex, string triggerMode)
        {
            // Lock the graph data while reading it to ensure consistency
            lock (_graphData)
            {
                int totalPoints = _graphData.XData.Count;
                // Determine candidate for where to start plotting data from on this UI update.
                int candidate = (!_plotFullHistory && totalPoints > (int)_windowWidth * _uniqueVars)
                    ? totalPoints - ((int)_windowWidth * _uniqueVars + 1)
                    : 0;

                // Indexes to determine subarray ranges for xDataDouble and yDataDouble
                int startIndex;
                int endIndex;
                localTriggerIndex = -1;

                // Check whether trigger point has been reached.
                // If it has, set candidate to 0 such that all historical points are included.
                if (currentTriggerIndex.HasValue && !_plotFullHistory)
                {
                    switch (triggerMode)
                    {
                        case "Single Trigger":
                            localTriggerIndex = currentTriggerIndex.Value;
                            candidate = 0;
                            break;

                        case "Normal Trigger":
                            // Ensure candidate is always >= 0
                            candidate = Math.Max(currentTriggerIndex.Value - ((int)_windowWidth * _uniqueVars), 0);
                            // Since trigger occured and we do not use full array,
                            // adjust trigger index relative to the subarray we provide.
                            localTriggerIndex = currentTriggerIndex.Value - candidate;
                            break;
                    }

                    // Set range to from candidate until end of full data array
                    startIndex = candidate - (candidate % _uniqueVars);
                    endIndex = totalPoints;
                }
                else if (!_plotFullHistory)
                {
                    // Check if a last trigger exists, normal trigger is enabled:
                    if (lastTriggerIndex.HasValue && triggerMode == "Normal Trigger")
                    {
                        // Set candidate to the most recent trigger, such that the subarray x/yDataDouble
                        // will be the most recent trigger and forward.
                        candidate = Math.Max(lastTriggerIndex.Value - ((int)_windowWidth * _uniqueVars), 0);
                        // set triggerindex relative to the subarray from the last trigger.
                        localTriggerIndex = lastTriggerIndex.Value - candidate;

                        // Limit number of points when no new trigger occurs to stay performant
                        startIndex = candidate - (candidate % _uniqueVars);
                        if (totalPoints >= startIndex + ((int)_windowWidth * _uniqueVars))
                        {
                            endIndex = startIndex + 2 * (int)_windowWidth * _uniqueVars;
                            endIndex = Math.Min(endIndex, totalPoints - (totalPoints % _uniqueVars));
                        }
                        else
                        {
                            endIndex = totalPoints;
                        }
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
                    if (!currentTriggerIndex.HasValue && lastTriggerIndex.HasValue && triggerMode == "Normal Trigger")
                    {
                        // Subtraction by candidate (0) in full-history mode
                        localTriggerIndex = lastTriggerIndex.Value;
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

                // Fallback in case index is out of the arroy
                if (localTriggerIndex < 0 || localTriggerIndex >= xDataDouble.Length)
                    localTriggerIndex = -1;
            }
        }

    }
}
