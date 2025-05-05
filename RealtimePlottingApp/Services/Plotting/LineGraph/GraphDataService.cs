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
        
        // --- Buffers to avoid torturing the heap --- //
        private double[] _xBuffer = [];
        private double[] _yBuffer = [];

        public GraphDataService(GraphDataModel graphData)
        {
            // Create a GraphDataModel to hold graph data.
            _graphData = graphData;
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
            int? currentTriggerIndex, int? lastTriggerIndex, TriggerMode triggerMode)
        {
            // Lock the graph data while reading it to ensure consistency
            lock (_graphData)
            {
                int totalPoints = _graphData.XData.Count;
                // Determine candidate for where to start plotting data from on this UI update.
                int candidate = 0;

                if (!_plotFullHistory && totalPoints > (int)(_windowWidth * _uniqueVars))
                {
                    // Set initial candidate
                    candidate = totalPoints - ((int)_windowWidth * _uniqueVars + 1);
                    candidate = Math.Max(0, candidate);

                    uint targetTimestamp = _graphData.XData[^1];
                    uint cutoffGuess = _graphData.XData[candidate];

                    // If initial candidat isn't spaced enough (#points > one per timestamp), search backwards
                    if (targetTimestamp - cutoffGuess < _windowWidth)
                    {
                        uint cutoff = targetTimestamp > (uint)_windowWidth
                            ? targetTimestamp - (uint)_windowWidth
                            : 0;

                        // Binary search for last value <= cutoff
                        int low = 0;
                        int high = totalPoints - 1;
                        int foundIndex = candidate;

                        while (low <= high)
                        {
                            int mid = (low + high) / 2;
                            uint ts = _graphData.XData[mid];

                            if (ts <= cutoff)
                            {
                                foundIndex = mid;
                                low = mid + 1;
                            }
                            else
                            {
                                high = mid - 1;
                            }
                        }

                        candidate = foundIndex;
                    }
                }

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
                        case TriggerMode.Single_Trigger:
                            localTriggerIndex = currentTriggerIndex.Value;
                            candidate = 0;
                            break;

                        case TriggerMode.Normal_Trigger:
                            // Ensure candidate is always >= 0. _uniqueVars+3 for extra variable headroom.
                            candidate = Math.Max(currentTriggerIndex.Value - ((int)_windowWidth * (_uniqueVars + 3)), 0);
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
                    if (lastTriggerIndex.HasValue && triggerMode == TriggerMode.Normal_Trigger)
                    {
                        // Set candidate to the most recent trigger, such that the subarray x/yDataDouble
                        // will be the most recent trigger and forward. _uniqueVars+3 for extra variable headroom.
                        candidate = Math.Max(lastTriggerIndex.Value - ((int)_windowWidth * (_uniqueVars + 3)), 0);
                        // set triggerindex relative to the subarray from the last trigger.
                        localTriggerIndex = lastTriggerIndex.Value - candidate;

                        // Limit number of points when no new trigger occurs to stay performant
                        startIndex = candidate - (candidate % _uniqueVars);
                        
                        // Limit the size of the plotted slice to a smaller window for performance
                        int maxPointsToPlot = 2 * (int)_windowWidth * (_uniqueVars + 3);
                        endIndex = Math.Min(startIndex + maxPointsToPlot, totalPoints);
                        endIndex -= endIndex % _uniqueVars; // Align to variable interleaving.
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
                    if (!currentTriggerIndex.HasValue && lastTriggerIndex.HasValue && 
                        triggerMode == TriggerMode.Normal_Trigger)
                    {
                        // Subtraction by candidate (0) in full-history mode
                        localTriggerIndex = lastTriggerIndex.Value;
                    }
                }
                
                int count = endIndex - startIndex;

                if (_xBuffer.Length != count)
                {
                    _xBuffer = new double[count];
                    _yBuffer = new double[count];
                }

                for (int i = 0; i < count; i++)
                {
                    _xBuffer[i] = _graphData.XData[startIndex + i];
                    _yBuffer[i] = _graphData.YData[startIndex + i];
                }

                xDataDouble = _xBuffer;
                yDataDouble = _yBuffer;

                // Fallback in case index is out of the arroy
                if (localTriggerIndex < 0 || localTriggerIndex >= xDataDouble.Length)
                    localTriggerIndex = -1;
            }
        }

    }
}
