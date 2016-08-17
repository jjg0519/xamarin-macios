﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Xamarin.MacDev.Tasks
{
	public abstract class ACToolTaskBase : XcodeCompilerToolTask
	{
		ITaskItem partialAppManifest;
		string outputSpecs;
		PDictionary plist;

		#region Inputs

		[Required]
		public ITaskItem[] AssetCatalogs { get; set; }

		public string DeviceModel { get; set; }

		public string DeviceOSVersion { get; set; }

		public bool IsWatchApp { get; set; }

		[Required]
		public bool OptimizePNGs { get; set; }

		[Required]
		public string OutputPath { get; set; }

		#endregion

		#region Outputs

		[Output]
		public ITaskItem PartialAppManifest { get; set; }

		#endregion

		protected override string DefaultBinDir {
			get { return DeveloperRootBinDir; }
		}

		protected override string ToolName {
			get { return "actool"; }
		}

		static bool IsWatchExtension (PDictionary plist)
		{
			PDictionary extension;
			PString id;

			if (!plist.TryGetValue ("NSExtension", out extension))
				return false;

			if (!extension.TryGetValue ("NSExtensionPointIdentifier", out id))
				return false;

			return id.Value == "com.apple.watchkit";
		}

		protected override void AppendCommandLineArguments (IDictionary<string, string> environment, ProcessArgumentBuilder args, ITaskItem[] items)
		{
			string minimumDeploymentTarget;

			if (plist != null) {
				PString value;

				if (!plist.TryGetValue (MinimumDeploymentTargetKey, out value) || string.IsNullOrEmpty (value.Value))
					minimumDeploymentTarget = SdkVersion;
				else
					minimumDeploymentTarget = value.Value;

				var assetDirs = new HashSet<string> (items.Select (x => x.ItemSpec));

				if (plist.TryGetValue (ManifestKeys.XSAppIconAssets, out value) && !string.IsNullOrEmpty (value.Value)) {
					int index = value.Value.IndexOf (".xcassets" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
					string assetDir = null;
					var rpath = value.Value;

					if (index != -1)
						assetDir = rpath.Substring (0, index + ".xcassets".Length);

					if (assetDirs != null && assetDirs.Contains (assetDir)) {
						var assetName = Path.GetFileNameWithoutExtension (rpath);

						if (PartialAppManifest == null) {
							args.Add ("--output-partial-info-plist");
							args.AddQuoted (partialAppManifest.GetMetadata ("FullPath"));

							PartialAppManifest = partialAppManifest;
						}

						args.Add ("--app-icon");
						args.AddQuoted (assetName);
					}
				}

				if (plist.TryGetValue (ManifestKeys.XSLaunchImageAssets, out value) && !string.IsNullOrEmpty (value.Value)) {
					int index = value.Value.IndexOf (".xcassets" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
					string assetDir = null;
					var rpath = value.Value;

					if (index != -1)
						assetDir = rpath.Substring (0, index + ".xcassets".Length);

					if (assetDirs != null && assetDirs.Contains (assetDir)) {
						var assetName = Path.GetFileNameWithoutExtension (rpath);

						if (PartialAppManifest == null) {
							args.Add ("--output-partial-info-plist");
							args.AddQuoted (partialAppManifest.GetMetadata ("FullPath"));

							PartialAppManifest = partialAppManifest;
						}

						args.Add ("--launch-image");
						args.AddQuoted (assetName);
					}
				}

				if (plist.TryGetValue (ManifestKeys.CLKComplicationGroup, out value) && !string.IsNullOrEmpty (value.Value))
					args.Add ("--complication", value);
			} else {
				minimumDeploymentTarget = SdkVersion;
			}

			if (OptimizePNGs)
				args.Add ("--compress-pngs");

			if (AppleSdkSettings.XcodeVersion.Major >= 7) {
				if (!string.IsNullOrEmpty (outputSpecs))
					args.Add ("--enable-on-demand-resources", "YES");

				if (!string.IsNullOrEmpty (DeviceModel))
					args.Add ("--filter-for-device-model", DeviceModel);

				if (!string.IsNullOrEmpty (DeviceOSVersion))
					args.Add ("--filter-for-device-os-version", DeviceOSVersion);

				if (!string.IsNullOrEmpty (outputSpecs)) {
					args.Add ("--asset-pack-output-specifications");
					args.AddQuoted (Path.GetFullPath (outputSpecs));
				}
			}

			if (plist != null) {
				foreach (var targetDevice in GetTargetDevices (plist))
					args.Add ("--target-device", targetDevice);
			}

			args.Add ("--minimum-deployment-target", minimumDeploymentTarget);

			switch (SdkPlatform) {
			case "iPhoneSimulator":
				args.Add ("--platform", IsWatchApp ? "watchsimulator" : "iphonesimulator");
				break;
			case "iPhoneOS":
				args.Add ("--platform", IsWatchApp ? "watchos" : "iphoneos");
				break;
			case "MacOSX":
				args.Add ("--platform", "macosx");
				break;
			case "WatchSimulator":
				args.Add ("--platform", "watchsimulator");
				break;
			case "WatchOS":
				args.Add ("--platform", "watchos");
				break;
			case "AppleTVSimulator":
				args.Add ("--platform", "appletvsimulator");
				break;
			case "AppleTVOS":
				args.Add ("--platform", "appletvos");
				break;
			}
		}

		IEnumerable<ITaskItem> GetCompiledBundleResources (PDictionary output, string intermediateBundleDir)
		{
			var pwd = PathUtils.ResolveSymbolicLink (Environment.CurrentDirectory);
			PDictionary dict;
			PArray array;

			if (output.TryGetValue (string.Format ("com.apple.{0}.compilation-results", ToolName), out dict) && dict.TryGetValue ("output-files", out array)) {
				foreach (var path in array.OfType<PString> ().Select (x => x.Value)) {
					// don't include the generated plist files as BundleResources
					if (path.EndsWith ("partial-info.plist", StringComparison.Ordinal))
						continue;

					var vpath = PathUtils.AbsoluteToRelative (pwd, PathUtils.ResolveSymbolicLink (path));
					var item = new TaskItem (vpath);

					// Note: the intermediate bundle dir functions as a top-level bundle dir
					var logicalName = PathUtils.AbsoluteToRelative (intermediateBundleDir, path);

					if (logicalName.StartsWith ("../OnDemandResources/", StringComparison.Ordinal)) {
						logicalName = logicalName.Substring (3);

						var outputPath = Path.Combine (OutputPath, logicalName);

						item.SetMetadata ("OutputPath", outputPath);
					}

					item.SetMetadata ("LogicalName", logicalName);
					item.SetMetadata ("Optimize", "false");

					yield return item;
				}
			}

			yield break;
		}

		public override bool Execute ()
		{
			var intermediate = Path.Combine (IntermediateOutputPath, ToolName);
			var intermediateBundleDir = Path.Combine (intermediate, "bundle");
			var manifest = new TaskItem (Path.Combine (intermediate, "asset-manifest.plist"));
			var bundleResources = new List<ITaskItem> ();
			var outputManifests = new List<ITaskItem> ();
			var knownSpecs = new HashSet<string> ();
			string bundleIdentifier = null;
			var specs = new PArray ();
			int rc;

			Log.LogTaskName ("ACTool");
			Log.LogTaskProperty ("AppManifest", AppManifest);
			Log.LogTaskProperty ("AssetCatalogs", AssetCatalogs);
			Log.LogTaskProperty ("DeviceModel", DeviceModel);
			Log.LogTaskProperty ("DeviceOSVersion", DeviceOSVersion);
			Log.LogTaskProperty ("IntermediateOutputPath", IntermediateOutputPath);
			Log.LogTaskProperty ("IsWatchApp", IsWatchApp);
			Log.LogTaskProperty ("OptimizePNGs", OptimizePNGs);
			Log.LogTaskProperty ("OutputPath", OutputPath);
			Log.LogTaskProperty ("ProjectDir", ProjectDir);
			Log.LogTaskProperty ("ResourcePrefix", ResourcePrefix);
			Log.LogTaskProperty ("SdkBinPath", SdkBinPath);
			Log.LogTaskProperty ("SdkPlatform", SdkPlatform);
			Log.LogTaskProperty ("SdkVersion", SdkVersion);

			if (AssetCatalogs == null || AssetCatalogs.Length == 0)
				return true;

			switch (SdkPlatform) {
			case "iPhoneSimulator":
			case "iPhoneOS":
			case "MacOSX":
			case "WatchSimulator":
			case "WatchOS":
			case "AppleTVSimulator":
			case "AppleTVOS":
				break;
			default:
				Log.LogError ("Unrecognized platform: {0}", SdkPlatform);
				return false;
			}

			if (AppManifest != null) {
				try {
					plist = PDictionary.FromFile (AppManifest.ItemSpec);
				} catch (Exception ex) {
					Log.LogError (null, null, null, AppManifest.ItemSpec, 0, 0, 0, 0, "{0}", ex.Message);
					return false;
				}

				bundleIdentifier = plist.GetCFBundleIdentifier ();
			}

			if (AppleSdkSettings.XcodeVersion.Major >= 7 && !string.IsNullOrEmpty (bundleIdentifier) && SdkPlatform != "WatchSimulator") {
				foreach (var catalog in AssetCatalogs) {
					foreach (var contentsJson in Directory.EnumerateFiles (catalog.ItemSpec, "Contents.json", SearchOption.AllDirectories)) {
						var text = File.ReadAllText (contentsJson);

						if (string.IsNullOrEmpty (text))
							continue;

						var json = JsonConvert.DeserializeObject (text) as JObject;

						if (json == null)
							continue;

						var properties = json.Property ("properties");

						if (properties == null)
							continue;

						var resourceTags = properties.Value.ToObject<JObject> ().Property ("on-demand-resource-tags");

						if (resourceTags == null || resourceTags.Value.Type != JTokenType.Array)
							continue;

						var tagArray = resourceTags.Value.ToObject<JArray> ();
						var tags = new HashSet<string> ();
						string hash;

						foreach (var tag in tagArray.Select (token => token.ToObject<string> ()))
							tags.Add (tag);

						var tagList = tags.ToList ();
						tagList.Sort ();

						var path = AssetPackUtils.GetAssetPackDirectory (intermediate, bundleIdentifier, tagList, out hash);

						if (knownSpecs.Add (hash)) {
							var assetpack = new PDictionary ();
							var ptags = new PArray ();

							Directory.CreateDirectory (path);

							for (int i = 0; i < tags.Count; i++)
								ptags.Add (new PString (tagList[i]));

							assetpack.Add ("bundle-id", new PString (string.Format ("{0}.asset-pack-{1}", bundleIdentifier, hash)));
							assetpack.Add ("bundle-path", new PString (Path.GetFullPath (path)));
							assetpack.Add ("tags", ptags);
							specs.Add (assetpack);
						}
					}
				}
			}

			partialAppManifest = new TaskItem (Path.Combine (intermediate, "partial-info.plist"));

			if (specs.Count > 0) {
				outputSpecs = Path.Combine (intermediate, "output-specifications.plist");
				specs.Save (outputSpecs, true);
			}

			var output = new TaskItem (intermediateBundleDir);

			Directory.CreateDirectory (intermediateBundleDir);

			// Note: Compile() will set the PartialAppManifest property if it is used...
			if ((rc = Compile (AssetCatalogs, output, manifest)) != 0) {
				if (File.Exists (manifest.ItemSpec)) {
					try {
						var log = PDictionary.FromFile (manifest.ItemSpec);

						LogWarningsAndErrors (log, AssetCatalogs[0]);
					} catch (FormatException) {
						Log.LogError ("actool exited with code {0}", rc);
					}

					File.Delete (manifest.ItemSpec);
				}

				return false;
			}

			if (PartialAppManifest != null && !File.Exists (PartialAppManifest.GetMetadata ("FullPath")))
				Log.LogError ("Partial Info.plist file was not generated: {0}", PartialAppManifest.GetMetadata ("FullPath"));

			try {
				var manifestOutput = PDictionary.FromFile (manifest.ItemSpec);

				LogWarningsAndErrors (manifestOutput, AssetCatalogs[0]);

				bundleResources.AddRange (GetCompiledBundleResources (manifestOutput, intermediateBundleDir));
				outputManifests.Add (manifest);
			} catch (Exception ex) {
				Log.LogError ("Failed to load output manifest for {0} for the file {2}: {1}", ToolName, ex.Message, manifest.ItemSpec);
			}

			foreach (var assetpack in specs.OfType<PDictionary> ()) {
				var path = Path.Combine (assetpack.GetString ("bundle-path").Value, "Info.plist");
				var bundlePath = PathUtils.AbsoluteToRelative (intermediate, path);
				var outputPath = Path.Combine (OutputPath, bundlePath);
				var rpath = Path.Combine (intermediate, bundlePath);
				var dict = new PDictionary ();

				dict.SetCFBundleIdentifier (assetpack.GetString ("bundle-id").Value);
				dict.Add ("Tags", assetpack.GetArray ("tags").Clone ());

				dict.Save (path, true, true);

				var item = new TaskItem (rpath);
				item.SetMetadata ("LogicalName", bundlePath);
				item.SetMetadata ("OutputPath", outputPath);
				item.SetMetadata ("Optimize", "false");

				bundleResources.Add (item);
			}

			BundleResources = bundleResources.ToArray ();
			OutputManifests = outputManifests.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
