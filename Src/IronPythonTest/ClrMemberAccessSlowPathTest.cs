// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting.Hosting;

using NUnit.Framework;

namespace IronPythonTest.ClrMemberAccess {
    public class ClrMemberAccessProbe {
        public int Field = 1;
        public int Property { get; set; }
        public int M() {
            return 42;
        }
    }

    public class ClrMemberAccessSlowPathTest {
        [Test]
        public void SlowPath_GetSetAndCall_FireListenerWithMemberInfo() {
            var engine = Python.CreateEngine();
            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessProbe
probe = ClrMemberAccessProbe()
", scope);

            engine.Execute(@"
_ = probe.Field
probe.Property = 2
_ = probe.M()
", scope);

            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessProbe.Field)));
            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Set && e.Member.Name == nameof(ClrMemberAccessProbe.Property)));
            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == nameof(ClrMemberAccessProbe.M)));

            var fieldEvent = events.First(e => e.Member.Name == nameof(ClrMemberAccessProbe.Field));
            Assert.AreEqual(ClrMemberKind.Field, fieldEvent.MemberKind);
            Assert.AreSame(typeof(ClrMemberAccessProbe).GetField(nameof(ClrMemberAccessProbe.Field)), fieldEvent.Member);
        }

        [Test]
        public void SlowPath_NoListener_NoCallbacks() {
            var engine = Python.CreateEngine();
            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));
            engine.SetClrMemberAccessListener(null);

            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessProbe
probe = ClrMemberAccessProbe()
_ = probe.Field
", scope);

            Assert.AreEqual(0, events.Count);
        }
    }
}
