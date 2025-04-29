using System;
using Avalonia.Controls;
using Avalonia.VisualTree;
using RealtimePlottingApp.ViewModels;

namespace RealtimePlottingApp.Views;

public partial class HeaderView : UserControl
{
    public HeaderView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += OnAttachInitialized;
    }
    
    // Called once the control is fully added to the visual tree
    // to pass a Storage Provider to the ViewModel from a window.
    private void OnAttachInitialized(object? sender, EventArgs e)
    {
        // Pass the IStorageProvider from parent window to HeaderViewModel
        Window? parentWindow = this.FindAncestorOfType<Window>();
        if (parentWindow != null)
        {
            // Instantiate the ViewModel with IStorageProvider
            DataContext = new HeaderViewModel(parentWindow.StorageProvider);
        }
    }
}