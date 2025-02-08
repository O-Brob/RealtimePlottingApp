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
        private readonly List<double> _xData = new();
        private readonly List<double> _yData = new();
        private readonly Random _random = new();
        // NON-ACCURATE time!!!, just to have some incrementing made up value
        private double _time = 0;
        private bool _dataReady = false;

        public event Action? DataAvailable; // to allow subscribing to when new data is availalble

        // Public "getters"
        public IReadOnlyList<double> XData => _xData;
        public IReadOnlyList<double> YData => _yData;
        public bool DataReady => _dataReady;

        public void Start()
        {
            var dataThread = new Thread(GenerateData)
            {
                IsBackground = true
            };
            dataThread.Start();
        }

        private void GenerateData()
        {
            while (true)
            {
                Thread.Sleep(1); // Simulate data generation delay (1 ms)

                lock (_xData)
                {
                    _time += 0.1;
                    double newY = _random.NextDouble() * 10;

                    _xData.Add(_time);
                    _yData.Add(newY);
                }

                _dataReady = true; // Signal new data is available
                DataAvailable?.Invoke();
            }
        }

        public void ResetDataReadyFlag()
        {
            _dataReady = false;
        }
    }
}