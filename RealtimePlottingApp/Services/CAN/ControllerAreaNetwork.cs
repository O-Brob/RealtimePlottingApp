using System;
using System.Runtime.InteropServices;

namespace RealtimePlottingApp.Services.CAN
{
    /// <summary>
    /// Factory class for creating CAN bus instances.
    /// It automatically selects the appropriate implementation based on the operating system,
    /// using SocketCAN for Linux to enable broadest support, and PCAN for Windows due to the lack of libraries with cross-platform support.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not supported.</exception>
    public static class ControllerAreaNetwork
    {
        /// <summary>
        /// Create a new CAN bus instance.
        /// </summary>
        /// <returns>A new CAN bus instance.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not supported.</exception>
        public static ICanBus Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Console.WriteLine("Using SocketCAN for Linux.");
                return new SocketCanBus();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Console.WriteLine("Using PCAN for Windows.");
                return new PeakCanBus();
            }
            throw new PlatformNotSupportedException("Only Windows and Linux are supported for CAN.");
        }
    }
}