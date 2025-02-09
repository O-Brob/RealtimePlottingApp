using ReactiveUI;
using System;
using System.Windows.Input;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class HeaderViewModel : ViewModelBase
    {
        // The constructor is used to initialize our commands.
        public HeaderViewModel()
        {
            // Initialize commands using ReactiveCommand since all of them are Commands.
            SaveConfigCommand = ReactiveCommand.Create(SaveConfig);
            LoadConfigCommand = ReactiveCommand.Create(LoadConfig);
            ToggleSidebarCommand = ReactiveCommand.Create(ToggleSidebar);
            ShowLineGraphCommand = ReactiveCommand.Create(ShowLineGraph);
            ShowBlockDiagramCommand = ReactiveCommand.Create(ShowBlockDiagram);
        }
        
        // ICommand properties for data binding.
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ShowLineGraphCommand { get; }
        public ICommand ShowBlockDiagramCommand { get; }

        // ==================== SAVECONFIG ==================== //
        
        // Command handler for saving configuration.
        private void SaveConfig()
        {
            // TODO: Implement this command's logic
            Console.WriteLine("SaveConfig executed");
        }
        
        // ==================== LOADCONFIG ==================== //
        
        // Command handler for loading configuration.
        private void LoadConfig()
        {
            // TODO: Implement this command's logic
            Console.WriteLine("LoadConfig executed");
        }
        
        // ==================== TOGGLESIDEBAR ==================== //
        
        // Command handler for toggling the sidebar.
        private void ToggleSidebar()
        {
            // Sends message on bus for SidebarViewModel to receive.
            MessageBus.Current.SendMessage("ToggleSidebar");
            Console.WriteLine($"ToggleSidebar executed");
        }
        
        // ==================== SHOWLINEGRAPH ==================== //
        
        // Command handler for showing the line graph.
        private void ShowLineGraph()
        {
            // TODO: Implement this command's logic
            Console.WriteLine("ShowLineGraph executed");
        }
        
        // ==================== SHOWBLOCKDIAGRAM ==================== //
        
        // Command handler for showing the block diagram.
        private void ShowBlockDiagram()
        {
            // TODO: Implement this command's logic
            Console.WriteLine("ShowBlockDiagram executed");
        }
        
    }
}
