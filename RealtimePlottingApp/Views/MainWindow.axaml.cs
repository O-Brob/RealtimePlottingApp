using Avalonia.Controls;
using RealtimePlottingApp.ViewModels;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainWindowViewModel
            {
                LinePlot = this.FindControl<AvaPlot>("Plot"),
            };

            DataContext = viewModel;
        }
    }
}