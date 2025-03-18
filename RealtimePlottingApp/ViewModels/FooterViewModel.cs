using System;
using System.Diagnostics;
using System.Windows.Input;
using ReactiveUI;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class FooterViewModel : ViewModelBase
    {
        // --- Private variables --- //
        
        // --- Data binding variables --- // 
        // Data binding text to show connection status:
        private string _commInterfaceStatus = "Communication Interface: Not Configured";

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
                
                else if (msg.Equals("CANConnected"))
                {
                    CommInterfaceStatus = "CAN: Connected";
                }
                
                else if (msg.Equals("CANDisconnected"))
                {
                    CommInterfaceStatus = "CAN: Disconnected";
                }
                
                // Message containing CAN error statuses to display:
                else if (msg.StartsWith("CANError:"))
                {
                    CommInterfaceStatus = $"CAN Error: {msg[9..]}";
                }
            });
            
            // Initialize ICommands
            OnGithubClick = ReactiveCommand.Create(OpenGithubRepo);
        }
        
        // ---------- ICommand properties + methods for data binding ---------- //
        public ICommand OnGithubClick { get; }

        private static void OpenGithubRepo()
        {
            const string url = "https://github.com/O-Brob/RealtimePlottingApp";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occured while opening the Github repository: " + e);
            }
        }
        
    }
}