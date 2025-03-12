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
    /// Gets or sets a value indicating whether the variable is selected.
    /// Notifies UI when changed!
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}