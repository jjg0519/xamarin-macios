//
// Unit tests for SKPhysicsBody
//
// Authors:
//	Sebastien Pouliot <sebastien@xamarin.com>
//
// Copyright 2013 Xamarin Inc. All rights reserved.
//

#if !__WATCHOS__

using System;
using System.Drawing;
#if XAMCORE_2_0
using Foundation;
using UIKit;
using SpriteKit;
using ObjCRuntime;
#else
using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
using MonoTouch.SpriteKit;
using MonoTouch.UIKit;
#endif
using NUnit.Framework;

#if XAMCORE_2_0
using RectangleF=CoreGraphics.CGRect;
using SizeF=CoreGraphics.CGSize;
using PointF=CoreGraphics.CGPoint;
#else
using nfloat=global::System.Single;
using nint=global::System.Int32;
using nuint=global::System.UInt32;
#endif

namespace MonoTouchFixtures.SpriteKit {

	[TestFixture]
	[Preserve (AllMembers = true)]
	public class PhysicsBodyTest {

		[Test]
		public void BodyWithEdgeLoopFromRect ()
		{
			if (!TestRuntime.CheckSystemAndSDKVersion (7, 0))
				Assert.Ignore ("Requires iOS7");

			// bug 13772 - that call actually return a PKPhysicsBody (private PhysicKit framework)
			SizeF size = new SizeF (3, 2);
			using (var body = SKPhysicsBody.CreateRectangularBody (size)) {
				Assert.That (body, Is.TypeOf<SKPhysicsBody> (), "SKPhysicsBody");
			}
		}

#if false
		// reminder that the default ctor is bad (uncomment to try again in future iOS release)
		// ref: https://bugzilla.xamarin.com/show_bug.cgi?id=14502

		[Test]
		public void DefaultCtor ()
		{
			using (var Scene = new SKScene(UIScreen.MainScreen.Bounds.Size))
			using (var b = new SKSpriteNode (UIColor.Red, new SizeF (10, 10))) {
				b.PhysicsBody = new SKPhysicsBody ();
				Scene.AddChild (b); //BOOM
			}
		}
#endif
	}
}

#endif // !__WATCHOS__
