using System.Windows;
using Universe3D.Core.Catalogs;

namespace Universe3D.App;

public partial class App : Application
{
    /// <summary>Creates the one data-set store (spec §6: the App reaches data only through <see cref="IDataSetStore"/>).</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IDataSetStore store = new SnapshotStore();
        var window = new MainWindow(store);
        MainWindow = window;
        window.Show();
    }
}
