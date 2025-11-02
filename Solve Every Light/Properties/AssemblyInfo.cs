using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9d4f7ba2-10f2-4373-bfcb-b4b3dcbe21db")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Solve Every Light")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("A plugin that plate solves automatically every light frame (optionally snapshots) and writes the astrometric solution to the header of FITS or XISF files.")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("astroalex80")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Solve Every Light")]
[assembly: AssemblyCopyright("Copyright © 2025 astroalex80")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.2017")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your plugin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/astroalex80/NINA.Plugin.SolveEveryLight")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "plate solving, WCS, astrometry")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"When enabled, the plugin automatically plate solves each light frame (optionally for snapshots) at runtime. 
If no telescope/target coordinates or focal length are provided the blind solver is used. When plate solving is successful, the astrometric solution is written to the image header of FITS or XISF files.
This is particular useful for applications such as variable star or other photometric observations and their processing, each frame to have an astrometric solution already stored. 
NOTE: Using the plugin may slightly increases the time to save a frame, as the image is plate solved before being written to disk. Therefore only one solving attempt is made.")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]