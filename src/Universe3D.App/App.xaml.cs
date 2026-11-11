using System.Net.Http;
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
        // A full quasar catalog download is large, so allow a long timeout; the Cancel button ends it sooner.
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        var downloads = new DownloadService(store, new VizierClient(http));
        var window = new MainWindow(store, downloads);
        MainWindow = window;
        window.Show();
    }
}
