using ReactiveUI;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class SidebarViewModel : ViewModelBase
    {
        // Private variables:
        private bool _showSidebar = true;
        public bool ShowSidebar => _showSidebar;
        
        // ---------- Data binding variables: ----------- //
        // Data binding for the selected ComboBoxItem
        private ComboBoxItem _selectedCommunicationInterface = null;
        public ComboBoxItem SelectedCommunicationInterface
        {
            get => _selectedCommunicationInterface;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCommunicationInterface, value);
                this.RaisePropertyChanged(nameof(IsCanSelected));
                this.RaisePropertyChanged(nameof(IsUartSelected));
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }

        // Data binding for Conditional CAN/UART elements
        public bool IsCanSelected => _selectedCommunicationInterface.Content?.ToString() == "CAN";
        public bool IsUartSelected => _selectedCommunicationInterface.Content?.ToString() == "UART";
        
        // Data binding for Connect button's "isEnabled" field. Ensures required fields are filled in before connecting.
        public bool IsConnectReady => (IsUartSelected && (IsValidComPortFormat(_comPortInput)) && (_baudRateInput > 0) );
        
        // ----- CAN Data Bindings ----- //
        // TODO : Implement CAN data bindings
        
        // ----- UART Data Bindings ----- //
        // COM-Port Input
        private string? _comPortInput;

        public string? ComPortInput
        {
            get => _comPortInput;
            set
            {
                this.RaiseAndSetIfChanged(ref _comPortInput, value);
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }

        // Baud-rate Input
        private int? _baudRateInput;

        public int? BaudRateInput
        {
            get => _baudRateInput;
            // Value in this set is a decimal value from UI, implicit typecast occurs here.
            set
            {
                if(value >= 0)
                {
                    this.RaiseAndSetIfChanged(ref _baudRateInput, value);
                }
                else // "null"(nothing) or invalid number was input. Set to 0 instead!
                {
                    this.RaiseAndSetIfChanged(ref _baudRateInput, 0);
                }
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }
        
        // Payload data size Input
        private ComboBoxItem _selectedDataSize = null;

        public ComboBoxItem SelectedDataSize
        {
            get => _selectedDataSize;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDataSize, value);
            }
        }
        
        // ---------- ICommand properties + methods for data binding ---------- //
        public ICommand ConnectButtonCommand { get; }

        private void ConnectButtonClicked()
        {
            if (IsUartSelected)
            {
                MessageBus.Current.SendMessage
                    ($"ConnectUart:ComPort:{_comPortInput},BaudRate:{_baudRateInput},DataSize:{_selectedDataSize.Content}");
            }
            else if (IsCanSelected)
            {
                // TODO: Implement CAN Connection request message
            }
        }
        // ---------- Constructor ---------- //
        // The constructor is used to initialize sidebar variable and listen on message bus for
        // when the header viewmodel sends an update of it.
        public SidebarViewModel()
        {
            // Initialize ICommands
            ConnectButtonCommand = ReactiveCommand.Create(ConnectButtonClicked);
            
            // Initialize Messagebus for sidebar toggling via HeaderViewModel.
            MessageBus.Current.Listen<string>().Subscribe((msg) =>
            {
                if (msg.Equals("ToggleSidebar"))
                {
                    _showSidebar = !_showSidebar;
                    this.RaisePropertyChanged(nameof(ShowSidebar));
                }
            });
        }
        
        // ---------- Private Helper Methods ---------- //
        // Helper method to validate COM-Port input format (Windows and Linux)
        private static bool IsValidComPortFormat(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            
            // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Regex for Windows COM ports (COM1, COM2, etc.)
                var windowsRegex = new Regex(@"^COM\d+$", RegexOptions.IgnoreCase);
                return windowsRegex.IsMatch(input);
            }
            
            // Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Regex for Linux TTY ports (/dev/ttyS0, /dev/ttyS1, etc.)
                var linuxRegex = new Regex(@"^/dev/ttyS\d+$", RegexOptions.IgnoreCase);
                return linuxRegex.IsMatch(input);
            }
            // No supported OS
            return false;
        }
        
    }
}
