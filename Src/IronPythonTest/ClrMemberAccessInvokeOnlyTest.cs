// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting.Hosting;

using NUnit.Framework;

namespace IronPythonTest.ClrMemberAccess {
    public class ClrMemberAccessInvokeOnlyTest {
        [Test]
        public void InvokeOnly_BoundMethodReference_CallNotifiesWithoutGet() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessProbe
probe = ClrMemberAccessProbe()
m = probe.M
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
_ = m()
", scope);

            var methodName = nameof(ClrMemberAccessProbe.M);
            Assert.AreEqual(0, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get));
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == methodName));
        }

        [Test]
        public void DirectCall_InstanceMethod_GetThenCall() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessProbe
probe = ClrMemberAccessProbe()
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
_ = probe.M()
", scope);

            var methodName = nameof(ClrMemberAccessProbe.M);
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == methodName));
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == methodName));
        }
    }
}
