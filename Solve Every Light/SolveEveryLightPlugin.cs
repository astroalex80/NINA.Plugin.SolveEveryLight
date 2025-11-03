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
public class SolveEveryLightPlugin : PluginBase, INotifyPropertyChanged, ISolveEveryLightOptions {
    private readonly IProfileService profileService;
    private readonly PluginOptionsAccessor pluginSettings;
    private SolveEveryLightSolver? solver;

    [ImportingConstructor]
    public SolveEveryLightPlugin(
        IProfileService profileService,
        IOptionsVM options,
        IImageSaveMediator imageSaveMediator,
        IPlateSolverFactory plateSolverFactory,
        IApplicationStatusMediator statusMediator) {
        if (Settings.Default.UpdateSettings) {
            Settings.Default.Upgrade();
            Settings.Default.UpdateSettings = false;
            CoreUtil.SaveSettings(Settings.Default);
        }

        this.profileService = profileService;

        this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));

        MigrateSettings();

        var settingsAdapter = new SolveEveryLightOptionsAccessor(pluginSettings);

        string pluginVersion = typeof(SolveEveryLightPlugin).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        solver = new SolveEveryLightSolver(
            imageSaveMediator,
            plateSolverFactory,
            statusMediator,
            profileService,
            settingsAdapter,
            this.Name,
            pluginVersion,
            CoreUtil.Version);

        profileService.ProfileChanged += ProfileService_ProfileChanged;
    }

    public bool PluginEnabled {
        get => pluginSettings.GetValueBoolean(nameof(PluginEnabled), Settings.Default.PluginEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(PluginEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool SnapshotsEnabled {
        get => pluginSettings.GetValueBoolean(nameof(SnapshotsEnabled), Settings.Default.SnapshotsEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(SnapshotsEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool NotificationsEnabled {
        get => pluginSettings.GetValueBoolean(nameof(NotificationsEnabled),
            Settings.Default.NotificationsEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(NotificationsEnabled), value);
            RaisePropertyChanged();
        }
    }

    public bool OptimizedSolverParameterEnabled {
        get => pluginSettings.GetValueBoolean(nameof(OptimizedSolverParameterEnabled),
            Settings.Default.OptimizedSolverParameterEnabled);
        set {
            pluginSettings.SetValueBoolean(nameof(OptimizedSolverParameterEnabled), value);
            RaisePropertyChanged();
        }
    }

    public int DownSampleFactor {
        get => pluginSettings.GetValueInt32(nameof(DownSampleFactor), Settings.Default.DownSampleFactor);
        set {
            pluginSettings.SetValueInt32(nameof(DownSampleFactor), value);
            RaisePropertyChanged();
        }
    }

    public double SearchRadius {
        get => pluginSettings.GetValueDouble(nameof(SearchRadius), Settings.Default.SearchRadius);
        set {
            pluginSettings.SetValueDouble(nameof(SearchRadius), value);
            RaisePropertyChanged();
        }
    }

    public int MaxObjects {
        get => pluginSettings.GetValueInt32(nameof(MaxObjects), Settings.Default.MaxObjects);
        set {
            pluginSettings.SetValueInt32(nameof(MaxObjects), value);
            RaisePropertyChanged();
        }
    }

    public override Task Teardown() {
        profileService.ProfileChanged -= ProfileService_ProfileChanged;
        solver?.Dispose();
        solver = null;
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
        pluginSettings.SetValueBoolean(nameof(OptimizedSolverParameterEnabled),
            Settings.Default.OptimizedSolverParameterEnabled);
        pluginSettings.SetValueInt32(nameof(DownSampleFactor), Settings.Default.DownSampleFactor);
        pluginSettings.SetValueDouble(nameof(SearchRadius), Settings.Default.SearchRadius);
        pluginSettings.SetValueInt32(nameof(MaxObjects), Settings.Default.MaxObjects);
        pluginSettings.SetValueBoolean("HasMigratedProperties", true);
    }
}