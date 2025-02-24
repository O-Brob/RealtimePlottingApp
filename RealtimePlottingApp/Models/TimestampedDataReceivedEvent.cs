using System;

namespace RealtimePlottingApp.Models;

/// <summary>
/// Event arguments that hold an array of timestamped data packages.
/// </summary>
public class TimestampedDataReceivedEvent : EventArgs
{
    /// <summary>
    /// Gets the timestamped data packages that has been received
    /// </summary>
    public UARTTimestampedData[] Packages { get; }

    /// <summary>
    /// Initializes a new isntance of the class.
    /// </summary>
    /// <param name="packages"> The received timestamped data packages </param>
    /// <exception cref="ArgumentNullException"> Is cast when the packages argument is null </exception>
    public TimestampedDataReceivedEvent(UARTTimestampedData[] packages)
    {
        // Null-checked assignment
        Packages = packages ?? throw new ArgumentNullException(nameof(packages));
    }
}