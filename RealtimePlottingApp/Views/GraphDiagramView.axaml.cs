using Avalonia.Controls;
using RealtimePlottingApp.ViewModels;
using ScottPlot.Avalonia;

namespace RealtimePlottingApp.Views;

public partial class GraphDiagramView : UserControl
{
    public GraphDiagramView()
    {
        InitializeComponent();

        // Unfortunately ScottPlot dev intentionally decided to not
        // allow interactions via Data binding (MVVM standard),
        // to enable total control of frame rendering and
        // allow for raw access to array values for plotting.
        // Therefore we let the view assign the plots to the ViewModel,
        // allowing greater performance at the cost of tighter coupling for these elements.
        // (see: https://scottplot.net/faq/mvvm/)
        var graphDiagramViewModel = new GraphDiagramViewModel
        {
            LinePlot = this.Find<AvaPlot>("Plot"),
        };
        
    }
}