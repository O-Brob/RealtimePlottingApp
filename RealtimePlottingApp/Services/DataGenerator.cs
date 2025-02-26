using System;
using System.Collections.Generic;
using System.Threading;

namespace RealtimePlottingApp.Services
{
    /// <summary>
    /// Generates random data for the real-time plot.
    /// This is for testing purposes only, when we strictly
    /// need to generate data which is displayed in real-time
    /// to see how a plot looks or works.
    /// </summary>
    public class DataGenerator
    {
        private readonly List<uint> _xData = new();
        private readonly List<uint> _yData = new();
        private readonly Random _random = new();
        private uint _counter = 0;
        private bool _dataReady = false;
        private bool _isRunning = false;
        private Thread? _dataThread;

        public event Action? DataAvailable; // Allows subscribing to when new data is available

        // Public "getters"
        public IReadOnlyList<uint> XData => _xData;
        public IReadOnlyList<uint> YData => _yData;
        public bool DataReady => _dataReady;

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _dataThread = new Thread(GenerateData)
            {
                IsBackground = true
            };
            _dataThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _dataThread?.Join(); // Ensure the thread stops before continuing
        }

        private void GenerateData()
        {
            while (_isRunning)
            {
                Thread.Sleep(1); // Simulate data generation delay (1 ms)
                //for (int i = 0; i < 100000; i++);
                
                lock (_xData)
                {
                    _counter++;
                    uint newX = _counter;
                    uint newY = (uint)_random.Next(0, 11);
                    _xData.Add(newX);
                    _yData.Add(newY);
                }

                _dataReady = true; // Signal that new data is available
                DataAvailable?.Invoke();
            }
        }

        public void ResetDataReadyFlag()
        {
            _dataReady = false;
        }
    }
}
