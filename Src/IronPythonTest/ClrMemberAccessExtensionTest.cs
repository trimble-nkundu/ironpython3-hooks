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
    public class ClrMemberAccessExtensionTarget {
        public int Value = 5;
    }

    public static class ClrMemberAccessExtensionMethods {
        public static int ExtGetValue(this ClrMemberAccessExtensionTarget target) {
            return target.Value;
        }
    }

    public class ClrMemberAccessExtensionTest {
        [Test]
        public void ExtensionMethod_GetAndCall_NotifyListener() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessExtensionTarget, ClrMemberAccessExtensionMethods
clr.ImportExtensions(ClrMemberAccessExtensionMethods)
target = ClrMemberAccessExtensionTarget()
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
m = target.ExtGetValue
_ = target.ExtGetValue()
", scope);

            var extName = nameof(ClrMemberAccessExtensionMethods.ExtGetValue);
            Assert.AreEqual(2, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == extName));
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Call && e.Member.Name == extName));
            Assert.AreEqual(3, events.Count);
        }

        [Test]
        public void ExtensionMethod_GetOnly_NotifyOnce() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(@"
import clr
clr.AddReference('IronPythonTest')
from IronPythonTest.ClrMemberAccess import ClrMemberAccessExtensionTarget, ClrMemberAccessExtensionMethods
clr.ImportExtensions(ClrMemberAccessExtensionMethods)
target = ClrMemberAccessExtensionTarget()
", scope);

            var events = new List<ClrMemberAccessEventArgs>();
            engine.SetClrMemberAccessListener(e => events.Add(e));

            engine.Execute(@"
_ = target.ExtGetValue
", scope);

            var extName = nameof(ClrMemberAccessExtensionMethods.ExtGetValue);
            Assert.AreEqual(1, events.Count(e => e.AccessKind == ClrMemberAccessKind.Get && e.Member.Name == extName));
            Assert.AreEqual(0, events.Count(e => e.AccessKind == ClrMemberAccessKind.Call));
        }
    }
}
