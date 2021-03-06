//
// Unit tests for SystemSound
//
// Authors:
//	Marek Safar (marek.safar@gmail.com)
//
// Copyright 2012 Xamarin Inc. All rights reserved.
//

#if !__WATCHOS__

using System;
using System.IO;
#if XAMCORE_2_0
using Foundation;
using MediaPlayer;
using AudioToolbox;
using CoreFoundation;
#else
using MonoTouch.Foundation;
using MonoTouch.MediaPlayer;
using MonoTouch.AudioToolbox;
using MonoTouch.CoreFoundation;
#endif
using NUnit.Framework;
using System.Threading;

namespace MonoTouchFixtures.AudioToolbox {
	
	[TestFixture]
	[Preserve (AllMembers = true)]
	public class SystemSoundTest
	{
		[Test]
		public void FromFile ()
		{
			var path = Path.GetFullPath (Path.Combine ("AudioToolbox", "1.caf"));

			using (var ss = SystemSound.FromFile (NSUrl.FromFilename (path))) {
				Assert.AreEqual (AudioServicesError.None, ss.AddSystemSoundCompletion (delegate {
					}));

				ss.PlaySystemSound ();
			}
		}

		[Test]
		public void Properties ()
		{
			var path = Path.GetFullPath (Path.Combine ("AudioToolbox", "1.caf"));

			using (var ss = SystemSound.FromFile (NSUrl.FromFilename (path))) {
				Assert.That (ss.IsUISound, Is.True, "#1");
				Assert.That (ss.CompletePlaybackIfAppDies, Is.False, "#2");
			}
		}

		[Test]
		public void TestCallbackPlaySystem ()
		{
			if (!TestRuntime.CheckSystemAndSDKVersion (9, 0))
				Assert.Inconclusive ("requires iOS9");

			string path = Path.Combine (NSBundle.MainBundle.ResourcePath, "drum01.mp3");

			using (var ss = SystemSound.FromFile (NSUrl.FromFilename (path))) {

				var completed = false;
				const int timeout = 10;

				completed = false;
				Assert.IsTrue (MonoTouchFixtures.AppDelegate.RunAsync (DateTime.Now.AddSeconds (timeout), async () =>
					ss.PlaySystemSound (() => {	completed = true; }
				), () => completed), "TestCallbackPlaySystem");
			}
		}

		[Test]
		public void TestCallbackPlayAlert ()
		{
			if (!TestRuntime.CheckSystemAndSDKVersion (9, 0))
				Assert.Inconclusive ("requires iOS9");

			string path = Path.Combine (NSBundle.MainBundle.ResourcePath, "drum01.mp3");

			using (var ss = SystemSound.FromFile (NSUrl.FromFilename (path))) {

				var completed = false;
				const int timeout = 10;

				completed = false;
				Assert.IsTrue (MonoTouchFixtures.AppDelegate.RunAsync (DateTime.Now.AddSeconds (timeout), async () =>
					ss.PlayAlertSound (() => { completed = true; }
				), () => completed), "TestCallbackPlayAlert");
			}
		}

		[Test]
		public void DisposeTest ()
		{
			var path = Path.GetFullPath (Path.Combine ("AudioToolbox", "1.caf"));

			var ss = SystemSound.FromFile (NSUrl.FromFilename (path));
			Assert.That (ss.Handle, Is.Not.EqualTo (IntPtr.Zero), "DisposeTest");

			ss.Dispose ();
			// Handle prop checks NotDisposed and throws if it is
			Assert.Throws<ObjectDisposedException> (() => ss.Handle.ToString (), "DisposeTest");
		}
	}
}

#endif // !__WATCHOS__
