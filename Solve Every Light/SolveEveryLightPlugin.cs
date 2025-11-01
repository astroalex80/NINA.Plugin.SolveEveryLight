using NINA.Core.Utility;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Settings = NINA.Plugin.SolveEveryLight.Properties.Settings;

namespace NINA.Plugin.SolveEveryLight;

/// <summary>
/// This class exports the IPluginManifest interface and will be used for the general plugin information and options
/// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
/// 
/// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
/// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "SolveEveryLight_Options" where SolveEveryLight corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
/// </summary>
[Export(typeof(IPluginManifest))]
public class SolveEveryLightPlugin : PluginBase, INotifyPropertyChanged {
    private readonly IPluginOptionsAccessor pluginSettings;

    private readonly IProfileService profileService;

    //private readonly IImageSaveMediator imageSaveMediator;
    private readonly IPlateSolverFactory plateSolverFactory;

    //private readonly IPlateSolver plateSolver;
    private readonly IApplicationStatusMediator applicationStatusMediator;
    private string pluginName;
    private string pluginVersion;

    public IProfileService ProfileService => profileService;
    public IPlateSolverFactory PlateSolverFactory => plateSolverFactory;
    public IApplicationStatusMediator ApplicationStatusMediator => applicationStatusMediator;


    [ImportingConstructor]
    public SolveEveryLightPlugin(IProfileService profileService, IOptionsVM options,
        IImageSaveMediator imageSaveMediator, IPlateSolverFactory plateSolverFactory,
        IApplicationStatusMediator applicationStatusMediator) {
        if (Settings.Default.UpdateSettings) {
            Settings.Default.Upgrade();
            Settings.Default.UpdateSettings = false;
            CoreUtil.SaveSettings(Settings.Default);
        }

        pluginName = this.Name;
        pluginVersion = this.Version.ToString();

        // This helper class can be used to store plugin settings that are dependent on the current profile
        this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
        MigrateSettings();
        this.profileService = profileService;
        // React on a changed profile
        profileService.ProfileChanged += ProfileService_ProfileChanged;

        this.plateSolverFactory = plateSolverFactory;
        this.applicationStatusMediator = applicationStatusMediator;
        new SolveEveryLightSolver(imageSaveMediator, this);
    }

    // for tests
    public SolveEveryLightPlugin(
        IProfileService profileService,
        IOptionsVM options,
        IImageSaveMediator imageSaveMediator,
        IPlateSolverFactory plateSolverFactory,
        IApplicationStatusMediator applicationStatusMediator,
        IPluginOptionsAccessor pluginOptionsAccessor,
        IApplicationStatusMediator applicationStatusMediatorTest) {
        this.pluginSettings = pluginOptionsAccessor;
        this.profileService = profileService;
        this.pluginSettings = pluginOptionsAccessor;
    }

    //internal static string IdentifierStatic => "9d4f7ba2-10f2-4373-bfcb-b4b3dcbe21db";

    public bool PluginEnabled {
        get => pluginSettings.GetValueBoolean(nameof(PluginEnabled), Properties.Settings.Default.PluginEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(PluginEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool SnapshotsEnabled {
        get => pluginSettings.GetValueBoolean(nameof(SnapshotsEnabled), Properties.Settings.Default.SnapshotsEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(SnapshotsEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool NotificationsEnabled {
        get => pluginSettings.GetValueBoolean(nameof(NotificationsEnabled),
            Properties.Settings.Default.NotificationsEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(NotificationsEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool OptimizedSolverParameterEnabled {
        get => pluginSettings.GetValueBoolean(nameof(OptimizedSolverParameterEnabled),
            Properties.Settings.Default.OptimizedSolverParameterEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(OptimizedSolverParameterEnabled), value);
            RaisePropertyChanged();
        }
    }

    public int DownSampleFactor {
        get => pluginSettings.GetValueInt32(nameof(DownSampleFactor), Properties.Settings.Default.DownSampleFactor);
        set {
            pluginSettings.SetValueInt32(nameof(DownSampleFactor), value);
            RaisePropertyChanged();
        }
    }

    public double SearchRadius {
        get => pluginSettings.GetValueDouble(nameof(SearchRadius), Properties.Settings.Default.SearchRadius);
        set {
            pluginSettings.SetValueDouble(nameof(SearchRadius), value);
            RaisePropertyChanged();
        }
    }

    public int MaxObjects {
        get => pluginSettings.GetValueInt32(nameof(MaxObjects), Properties.Settings.Default.MaxObjects);
        set {
            pluginSettings.SetValueInt32(nameof(MaxObjects), value);
            RaisePropertyChanged();
        }
    }

    public override Task Teardown() {
        profileService.ProfileChanged -= ProfileService_ProfileChanged;
        return base.Teardown();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ProfileService_ProfileChanged(object sender, EventArgs e) {
        MigrateSettings();
        RaisePropertyChanged(nameof(PluginEnabled));
        RaisePropertyChanged(nameof(SnapshotsEnabled));
    }

    private void MigrateSettings() {
        bool hasMigratedProperties = pluginSettings.GetValueBoolean("HasMigratedProperties", false);
        if (hasMigratedProperties) { return; }

        Logger.Debug("performing onetime migration of Solve Every Light plugin configuration for this profile");
        pluginSettings.SetValueBoolean(nameof(PluginEnabled), Settings.Default.PluginEnabled);
        pluginSettings.SetValueBoolean(nameof(SnapshotsEnabled), Settings.Default.SnapshotsEnabled);
        pluginSettings.SetValueBoolean(nameof(NotificationsEnabled), Settings.Default.NotificationsEnabled);
        pluginSettings.SetValueBoolean(nameof(OptimizedSolverParameterEnabled), Settings.Default.OptimizedSolverParameterEnabled);
        pluginSettings.SetValueInt32(nameof(DownSampleFactor), Settings.Default.DownSampleFactor);
        pluginSettings.SetValueDouble(nameof(SearchRadius), Settings.Default.SearchRadius);
        pluginSettings.SetValueInt32(nameof(MaxObjects), Settings.Default.MaxObjects);
        pluginSettings.SetValueBoolean("HasMigratedProperties", true);
    }
}