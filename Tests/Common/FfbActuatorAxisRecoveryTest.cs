// @under-test: Engine/Input/States/SourceState.cs
// @area: force-feedback   @layer: unit
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX.DirectInput;
using x360ce.Engine;

namespace x360ce.Tests
{
	[TestClass]
	public class FfbActuatorAxisRecoveryTest
	{
		static DeviceObjectItem XActuator()
		{
			return new DeviceObjectItem(0, ObjectGuid.XAxis, ObjectAspect.Position,
				DeviceObjectTypeFlags.AbsoluteAxis | DeviceObjectTypeFlags.ForceFeedbackActuator,
				0, "X Axis") { DiIndex = -1 };
		}

		[TestMethod, TestCategory("force-feedback"), TestCategory("critical")]
		public void A_unique_absolute_x_actuator_with_no_axis_index_is_recovered()
		{
			var x = XActuator();
			var y = new DeviceObjectItem(4, ObjectGuid.YAxis, ObjectAspect.Position,
				DeviceObjectTypeFlags.AbsoluteAxis, 1, "Y Axis") { DiIndex = 1 };
			var axes = 2; // Y was mapped by DirectInput; X failed.
			var mask = 0;
			var count = 0;
			Assert.IsTrue(SourceState.RecoverSingleXAxisActuator(new[] { y, x }, ref axes, ref mask, ref count));
			Assert.AreEqual(0, x.DiIndex);
			Assert.AreEqual(3, axes);
			Assert.AreEqual(1, mask);
			Assert.AreEqual(1, count);
		}

		[TestMethod, TestCategory("force-feedback"), TestCategory("critical")]
		public void Recovery_does_not_guess_with_multiple_actuators()
		{
			var x = XActuator();
			var z = new DeviceObjectItem(8, ObjectGuid.ZAxis, ObjectAspect.Position,
				DeviceObjectTypeFlags.AbsoluteAxis | DeviceObjectTypeFlags.ForceFeedbackActuator,
				1, "Z Axis") { DiIndex = -1 };
			var axes = 0;
			var mask = 0;
			var count = 0;
			Assert.IsFalse(SourceState.RecoverSingleXAxisActuator(new[] { x, z }, ref axes, ref mask, ref count));
			Assert.AreEqual(-1, x.DiIndex);
			Assert.AreEqual(0, count);
		}

		[TestMethod, TestCategory("force-feedback"), TestCategory("critical")]
		public void Recovery_repairs_actuator_when_x_axis_is_already_present()
		{
			var x = XActuator();
			var axes = 1; // Ordinary X input was found, but FFB lookup missed its actuator.
			var mask = 0;
			var count = 0;
			Assert.IsTrue(SourceState.RecoverSingleXAxisActuator(new[] { x }, ref axes, ref mask, ref count));
			Assert.AreEqual(1, axes);
			Assert.AreEqual(1, mask);
			Assert.AreEqual(1, count);
			Assert.AreEqual(0, x.DiIndex);
		}

		[TestMethod, TestCategory("force-feedback"), TestCategory("critical")]
		public void Recovery_does_not_override_an_indexed_actuator_or_existing_ffb_mapping()
		{
			var x = XActuator();
			x.DiIndex = 0;
			var axes = 1;
			var mask = 0;
			var count = 0;
			Assert.IsFalse(SourceState.RecoverSingleXAxisActuator(new[] { x }, ref axes, ref mask, ref count));
			Assert.AreEqual(0, x.DiIndex);
			x.DiIndex = -1;
			axes = 0;
			mask = 1;
			count = 1;
			Assert.IsFalse(SourceState.RecoverSingleXAxisActuator(new[] { x }, ref axes, ref mask, ref count));
			Assert.AreEqual(1, count);
		}

		[TestMethod, TestCategory("force-feedback"), TestCategory("critical")]
		public void Recovery_rejects_nonstandard_x_offset()
		{
			var x = XActuator();
			x.Offset = 12;
			var axes = 0;
			var mask = 0;
			var count = 0;
			Assert.IsFalse(SourceState.RecoverSingleXAxisActuator(new[] { x }, ref axes, ref mask, ref count));
			Assert.AreEqual(-1, x.DiIndex);
		}
	}
}
