using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SolveEveryLight {
    public class SolveEveryLightSolver {
        private readonly SolveEveryLightPlugin plugin;
        private readonly string pluginName;
        private readonly string pluginVersion;

        private readonly IApplicationStatusMediator applicationStatusMediator;

        private readonly ApplicationStatus applicationStatus;

        public SolveEveryLightSolver(IImageSaveMediator imageSaveMediator, SolveEveryLightPlugin plugin) {
            this.plugin = plugin;
            this.pluginName = plugin.Name;

            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            this.pluginVersion = version?.ToString();

            imageSaveMediator.BeforeImageSaved += ImageSaveMediator_BeforeImageSaved;
            this.applicationStatusMediator = plugin.ApplicationStatusMediator;
            this.applicationStatus = new ApplicationStatus();
        }

        private async Task ImageSaveMediator_BeforeImageSaved(object sender, BeforeImageSavedEventArgs e) {
            if (!plugin.PluginEnabled) return;

            FileTypeEnum fileType = plugin.ProfileService.ActiveProfile.ImageFileSettings.FileType;

            if (fileType != FileTypeEnum.FITS && fileType != FileTypeEnum.XISF) return;

            string imageType = e.Image.MetaData.Image.ImageType;
            bool isLight = imageType.Equals("LIGHT", StringComparison.OrdinalIgnoreCase);
            bool isSnapshot = imageType.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase);

            if (!isLight && (!plugin.SnapshotsEnabled || !isSnapshot)) return;

            applicationStatus.Source = $"Plugin {pluginName}";
            applicationStatus.Status = "Plate solving";

            applicationStatusMediator.StatusUpdate(applicationStatus);

            Stopwatch stopwatch = Stopwatch.StartNew();

            int downsampleFactor = plugin.ProfileService.ActiveProfile.PlateSolveSettings.DownSampleFactor;
            int maxObjects = plugin.ProfileService.ActiveProfile.PlateSolveSettings.MaxObjects;
            double searchRadius = plugin.ProfileService.ActiveProfile.PlateSolveSettings.SearchRadius;

            if (plugin.OptimizedSolverParameterEnabled) {
                downsampleFactor = plugin.DownSampleFactor;
                searchRadius = plugin.SearchRadius;
                maxObjects = plugin.MaxObjects;
            }

            PlateSolveParameter plateSolveParameter = new PlateSolveParameter {
                Binning = e.Image.MetaData.Camera.BinX,
                BlindFailoverEnabled = false,
                Coordinates = new Astrometry.Coordinates(
                    Angle.ByDegree(e.Image.MetaData.Telescope.Coordinates.RA),
                    Angle.ByDegree(e.Image.MetaData.Telescope.Coordinates.Dec),
                    e.Image.MetaData.Target.Coordinates.Epoch,
                    DateTime.Now),
                DisableNotifications = !plugin.NotificationsEnabled,
                DownSampleFactor = downsampleFactor,
                FocalLength = e.Image.MetaData.Telescope.FocalLength,
                MaxObjects = maxObjects,
                PixelSize = e.Image.MetaData.Camera.PixelSize,
                Regions = plugin.ProfileService.ActiveProfile.PlateSolveSettings.Regions,
                SearchRadius = searchRadius
            };

            IPlateSolver solver =
                plugin.PlateSolverFactory.GetPlateSolver(plugin.ProfileService.ActiveProfile.PlateSolveSettings);

            IProgress<ApplicationStatus> progress = null;

            CancellationToken ct = CancellationToken.None;

            PlateSolveResult result = await solver.SolveAsync(e.Image, plateSolveParameter, progress, ct);

            if (result?.Success == true) {
                AddWcsHeader(e.Image, result, pluginName, pluginVersion);

                stopwatch.Stop();
                double elapsedSeconds = stopwatch.Elapsed.TotalMilliseconds / 1000;

                Logger.Info(
                    $"Plate solved {e.Image.MetaData.Image.ImageType} {e.Image.MetaData.Image.Id} and stored solution in header. Coordinates RA: {result.Coordinates.RAString} DEC: {result.Coordinates.DecString} Time to solve and write wcs header: {elapsedSeconds} sec.");

                return;
            } else {
                Logger.Error($"Plate solving of {e.Image.MetaData.Image.ImageType} {e.Image.MetaData.Image.Id} failed");
            }

            applicationStatus.Status = string.Empty;
            applicationStatusMediator.StatusUpdate(applicationStatus);
        }

        private static void AddWcsHeader(IImageData image, PlateSolveResult result, string pluginName,
            string pluginVersion) {
            double scaleDegPerPix = result.Pixscale / 3600.0;
            double paRad = result.PositionAngle * Math.PI / 180.0;
            double flip = result.Flipped ? -1.0 : 1.0;

            double cd11 = -flip * scaleDegPerPix * Math.Cos(paRad);
            double cd12 = -scaleDegPerPix * Math.Sin(paRad);
            double cd21 = scaleDegPerPix * Math.Sin(paRad);
            double cd22 = -flip * scaleDegPerPix * Math.Cos(paRad);

            double w = image.Properties.Width / 2.0;
            double h = image.Properties.Height / 2.0;

            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CTYPE1", "RA---TAN", "first parameter RA, projection TAN"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CTYPE2", "DEC--TAN", "second parameter DEC, projection TAN"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CUNIT1", "deg", "Unit of coordinates"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("CUNIT2", "deg", "Unit of coordinates"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRVAL1", result.Coordinates.RADegrees, "RA of reference pixel (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRVAL2", result.Coordinates.Dec, "DEC of reference pixel (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRPIX1", w + 0.5, "X of reference pixel"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CRPIX2", h + 0.5, "Y of reference pixel"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD1_1", cd11, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD1_2", cd12, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD2_1", cd21, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CD2_2", cd22, ""));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CDELT1", Math.Abs(scaleDegPerPix), "X pixel size (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CDELT2", Math.Abs(scaleDegPerPix), "Y pixel size (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CROTA1", result.PositionAngle, "Image twist X axis (deg)"));
            image.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("CROTA2", result.PositionAngle, "Image twist Y axis (deg)"));
            image.MetaData.GenericHeaders.Add(new StringMetaDataHeader("PLTSOLVD", "T", $"N.I.N.A. Plugin: {pluginName}, version: {pluginVersion}"));
        }
    }
}