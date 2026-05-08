using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GDD.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    public BrowserCellViewModel Cell { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    public OverlayViewModel(BrowserCellViewModel cell, string deviceName, double width, double height)
    {
        Cell = cell;
        _title = $"{cell.PlayerName} — {deviceName}";
        _width = width;
        _height = height;
    }

    [RelayCommand]
    private void Close()
    {
        Cell.IsOverlayOpen = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CloseRequested;
}
