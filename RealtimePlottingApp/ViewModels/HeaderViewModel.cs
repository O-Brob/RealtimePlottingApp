using ReactiveUI;
using System;
using System.Windows.Input;
using Avalonia.Threading;

namespace RealtimePlottingApp.ViewModels
{
    // Assume that ViewModelBase inherits from ReactiveObject.
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
        // Private boolean holding boolean for if sidebar should show.
        private bool _showSidebar = true;

        // Getter/Setter for showSidebar
        public bool ShowSidebar
        {
            get => _showSidebar;
            set => _showSidebar = value;
        }
        
        // Command handler for toggling the sidebar.
        private void ToggleSidebar()
        {
            ShowSidebar = !ShowSidebar;
            Console.WriteLine($"ShowSidebar: {_showSidebar}");
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
