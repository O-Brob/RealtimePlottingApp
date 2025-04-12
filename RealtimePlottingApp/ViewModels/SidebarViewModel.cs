using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class SidebarViewModel : ViewModelBase
    {
        // Private variables:
        public bool ShowSidebar { get; private set; } = true;

        private bool _isConnected; // false default
        
        // ---------- Data binding variables: ----------- //
        // Data binding for the selected ComboBoxItem
        private ComboBoxItem? _selectedCommunicationInterface;
        public ComboBoxItem? SelectedCommunicationInterface
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
        
        // Data binding for whether ComboBox should be locked or not
        private bool _commSelectorEnabled = true;

        public bool CommSelectorEnabled
        {
            get => _commSelectorEnabled;
            set => this.RaiseAndSetIfChanged(ref _commSelectorEnabled, value);
        }

        // Data binding for Conditional CAN/UART elements
        public bool IsCanSelected => _selectedCommunicationInterface?.Content?.ToString() == "CAN";
        public bool IsUartSelected => _selectedCommunicationInterface?.Content?.ToString() == "UART";
        
        // Data binding for Connect button's "isEnabled" field. Ensures required fields are filled in before connecting.
        public bool IsConnectReady => 
        (
            // Check UART has valid inputs, if selected.
            (IsUartSelected && (IsValidComPortFormat(_comPortInput)) && (_baudRateInput > 0)) 
                ||
            // Check CAN has valid inputs, if selected.
            (IsCanSelected && _canInterfaceInput?.Length > 0 && _canIdFilter is > 0
             // Ensure a variable 1 is masked. If any additional variable n exist, the variable n-1 must also.
             && _canDataMask.Any(c => c == '1') && 
             !_canDataMask.Where(c => c is >= '1' and <= '9')  // Only check the following for numbers:
                 .Any(c => c > '1' && !_canDataMask.Contains((char)(c - 1))))
        );
        
        // Update Frequency Slider.
        private int? _updateFrequencySlider = 100; // Default value

        public int? UpdateFrequencySlider
        {
            get => _updateFrequencySlider;
            set
            {
                if (value == null) return;
                this.RaiseAndSetIfChanged(ref _updateFrequencySlider, value);
                MessageBus.Current.SendMessage($"updateFrequency:{_updateFrequencySlider}");
            }
        }

        // Variable Amount Slider.
        private int? _variableAmountSlider = 75;

        public int? VariableAmountSlider
        {
            get => _variableAmountSlider;
            set
            {
                if (value == null) return;
                this.RaiseAndSetIfChanged(ref _variableAmountSlider, value);
                MessageBus.Current.SendMessage($"variableAmount:{_variableAmountSlider}");
            }
        }

        // Trigger Level enable checkbox
        private bool _trigChecked; // false default

        public bool TrigChecked
        {
            get => _trigChecked;
            set
            {
                this.RaiseAndSetIfChanged(ref _trigChecked, value);
                MessageBus.Current.SendMessage(_trigChecked, "TrigChecked");
            }
        }
        
        // Trigger Mode combobox dropdown
        private ComboBoxItem? _selectedTriggerMode;

        public ComboBoxItem? SelectedTriggerMode
        {
            get => _selectedTriggerMode;
            set
            {
                // Update the value and send the new trigger mode on the bus as a string.
                this.RaiseAndSetIfChanged(ref _selectedTriggerMode, value);
                MessageBus.Current.SendMessage(_selectedTriggerMode?.Content?.ToString(),
                    "SelectedTriggerMode");
            }
        }
        
        // ========== CAN Data Bindings ========== //
        // CAN-Interface Input
        private string? _canInterfaceInput = "";

        public string? CanInterfaceInput
        {
            get => _canInterfaceInput;
            set
            {
                this.RaiseAndSetIfChanged(ref _canInterfaceInput, value);
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }

        // Bit Rate
        private ComboBoxItem? _selectedBitRate;

        public ComboBoxItem? SelectedBitRate
        {
            get => _selectedBitRate;
            set
            {
                // On linux, we set bit rate via SocketCAN Configuration.
                // We give user information that this should be done here instead.
                if (OperatingSystem.IsLinux())
                {
                    if (value == null) return;
                    value.Content = "Set via SocketCAN";
                    this.RaiseAndSetIfChanged(ref _selectedBitRate, value);

                    return;
                }
                this.RaiseAndSetIfChanged(ref _selectedBitRate, value);
            }
        }

        public static bool CanBitrateEnabled => OperatingSystem.IsWindows(); // On Linux, we use SocketCan, not UI.
        
        // CAN ID Filter
        private int? _canIdFilter;

        public int? CanIdFilter
        {
            get => _canIdFilter;
            set
            {
                // Allow null value to be set, but don't allow IsConnectReady to be true then.
                this.RaiseAndSetIfChanged(ref _canIdFilter, value);
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }

        // CAN Data Payload Mask
        private string _canDataMask = "__:__:__:__:__:__:__:__"; // Default value, nothing masked.

        public string CanDataMask
        {
            get => _canDataMask;
            set
            {
                this.RaiseAndSetIfChanged(ref _canDataMask, value);
                this.RaisePropertyChanged(nameof(IsConnectReady));
            }
        }

        // ========== UART Data Bindings ========== //
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
        
        // Unique Variable Count input
        private int? _uniqueVariableCount = 1;

        public int? UniqueVariableCount
        {
            get => _uniqueVariableCount;
            set
            {
                // Never allow null values to actually be set, but allow the input field to be empty (for manual input)
                if(value != null)
                    this.RaiseAndSetIfChanged(ref _uniqueVariableCount, value);
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
        private ComboBoxItem? _selectedDataSize;

        public ComboBoxItem? SelectedDataSize
        {
            get => _selectedDataSize;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDataSize, value);
            }
        }
        
        // ========== CommInterface-independent Data Bindings ========== //
        // ConnectButton text
        private string _connectButtonText = "Connect";

        public string ConnectButtonText
        {
            get => _connectButtonText;
            set
            {
                this.RaiseAndSetIfChanged(ref _connectButtonText, value);
            }
        }
        
        // Variable List bindings (dynamic)
        private ObservableCollection<IVariableModel> _variables = [];
        public ObservableCollection<IVariableModel> Variables
        {
            get => _variables;
            set => this.RaiseAndSetIfChanged(ref _variables, value); 
        }

        // ---------- ICommand properties + methods for data binding ---------- //
        public ICommand ConnectButtonCommand { get; }

        private void ConnectButtonClicked()
        {
            if (IsUartSelected)
            {
                MessageBus.Current.SendMessage(
                    !_isConnected
                        ? $"ConnectUart:ComPort:{_comPortInput},BaudRate:{_baudRateInput},DataSize:{_selectedDataSize?.Content},UniqueVars:{_uniqueVariableCount}"
                        : "DisconnectUart");
            }
            else if (IsCanSelected)
            {
                MessageBus.Current.SendMessage(
                    !_isConnected
                    ? $"ConnectCan:CanInterface:{_canInterfaceInput},BitRate:{_selectedBitRate?.Content},CanIdFilter:{_canIdFilter},DataPayloadMask:{_canDataMask}"
                    : "DisconnectCan");
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
                // Toggle sidebar
                if (msg.Equals("ToggleSidebar"))
                {
                    ShowSidebar = !ShowSidebar;
                    this.RaisePropertyChanged(nameof(ShowSidebar));
                }
                
                // Connection successful, enable "Disconnect" button.
                else if (msg.Equals("UARTConnected") || msg.Equals("CANConnected"))
                {
                    _isConnected = true;
                    ConnectButtonText = "Disconnect";
                    CommSelectorEnabled = false; // Disable the Communication interface selector
                    this.RaisePropertyChanged(nameof(CommSelectorEnabled));
                    
                    // Update Plot Config window's variable list to match the count on successful connect:
                    ObservableCollection<IVariableModel> newList = [];
                    // For UART; add _uniqueVariableCount variables:
                    if (msg.Equals("UARTConnected"))
                    {
                        for (int i = 0; i < _uniqueVariableCount; i++)
                            newList.Add(new VariableModel { Name = $"Var {i+1}", IsChecked = true });
                    }
                    else if (msg.Equals("CANConnected"))
                    {
                        // For CAN; add a variable for each unique number in the mask:
                        int numberOfCanVars = new HashSet<char>(
                            _canDataMask.Where(c => char.IsDigit(c))
                        ).Count;
                        for(int i = 0; i < numberOfCanVars; i++)
                            newList.Add(new VariableModel { Name = $"Var {i+1}", IsChecked = true });
                    }
                    
                    Variables = newList;
                    
                    // Notify other views that want an up-to-date access to the variables from when connection is made.
                    MessageBus.Current.SendMessage(Variables, "VariableList");
                }
                
                // Disconnect successful, enable "Connect" button.
                else if (msg.Equals("UARTDisconnected") || msg.Equals("CANDisconnected"))
                {
                    _isConnected = false;
                    ConnectButtonText = "Connect";
                    CommSelectorEnabled = true;
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
                // To allow flexibility for different ports (/dev/ttyS*, /dev/pts* virtual port... we just check /dev/)
                return input.StartsWith("/dev/");
            }
            // No supported OS
            return false;
        }
        
    }
}
