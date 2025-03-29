using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RealtimePlottingApp.ViewModels;
using ScottPlot.Avalonia;
using ScottPlot;
using ScottPlot.Plottables;

namespace RealtimePlottingApp.Views;

public partial class GraphDiagramView : UserControl
{
    private AxisLine? _draggedTriggerLevel = null; // Holds a triggerlevel which is being dragged

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
        if (DataContext is GraphDiagramViewModel viewModel)
        {
            viewModel.LinePlot = this.Find<AvaPlot>("Plot");
            
            // Initialize mouse event handlers after setting LinePlot
            if (viewModel.LinePlot == null) return;
            viewModel.LinePlot.PointerPressed += LinePlot_PointerPressed;
            viewModel.LinePlot.PointerReleased += LinePlot_PointerReleased;
            viewModel.LinePlot.PointerMoved += LinePlot_PointerMoved;
        };
    }

    // --- Graph elements from View ---
    private AvaPlot? LinePlot => (DataContext as GraphDiagramViewModel)?.LinePlot;

    // =============== Private Mouse event Helper Methods =============== //
    // These methods are based on the ScottPlot WinForm example "DraggableAxisLines".
    // They has been modified to function for AvaloniaUI, and thus work cross-platform.
    // ScottPlot is licensed under the MIT License.
    // Copyright (c) 2018 ScottPlot
    // https://github.com/ScottPlot/ScottPlot
    private void LinePlot_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control linePlot) return;
        Point position = e.GetPosition(linePlot);
        AxisLine? lineUnderMouse = GetLineUnderMouse((float)position.X, (float)position.Y);
        if (lineUnderMouse is null) return;
        _draggedTriggerLevel = lineUnderMouse;
        LinePlot?.UserInputProcessor.Disable(); // Disable panning of plot while dragging
    }

    private void LinePlot_PointerReleased(object? sender, PointerEventArgs e)
    {
        _draggedTriggerLevel = null;
        LinePlot?.UserInputProcessor.Enable(); // Enable panning of plot when line is released
        LinePlot?.Refresh();
    }

    private void LinePlot_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (LinePlot == null) return;

        Point position = e.GetPosition(LinePlot);

        // Rectangle area around mouse in pixels
        CoordinateRect rect = LinePlot.Plot.GetCoordinateRect((float)position.X, (float)position.Y, radius: 10);

        if (_draggedTriggerLevel is null)
        {
            // Set cursor based on what's beneath the plottable
            AxisLine? lineUnderMouse = GetLineUnderMouse((float)position.X, (float)position.Y);
            if (lineUnderMouse is null)
                LinePlot.Cursor = new Cursor(StandardCursorType.Arrow);
            else
                LinePlot.Cursor = lineUnderMouse.IsDraggable switch
                {
                    true when lineUnderMouse is VerticalLine => new Cursor(StandardCursorType.SizeWestEast),
                    true when lineUnderMouse is HorizontalLine => new Cursor(StandardCursorType.SizeNorthSouth),
                    _ => LinePlot.Cursor
                };
        }
        else
        {
            // update the position of the plottable being dragged
            switch (_draggedTriggerLevel)
            {
                case HorizontalLine hl:
                    hl.Y = rect.VerticalCenter;
                    hl.Text = $"{hl.Y:0.00}";
                    break;
                case VerticalLine vl:
                    vl.X = rect.HorizontalCenter;
                    vl.Text = $"{vl.X:0.00}";
                    break;
            }

            LinePlot.Refresh();
        }
    }

    private AxisLine? GetLineUnderMouse(float x, float y)
    {
        if (LinePlot == null) return null;
        CoordinateRect rect = LinePlot.Plot.GetCoordinateRect(x, y, radius: 10);

        return LinePlot.Plot.GetPlottables<AxisLine>().Reverse().FirstOrDefault(axLine => axLine.IsUnderMouse(rect));
    }
}