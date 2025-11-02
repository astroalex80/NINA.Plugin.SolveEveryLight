using FluentAssertions;
using Moq;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.SolveEveryLight;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SolveEveryLight.Test
{
    internal class SolveEveryLightPluginTest : IDisposable
    {
        private Mock<IImageSaveMediator> imageSaveMediatorMock;
        private Mock<IPlateSolverFactory> plateSolverFactoryMock;
        private Mock<IPlateSolver> plateSolverMock;
        private Mock<IProfileService> profileServiceMock;
        private Mock<IApplicationStatusMediator> applicationsStatusMediatorMock;
        private Mock<IPluginOptionsAccessor> pluginOptionsAccessorMock;

        private SolveEveryLightSolver solver;
        private ImageFileSettings imageFileSettings;

        [SetUp]
        public void Setup()
        {
            imageSaveMediatorMock = new Mock<IImageSaveMediator>();
            plateSolverFactoryMock = new Mock<IPlateSolverFactory>();
            plateSolverMock = new Mock<IPlateSolver>();
            profileServiceMock = new Mock<IProfileService>();
            applicationsStatusMediatorMock = new Mock<IApplicationStatusMediator>();

            applicationsStatusMediatorMock
                .Setup(m => m.StatusUpdate(It.IsAny<ApplicationStatus>()))
                .Verifiable();

            plateSolverFactoryMock
                .Setup(f => f.GetPlateSolver(It.IsAny<PlateSolveSettings>()))
                .Returns(plateSolverMock.Object);

            plateSolverFactoryMock
                .Setup(f => f.GetBlindSolver(It.IsAny<PlateSolveSettings>()))
                .Returns(plateSolverMock.Object);

            imageFileSettings = new ImageFileSettings { FileType = FileTypeEnum.FITS };

            var profile = new Mock<IProfile>();
            profile.Setup(p => p.ImageFileSettings).Returns(imageFileSettings);
            profile.Setup(p => p.PlateSolveSettings)
                   .Returns(new PlateSolveSettings { DownSampleFactor = 2, MaxObjects = 100, SearchRadius = 2.0 });
            profileServiceMock.Setup(p => p.ActiveProfile).Returns(profile.Object);

            // In-Memory Store für PluginOptions-Mock
            var store = new Dictionary<string, object>();
            pluginOptionsAccessorMock = new Mock<IPluginOptionsAccessor>();

            pluginOptionsAccessorMock
                .Setup(p => p.GetValueBoolean(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((string k, bool def) => store.TryGetValue(k, out var v) ? (bool)v : def);
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueInt32(It.IsAny<string>(), It.IsAny<int>()))
                .Returns((string k, int def) => store.TryGetValue(k, out var v) ? (int)v : def);
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueDouble(It.IsAny<string>(), It.IsAny<double>()))
                .Returns((string k, double def) => store.TryGetValue(k, out var v) ? (double)v : def);
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueString(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string k, string def) => store.TryGetValue(k, out var v) ? (string)v : def);
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueGuid(It.IsAny<string>(), It.IsAny<Guid>()))
                .Returns((string k, Guid def) => store.TryGetValue(k, out var v) ? (Guid)v : def);

            pluginOptionsAccessorMock
                .Setup(p => p.SetValueBoolean(It.IsAny<string>(), It.IsAny<bool>()))
                .Callback((string k, bool val) => store[k] = val);
            pluginOptionsAccessorMock
                .Setup(p => p.SetValueInt32(It.IsAny<string>(), It.IsAny<int>()))
                .Callback((string k, int val) => store[k] = val);
            pluginOptionsAccessorMock
                .Setup(p => p.SetValueDouble(It.IsAny<string>(), It.IsAny<double>()))
                .Callback((string k, double val) => store[k] = val);
            pluginOptionsAccessorMock
                .Setup(p => p.SetValueString(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string k, string val) => store[k] = val);
            pluginOptionsAccessorMock
                .Setup(p => p.SetValueGuid(It.IsAny<string>(), It.IsAny<Guid>()))
                .Callback((string k, Guid val) => store[k] = val);


            var settingsAdapter = new SolveEveryLightOptionsAccessor(pluginOptionsAccessorMock.Object);

            solver = new SolveEveryLightSolver(
                imageSaveMediatorMock.Object,
                plateSolverFactoryMock.Object,
                applicationsStatusMediatorMock.Object,
                profileServiceMock.Object,
                settingsAdapter,
                pluginName: "SolveEveryLight",
                pluginVersion: "1.0.0-test",
                ninaVersion: "3.x-test");
        }

        [Test]
        [TestCase(false, "LIGHT", false, FileTypeEnum.FITS)]    // plugin disabled
        [TestCase(true, "LIGHT", false, FileTypeEnum.RAW)]      // plugin enabled, RAW file
        [TestCase(true, "LIGHT", false, FileTypeEnum.TIFF)]     // plugin enabled, TIFF file
        [TestCase(true, "SNAPSHOT", true, FileTypeEnum.RAW)]    // snapshot enabled, RAW file
        [TestCase(true, "SNAPSHOT", false, FileTypeEnum.FITS)]  // snapshot disabled
        public async Task ShouldNotSolve(bool pluginEnabled, string frameType, bool snapShotsEnabled, FileTypeEnum fileType)
        {
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueBoolean("PluginEnabled", It.IsAny<bool>()))
                .Returns(pluginEnabled);

            pluginOptionsAccessorMock
                .Setup(p => p.GetValueBoolean("SnapshotsEnabled", It.IsAny<bool>()))
                .Returns(snapShotsEnabled);

            imageFileSettings.FileType = fileType;

            var args = CreateMockArgs(frameType);

            await InvokeBeforeImageSaved(solver, args);

            plateSolverMock.Verify(x => x.SolveAsync(
                It.IsAny<IImageData>(),
                It.IsAny<PlateSolveParameter>(),
                It.IsAny<IProgress<ApplicationStatus>>(),
                It.IsAny<CancellationToken>()), Times.Never);

            applicationsStatusMediatorMock.Verify(m => m.StatusUpdate(It.IsAny<ApplicationStatus>()), Times.Never);
        }

        [Test]
        [TestCase(true, "LIGHT", false, FileTypeEnum.FITS)]
        [TestCase(true, "LIGHT", false, FileTypeEnum.XISF)]
        [TestCase(true, "LIGHT", true, FileTypeEnum.FITS)]
        [TestCase(true, "SNAPSHOT", true, FileTypeEnum.FITS)]
        public async Task ShouldSolve(bool pluginEnabled, string frameType, bool snapShotsEnabled, FileTypeEnum fileType)
        {
            pluginOptionsAccessorMock
                .Setup(p => p.GetValueBoolean("PluginEnabled", It.IsAny<bool>()))
                .Returns(pluginEnabled);

            pluginOptionsAccessorMock
                .Setup(p => p.GetValueBoolean("SnapshotsEnabled", It.IsAny<bool>()))
                .Returns(snapShotsEnabled);

            imageFileSettings.FileType = fileType;

            var args = CreateMockArgs(frameType);

            var testResult = new PlateSolveResult
            {
                Success = true,
                Pixscale = 1.0,
                PositionAngle = 0,
                Flipped = false,
                Coordinates = new Coordinates(10, 10, Epoch.J2000, Coordinates.RAType.Degrees)
            };

            plateSolverMock.Setup(x => x.SolveAsync(
                    It.IsAny<IImageData>(),
                    It.IsAny<PlateSolveParameter>(),
                    It.IsAny<IProgress<ApplicationStatus>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResult);

            await InvokeBeforeImageSaved(solver, args);

            plateSolverMock.Verify(x => x.SolveAsync(
                    It.IsAny<IImageData>(),
                    It.IsAny<PlateSolveParameter>(),
                    It.IsAny<IProgress<ApplicationStatus>>(),
                    It.IsAny<CancellationToken>()), Times.Once);

            args.Image.MetaData.GenericHeaders.Should().Contain(h => h.Key == "CTYPE1");
        }

        private static async Task InvokeBeforeImageSaved(SolveEveryLightSolver solver, BeforeImageSavedEventArgs args)
        {
            var mi = typeof(SolveEveryLightSolver).GetMethod("BeforeImageSavedAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null);
            var task = mi.Invoke(solver, new object[] { null, args }) as Task;
            await (task ?? Task.CompletedTask);
        }

        private static BeforeImageSavedEventArgs CreateMockArgs(string imageType)
        {
            var image = new Mock<IImageData>();
            var meta = new ImageMetaData
            {
                Image = { ImageType = imageType },
                Camera = { PixelSize = 3.76, BinX = 1 },
                Telescope = { FocalLength = 990, Coordinates = new Coordinates(10, 10, Epoch.J2000, Coordinates.RAType.Degrees) },
                Target = { Coordinates = new Coordinates(10, 10, Epoch.J2000, Coordinates.RAType.Degrees) }
            };

            image.Setup(i => i.MetaData).Returns(meta);
            image.Setup(i => i.Properties).Returns(new ImageProperties(9576, 6388, 16, false, 1, 1));

            var renderedImageMock = new Mock<IRenderedImage>();
            var renderedImageTask = Task.FromResult(renderedImageMock.Object);
            return new BeforeImageSavedEventArgs(image.Object, renderedImageTask);
        }

        public void Dispose()
        {
            solver?.Dispose();
            imageSaveMediatorMock.Object.Shutdown();
        }
    }
}
