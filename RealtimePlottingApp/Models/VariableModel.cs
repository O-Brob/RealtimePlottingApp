using ReactiveUI;

namespace RealtimePlottingApp.Models;

/// <summary>
/// Represents a selectable variable item with a name and a checkbox state.
/// Supports notifications on property changes for UI updates.
/// </summary>
public class VariableModel : ReactiveObject, IVariableModel
{
    private string _name = "Variable";
    private bool _isChecked = true;
    private bool _isTriggerable = false;

    /// <summary>
    /// Gets or sets the name of the variable.
    /// Notifies UI when changed!
    /// </summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the variable is selected
    /// and should be shown/visible on the graph.
    /// Notifies UI when changed!
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    /// <summary>
    /// Gets or sets a state indicating whether
    /// this variable should be considered part of the
    /// trigger channel.
    /// </summary>
    public bool IsTriggerable
    {
        get => _isTriggerable;
        set
        {
            this.RaiseAndSetIfChanged(ref _isTriggerable, value);
            MessageBus.Current.SendMessage("TriggerableChanged","TriggerUpdate");
        }
    }
}