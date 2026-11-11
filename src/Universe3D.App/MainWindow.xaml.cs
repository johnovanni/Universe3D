using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Universe3D.App.Controllers;
using Universe3D.App.ViewModels;
using Universe3D.Core.Catalogs;

namespace Universe3D.App;

/// <summary>Main window: wires the view model to the scene controller and handles the keyboard. View glue only.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SceneController _scene;

    public MainWindow(IDataSetStore store)
    {
        InitializeComponent();
        // Fit the default size to the screen's work area (laptops at 125–150% scaling are smaller than 1500×920).
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
        _vm = new MainViewModel(store);
        DataContext = _vm;
        _scene = new SceneController(GlControl, Overlay, _vm);
        int first = ParseViewArgs();
        _scene.Start();
        Closing += (_, _) => _scene.Dispose();
        Loaded += (_, _) =>
        {
            _vm.SelectedDataSet = _vm.DataSets[first];
            StartBenchIfRequested();
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBox || Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Home:
                _vm.HomeCommand.Execute(null);
                break;
            case Key.F:
                _vm.CenterOnSelectedCommand.Execute(null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
