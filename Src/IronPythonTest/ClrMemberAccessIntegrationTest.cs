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
    /// <summary>
    /// CLR surface for Task 10 v1 integration coverage (instance + static members).
    /// </summary>
    public class ClrMemberAccessIntegrationTarget {
        public int InstanceField = 10;
        public int InstanceProperty { get; set; }
        public int InstanceMethod() {
            return 1;
        }

        public static int StaticField = 20;
        public static int StaticProperty { get; set; }
        public static int StaticMethod() {
            return 2;
        }
    }

    /// <summary>
    /// End-to-end host + Python scenarios for v1 CLR member access callbacks.
    /// </summary>
    public class ClrMemberAccessIntegrationTest {
        [Test]
        public void V1_InstanceAndStaticMembers_GetSetAndCall() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessIntegrationTarget
t = ClrMemberAccessIntegrationTarget()
ClrMemberAccessIntegrationTarget.StaticProperty = 0
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
_ = t.InstanceField
t.InstanceProperty = 3
_ = t.InstanceMethod()
_ = t.StaticField
ClrMemberAccessIntegrationTarget.StaticProperty = 4
_ = t.StaticMethod()
", scope);

            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.InstanceField)));
            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Set && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.InstanceProperty)));
            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.InstanceMethod)));

            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.StaticField)));
            Assert.IsTrue(events.Any(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.StaticMethod)));
            // Static property set via type object is required by IronPython but is not a v1 notify surface (MetaPythonType).

            var instanceField = events.First(e => e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.InstanceField));
            Assert.AreEqual(ClrMemberKind.Field, instanceField.MemberKind);
            Assert.AreSame(typeof(ClrMemberAccessIntegrationTarget).GetField(nameof(ClrMemberAccessIntegrationTarget.InstanceField)), instanceField.Member);
            Assert.AreSame(scope.GetVariable("t"), instanceField.Instance);

            var staticCall = events.First(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == nameof(ClrMemberAccessIntegrationTarget.StaticMethod));
            Assert.AreEqual(ClrMemberKind.Method, staticCall.MemberKind);
            Assert.IsNull(staticCall.Instance);
        }

        [Test]
        public void V1_RegisterThenUnregister_NoEventsAfterUnregister() {
            var engine = Python.CreateEngine();
            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessIntegrationTarget
t = ClrMemberAccessIntegrationTarget()
_ = t.InstanceField
", scope);
            Assert.Greater(events.Count, 0);

            events.Clear();
            engine.SetClrMemberAccessListener(null);
            engine.Execute("_ = t.InstanceField", scope);
            Assert.AreEqual(0, events.Count);
        }
    }
}
