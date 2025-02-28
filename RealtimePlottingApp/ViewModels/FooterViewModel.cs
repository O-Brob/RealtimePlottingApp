using System;
using ReactiveUI;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class FooterViewModel : ViewModelBase
    {
        // --- Private variables --- //
        
        // --- Data binding variables --- // 
        // Data binding text to show connection status:
        private string _commInterfaceStatus = "Comm. Interface: Disconnected";

        public string CommInterfaceStatus
        {
            get => _commInterfaceStatus;
            set => this.RaiseAndSetIfChanged(ref _commInterfaceStatus, value);
        }
        
        // The constructor
        public FooterViewModel()
        {
            // --- Initialize MessageBus for incoming messages --- //
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                // Message telling us a UART has connected successfully:
                if (msg.Equals("UARTConnected"))
                {
                    CommInterfaceStatus = "UART: Connected";
                    this.RaisePropertyChanged(nameof(CommInterfaceStatus));
                }
                
                // Message telling us a UART has disconnected successfully:
                else if (msg.Equals("UARTDisconnected"))
                {
                    CommInterfaceStatus = "UART: Disconnected";
                    this.RaisePropertyChanged(nameof(CommInterfaceStatus));
                }
                
                // Message containing UART error statuses to display:
                else if (msg.StartsWith("UARTError:"))
                {
                    CommInterfaceStatus = $"UART Error: {msg[10..]}"; // Trim string identifier
                }
            });

        }
        
        
    }
}