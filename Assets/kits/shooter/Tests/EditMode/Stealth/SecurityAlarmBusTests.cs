using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class SecurityAlarmBusTests
    {
        [SetUp]
        public void SetUp()
        {
            SecurityAlarmBus.ResetOnPlay();
        }

        [TearDown]
        public void TearDown()
        {
            SecurityAlarmBus.ResetOnPlay();
        }

        [Test]
        public void RaiseWithoutListenersDoesNothing()
        {
            Assert.DoesNotThrow(() => SecurityAlarmBus.Raise(Vector3.one, "cam-a"));
        }

        [Test]
        public void RaiseHandsThePlaceAndTheTrimmedCameraIdToEveryListener()
        {
            var heard = new List<(Vector3, string)>();
            SecurityAlarmBus.Raised += (at, id) => heard.Add((at, id));

            SecurityAlarmBus.Raise(new Vector3(1f, 2f, 3f), " cam-lobby ");
            SecurityAlarmBus.Raise(Vector3.zero, null);

            Assert.That(heard, Has.Count.EqualTo(2));
            Assert.That(heard[0].Item1, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(heard[0].Item2, Is.EqualTo("cam-lobby"));
            Assert.That(heard[1].Item2, Is.EqualTo(string.Empty), "an alarm without a camera still rings");
        }

        [Test]
        public void AThrowingListenerIsLoggedAndTheOthersStillHear()
        {
            var heard = 0;
            SecurityAlarmBus.Raised += (at, id) => throw new InvalidOperationException("broken alarm");
            SecurityAlarmBus.Raised += (at, id) => heard++;
            LogAssert.Expect(LogType.Exception, new Regex("broken alarm"));

            SecurityAlarmBus.Raise(Vector3.zero, "cam-a");

            Assert.That(heard, Is.EqualTo(1));
        }

        [Test]
        public void ResetOnPlayDropsTheListeners()
        {
            var heard = 0;
            SecurityAlarmBus.Raised += (at, id) => heard++;

            SecurityAlarmBus.ResetOnPlay();
            SecurityAlarmBus.Raise(Vector3.zero, "cam-a");

            Assert.That(heard, Is.Zero);
        }
    }
}
