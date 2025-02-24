using ReactiveUI;
using System;
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
        private ComboBoxItem _selectedCommunicationInterface;
        public ComboBoxItem SelectedCommunicationInterface
        {
            get => _selectedCommunicationInterface;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCommunicationInterface, value);
                this.RaisePropertyChanged(nameof(IsCanSelected));
                this.RaisePropertyChanged(nameof(IsUartSelected));
            }
        }

        // Data binding for Conditional CAN/UART elements
        public bool IsCanSelected => _selectedCommunicationInterface.Content?.ToString() == "CAN";
        public bool IsUartSelected => _selectedCommunicationInterface.Content?.ToString() == "UART";
        
        // ---------- Constructor ---------- //
        // The constructor is used to initialize sidebar variable and listen on message bus for
        // when the header viewmodel sends an update of it.
        public SidebarViewModel()
        {
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
        
    }
}
