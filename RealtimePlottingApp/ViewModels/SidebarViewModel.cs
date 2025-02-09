﻿using ReactiveUI;
using System;
using System.Windows.Input;
using Avalonia.Threading;

namespace RealtimePlottingApp.ViewModels
{
    // Assume that ViewModelBase inherits from ReactiveObject.
    public class SidebarViewModel : ViewModelBase
    {
        // Private variables:
        private bool _showSidebar = true;
        public bool ShowSidebar => _showSidebar;
        
        // The constructor is used to initialize sidebar variable and listen on message bus for
        // when the header viewmodel sends an update of it.
        public SidebarViewModel()
        {
            // Initialize commands using ReactiveCommand since all of them are Commands.
            
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
        
        // ICommand properties for data binding.
        

        // ==================== METHOD1 ==================== //
        
        
        
    }
}
