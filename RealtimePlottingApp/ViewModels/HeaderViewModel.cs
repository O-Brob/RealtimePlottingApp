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
            ToggleLineGraphCommand = ReactiveCommand.Create(ToggleLineGraph);
            ToggleBlockDiagramCommand = ReactiveCommand.Create(ToggleBlockDiagram);
        }
        
        // ICommand properties for data binding.
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleLineGraphCommand { get; }
        public ICommand ToggleBlockDiagramCommand { get; }

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
        }
        
        // ==================== TOGGLELINEGRAPH ==================== //
        
        // Command handler for showing the line graph.
        private void ToggleLineGraph()
        {
            MessageBus.Current.SendMessage("ToggleLineGraph");
        }
        
        // ==================== TOGGLEBLOCKDIAGRAM ==================== //
        
        // Command handler for showing the block diagram.
        private void ToggleBlockDiagram()
        {
            MessageBus.Current.SendMessage("ToggleBlockDiagram");
        }
        
    }
}
