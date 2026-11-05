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

namespace IronPythonTest {
    public class ClrMemberAccessTrackerBase {
        public int ShadowField = 1;
    }

    public class ClrMemberAccessTrackerDerived : ClrMemberAccessTrackerBase {
        public new int ShadowField = 2;
    }

    public class ClrMemberAccessTrackerBindingTest {
        [Test]
        public void ReResolve_GetOnDerivedInstance_PrefersDerivedMember() {
            var engine = Python.CreateEngine();
            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest import ClrMemberAccessTrackerDerived
obj = ClrMemberAccessTrackerDerived()
_ = obj.ShadowField
", scope);

            var derivedField = typeof(ClrMemberAccessTrackerDerived).GetField(nameof(ClrMemberAccessTrackerDerived.ShadowField));
            var baseField = typeof(ClrMemberAccessTrackerBase).GetField(nameof(ClrMemberAccessTrackerBase.ShadowField));

            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == nameof(ClrMemberAccessTrackerBase.ShadowField)));
            var getEvent = events.Single(e => e.AccessKind == ClrMemberAccessKind.Get);
            Assert.AreSame(derivedField, getEvent.Member);
            Assert.AreNotSame(baseField, getEvent.Member);
        }
    }
}
