using ReactiveUI;
using RealtimePlottingApp.Services.ConfigHandler;

namespace RealtimePlottingApp.ViewModels;

public class ViewModelBase : ReactiveObject
{
    // ========= Shared ViewModel Config Services ========== //
    
    // Every viewmodel will use this shared singleton instance
    // to manage saving / loading data from persistent config files:
    protected IConfigService ConfigManager => ConfigService.Instance;
}
