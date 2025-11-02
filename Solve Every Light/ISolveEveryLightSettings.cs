using NINA.Plugin.SolveEveryLight.Properties;
using NINA.Profile.Interfaces;
using System;

namespace NINA.Plugin.SolveEveryLight {
    public interface ISolveEveryLightOptions {
        bool PluginEnabled { get; }
        bool SnapshotsEnabled { get; }
        bool NotificationsEnabled { get; }
        bool OptimizedSolverParameterEnabled { get; }
        int DownSampleFactor { get; }
        double SearchRadius { get; }
        int MaxObjects { get; }
    }
    public sealed class SolveEveryLightOptionsAccessor(IPluginOptionsAccessor s) : ISolveEveryLightOptions {

        private readonly IPluginOptionsAccessor s = s ?? throw new ArgumentNullException(nameof(s));
        public bool PluginEnabled => s.GetValueBoolean(nameof(PluginEnabled), Settings.Default.PluginEnabled);
        public bool SnapshotsEnabled => s.GetValueBoolean(nameof(SnapshotsEnabled), Settings.Default.SnapshotsEnabled);
        public bool NotificationsEnabled => s.GetValueBoolean(nameof(NotificationsEnabled), Settings.Default.NotificationsEnabled);
        public bool OptimizedSolverParameterEnabled => s.GetValueBoolean(nameof(OptimizedSolverParameterEnabled), Settings.Default.OptimizedSolverParameterEnabled);
        public int DownSampleFactor => s.GetValueInt32(nameof(DownSampleFactor), Settings.Default.DownSampleFactor);
        public double SearchRadius => s.GetValueDouble(nameof(SearchRadius), Settings.Default.SearchRadius);
        public int MaxObjects => s.GetValueInt32(nameof(MaxObjects), Settings.Default.MaxObjects);
    }
}
