using System.Windows;
using System.Windows.Media.Animation;

namespace LaboratoireProgrammation;
public partial class App : Application {
    protected override void OnStartup(StartupEventArgs e) {
        bool isGpuActive = GpuEnforcer.Initialize(restartIfPreferenceChanged: true);
        base.OnStartup(e);
    }
}