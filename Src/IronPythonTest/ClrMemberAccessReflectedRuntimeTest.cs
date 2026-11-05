// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;

using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;

using NUnit.Framework;

namespace IronPythonTest {
    public class ClrMemberAccessReflectedRuntimeTest {
        public class RuntimeProbe {
            public int ValueField = 3;
            public int ValueProperty { get; set; }
        }

        [Test]
        public void ReflectedField_GetValue_NotifiesWithoutSlotTryWrapper() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            var events = new List<ClrMemberAccessEventArgs>();
            pyContext.SetClrMemberAccessListener(e => events.Add(e));

            var target = new RuntimeProbe();
            var fieldInfo = typeof(RuntimeProbe).GetField(nameof(RuntimeProbe.ValueField));
            var slot = new ReflectedField(fieldInfo);

            Assert.AreEqual(3, slot.GetValue(codeContext, target));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ClrMemberAccessKind.Get, events[0].AccessKind);
            Assert.AreSame(fieldInfo, events[0].Member);
        }
    }
}
