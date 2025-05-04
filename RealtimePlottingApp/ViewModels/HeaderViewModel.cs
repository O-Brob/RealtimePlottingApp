using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveUI;
using System.Windows.Input;
using Avalonia.Platform.Storage;

namespace RealtimePlottingApp.ViewModels
{
    // ViewModelBase inherits from ReactiveObject.
    public class HeaderViewModel : ViewModelBase
    {
        // --- Local states: --- //
        // Storage Provider for file picker access
        private readonly IStorageProvider? _storageProvider;
        
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
        
        // Alternate constructor to pass a storageProvider
        public HeaderViewModel(IStorageProvider storageProvider)
        {
            // Initialize local storageprovider
            _storageProvider = storageProvider;
            
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
        private async void SaveConfig()
        {
            try
            {
                if (_storageProvider != null)
                {
                    // Open a save-file picker
                    IStorageFile? file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Save Config File",
                        FileTypeChoices = new List<FilePickerFileType>
                        {
                            new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
                        }
                    });

                    // If a "save file" was decided, set the path to it, otherwise fallback.
                    string? filePath = file?.TryGetLocalPath();
                    if (filePath != null) ConfigManager.FilePath = filePath;
                    else return; // Operation cancelled by user
                }
                else
                {
                    // Something prevents file picker from initializing.
                    // Use emergency fallback save path.
                    ConfigManager.FilePath = "appconfig.json"; // Fallback
                }

                // Sends message on bus that any state-owning ViewModel can
                // receive as an indicator to SAVE their states to a config.
                MessageBus.Current.SendMessage("SaveConfigRequest", "SaveConfigRequest");
            
                // Set a reasonable delay for config KeyVals to be added before exporting the config
                new Thread(() =>
                {
                    Thread.Sleep(1000);
                    try
                    {
                        ConfigManager.ExportConfig();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        // ==================== LOADCONFIG ==================== //
        
        // Command handler for loading configuration.
        private async void LoadConfig()
        {
            try
            {
                if (_storageProvider != null)
                {
                    // Open a load-file picker
                    IReadOnlyList<IStorageFile> files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load Config File",
                        AllowMultiple = false,  // Only allow selecting ONE file
                        FileTypeFilter = new List<FilePickerFileType>
                        {
                            new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
                        }
                    });

                    // Check if a file was selected
                    if (files.Count > 0)
                    {
                        string? filePath = files[0].TryGetLocalPath();
                        if (filePath != null) ConfigManager.FilePath = filePath;
                    }
                    else
                    {
                        return; // Operation is cancelled by user
                    }
                }
                else
                {
                    // Something prevents file picker from initializing.
                    // Use emergency fallback save path.
                    ConfigManager.FilePath = "appconfig.json";
                }
            
                // Sends message on bus that any state-owning ViewModel can
                // receive as an indicator that a config file has been read
                // and they should load their states from it.
                MessageBus.Current.SendMessage("LoadConfigRequest", "LoadConfigRequest");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
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
