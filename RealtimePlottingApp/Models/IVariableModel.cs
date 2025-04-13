using System.ComponentModel;
namespace RealtimePlottingApp.Models;

public interface IVariableModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the name of the variable.
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether
    /// the variable is selected in some regard.
    /// </summary>
    bool IsChecked { get; set; }
    
    /// <summary>
    /// Gets or sets a state indicating whether
    /// a variable should be considered part of the
    /// trigger channel.
    /// </summary>
    bool IsTriggerable { get; set; }
}