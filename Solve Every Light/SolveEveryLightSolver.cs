using Grpc.Core;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.ManifestDefinition;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Mediator;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.Core.Converters;

namespace NINA.Plugin.SolveEveryLight {

    public class SolveEveryLightSolver : IDisposable {
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IPlateSolverFactory plateSolverFactory;
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly IProfileService profileService;
        private readonly ISolveEveryLightOptions settings;

        private readonly string pluginName;
        private readonly string pluginVersion;
        private readonly string ninaVersion;

        private readonly ApplicationStatus applicationStatus;

        public SolveEveryLightSolver(
            IImageSaveMediator imageSaveMediator,
            IPlateSolverFactory plateSolverFactory,
            IApplicationStatusMediator applicationStatusMediator,
            IProfileService profileService,
            ISolveEveryLightOptions settings,
            string pluginName,
            string pluginVersion,
            string ninaVersion) {
            this.imageSaveMediator = imageSaveMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.applicationStatusMediator = applicationStatusMediator;
            this.applicationStatus = new ApplicationStatus();
            this.profileService = profileService;
            this.settings = settings;

            this.pluginName = pluginName;
            this.pluginVersion = pluginVersion;
            this.ninaVersion = ninaVersion;

            imageSaveMediator.BeforeImageSaved += BeforeImageSavedAsync;
        }

        private async Task BeforeImageSavedAsync(object sender, BeforeImageSavedEventArgs e) {
            if (e.Image == null) return;

            if (!settings.PluginEnabled) return;

            FileTypeEnum fileType = profileService.ActiveProfile.ImageFileSettings.FileType;

            if (fileType != FileTypeEnum.FITS && fileType != FileTypeEnum.XISF) return;

            string imageType = e.Image.MetaData.Image.ImageType;
            bool isLight = imageType.Equals("LIGHT", StringComparison.OrdinalIgnoreCase);
            bool isSnapshot = imageType.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase);

            if (!isLight && (!settings.SnapshotsEnabled || !isSnapshot)) return;

            try {
                Stopwatch stopwatch = Stopwatch.StartNew();

                int downsampleFactor = profileService.ActiveProfile.PlateSolveSettings.DownSampleFactor;
                int maxObjects = profileService.ActiveProfile.PlateSolveSettings.MaxObjects;
                double searchRadius = profileService.ActiveProfile.PlateSolveSettings.SearchRadius;

                if (settings.OptimizedSolverParameterEnabled) {
                    downsampleFactor = settings.DownSampleFactor;
                    searchRadius = settings.SearchRadius;
                    maxObjects = settings.MaxObjects;
                }

                int binning = e.Image?.MetaData?.Camera?.BinX ?? 1;

                Coordinates? telescopeCoords = e.Image?.MetaData?.Telescope?.Coordinates;
                Coordinates? targetCoords = e.Image?.MetaData?.Target?.Coordinates;

                static double? FiniteOrNull(double? v) =>
                    v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value) ? v.Value : (double?)null;

                double ra = FiniteOrNull(telescopeCoords?.RADegrees)
                            ?? FiniteOrNull(targetCoords?.RADegrees)
                            ?? 0.00;

                double dec = FiniteOrNull(telescopeCoords?.Dec)
                             ?? FiniteOrNull(targetCoords?.Dec)
                             ?? 0.00;

                double? fl = profileService.ActiveProfile.TelescopeSettings?.FocalLength;

                double focalLength = FiniteOrNull(fl) ?? 500;

                Epoch epoch = e.Image?.MetaData?.Telescope?.Coordinates?.Epoch ?? Epoch.J2000;

                if (ra == 0.00 && dec == 0.00) searchRadius = 180;

                PlateSolveParameter plateSolveParameter = new() {
                    Binning = binning,
                    BlindFailoverEnabled = false,
                    Coordinates = new Coordinates(
                        ra, dec,
                        epoch,
                        Coordinates.RAType.Degrees),
                    DisableNotifications = !settings.NotificationsEnabled,
                    DownSampleFactor = downsampleFactor,
                    FocalLength = focalLength,
                    MaxObjects = maxObjects,
                    PixelSize = e.Image.MetaData.Camera.PixelSize,
                    Regions = profileService.ActiveProfile.PlateSolveSettings.Regions,
                    SearchRadius = searchRadius
                };

                applicationStatus.Source = $"Plugin {pluginName}";

                IPlateSolver solver =
                    plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
                IImageSolver imageSolver = plateSolverFactory.GetImageSolver(solver, solver);

                var solverType = profileService.ActiveProfile.PlateSolveSettings.PlateSolverType;

                if (solverType != PlateSolverEnum.ASTAP && solverType != PlateSolverEnum.ASPS) {
                    Notification.ShowWarning(
                        "Solve Every Light plugin currently supports only ASTAP and All Sky Plate Solver. " +
                        "Please configure one of them under Options → Plate Solving."
                    );
                    return;
                }

                applicationStatus.Source = $"Plugin {pluginName}";
                applicationStatus.Status = "Plate solving";
                applicationStatusMediator.StatusUpdate(applicationStatus);

                IProgress<ApplicationStatus> progress = null;

                CancellationToken ct = CancellationToken.None;

                PlateSolveResult result = await imageSolver.Solve(e.Image, plateSolveParameter, progress, ct);

                if (result?.Success == true) {
                    AddWcsHeader(e.Image, result, pluginName, pluginVersion, ninaVersion);

                    stopwatch.Stop();
                    double elapsedSeconds = Math.Round((stopwatch.Elapsed.TotalMilliseconds / 1000), 3);

                    Logger.Info(
                        $"Plate solved {e.Image.MetaData.Image.ImageType} {e.Image.MetaData.Image.Id} and stored solution in header. Coordinates RA: {result.Coordinates.RAString} DEC: {result.Coordinates.DecString} Time to solve and write wcs header: {elapsedSeconds} sec.");

                    return;
                } else {
                    Logger.Error(
                        $"Plate solving of {e.Image.MetaData.Image.ImageType} {e.Image.MetaData.Image.Id} failed");
                }
            } catch (Exception ex) {
                Notification.ShowError("Could not solve image. Error message: " + ex.Message);
                Logger.Error("Could not solve image. Error message: " + ex.Message);
                Logger.Debug("Stack Trace: " + ex.StackTrace);
            } finally {
                applicationStatus.Status = string.Empty;
                applicationStatusMediator.StatusUpdate(applicationStatus);
            }
        }

        private static void AddWcsHeader(IImageData image, PlateSolveResult result, string pluginName,
            string pluginVersion, string ninaVersion) {
            double scaleDegPerPix = result.Pixscale / 3600.0;
            double paRad = result.PositionAngle * Math.PI / 180.0;
            double flip = result.Flipped ? -1.0 : 1.0;

            double cd11 = -flip * scaleDegPerPix * Math.Cos(paRad);
            double cd12 = -scaleDegPerPix * Math.Sin(paRad);
            double cd21 = scaleDegPerPix * Math.Sin(paRad);
            double cd22 = -flip * scaleDegPerPix * Math.Cos(paRad);

            double w = image.Properties.Width / 2.0;
            double h = image.Properties.Height / 2.0;

            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CTYPE1", "RA---TAN",
                "first parameter RA, projection TAN"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CTYPE2", "DEC--TAN",
                "second parameter DEC, projection TAN"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CUNIT1", "deg", "Unit of coordinates"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CUNIT2", "deg", "Unit of coordinates"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRVAL1", result.Coordinates.RADegrees,
                "RA of reference pixel (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRVAL2", result.Coordinates.Dec,
                "DEC of reference pixel (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRPIX1", w + 0.5, "X of reference pixel"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRPIX2", h + 0.5, "Y of reference pixel"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD1_1", cd11, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD1_2", cd12, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD2_1", cd21, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD2_2", cd22, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CDELT1", Math.Abs(scaleDegPerPix),
                "X pixel size (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CDELT2", Math.Abs(scaleDegPerPix),
                "Y pixel size (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CROTA1", result.PositionAngle,
                "Image twist X axis (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CROTA2", result.PositionAngle,
                "Image twist Y axis (deg)"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("PLTSOLVD1", "T",
                $"N.I.N.A. {ninaVersion} Plugin: {pluginName}"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("PLTSOLVD2", "T",
                $"Plugin Version: {pluginVersion} using ASTAP"));
        }

        public void Dispose() {
            imageSaveMediator.BeforeImageSaved -= BeforeImageSavedAsync;
        }
    }
}