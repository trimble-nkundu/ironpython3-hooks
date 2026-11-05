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
    /// <summary>
    /// CLR type that would use TieredJitPropertyGet / FastMethodGet when no listener is registered.
    /// </summary>
    public class ClrMemberAccessFastPathProbe {
        public int ValueField = 1;
        public int ValueProperty { get; set; }
        public int M() {
            return 0;
        }
    }

    /// <summary>
    /// With a listener, Task 7 Option A forces slow bind; these tests assert exactly one notify per access.
    /// </summary>
    public class ClrMemberAccessFastPathPolicyTest {
        [Test]
        public void ListenerOn_PropertyGet_MethodGet_Set_EachNotifyOnce() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessFastPathProbe
probe = ClrMemberAccessFastPathProbe()
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
v = probe.ValueProperty
m = probe.M
probe.ValueProperty = 2
", scope);

            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessFastPathProbe.ValueProperty)));
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessFastPathProbe.M)));
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Set && e.Member.Name == nameof(ClrMemberAccessFastPathProbe.ValueProperty)));
            Assert.AreEqual(3, events.Count);
        }

        [Test]
        public void ListenerOn_SinglePropertyGet_NotDoubleNotified() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessFastPathProbe
probe = ClrMemberAccessFastPathProbe()
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
_ = probe.ValueProperty
", scope);

            var propertyGets = events.Where(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessFastPathProbe.ValueProperty)).ToList();
            Assert.AreEqual(1, propertyGets.Count);
        }
    }
}
