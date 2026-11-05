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
    public class ClrMemberAccessSlotPathTest {
        public class SlotProbe {
            public int ValueField = 7;
            public int ValueProperty { get; set; }
        }

        [Test]
        public void SlotTryGetValue_ReflectedField_NotifiesListener() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            var events = new List<ClrMemberAccessEventArgs>();
            pyContext.SetClrMemberAccessListener(e => events.Add(e));

            var target = new SlotProbe();
            var owner = DynamicHelpers.GetPythonTypeFromType(typeof(SlotProbe));
            var fieldInfo = typeof(SlotProbe).GetField(nameof(SlotProbe.ValueField));
            var slot = new ReflectedField(fieldInfo);

            Assert.IsTrue(PythonOps.SlotTryGetValue(codeContext, slot, target, owner, out object value));
            Assert.AreEqual(7, value);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ClrMemberAccessKind.Get, events[0].AccessKind);
            Assert.AreSame(fieldInfo, events[0].Member);
            Assert.AreSame(target, events[0].Instance);
        }

        [Test]
        public void SlotTrySetValue_ReflectedField_NotifiesListener() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            var events = new List<ClrMemberAccessEventArgs>();
            pyContext.SetClrMemberAccessListener(e => events.Add(e));

            var target = new SlotProbe();
            var owner = DynamicHelpers.GetPythonTypeFromType(typeof(SlotProbe));
            var fieldInfo = typeof(SlotProbe).GetField(nameof(SlotProbe.ValueField));
            var slot = new ReflectedField(fieldInfo);

            Assert.IsTrue(PythonOps.SlotTrySetValue(codeContext, slot, target, owner, 99));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ClrMemberAccessKind.Set, events[0].AccessKind);
            Assert.AreSame(fieldInfo, events[0].Member);
        }

        [Test]
        public void SlotTryGetValue_NoListener_DoesNotNotify() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;
            pyContext.SetClrMemberAccessListener(null);

            var invoked = false;
            pyContext.SetClrMemberAccessListener(_ => invoked = true);
            pyContext.SetClrMemberAccessListener(null);

            var slot = new ReflectedField(typeof(SlotProbe).GetField(nameof(SlotProbe.ValueField)));
            PythonOps.SlotTryGetValue(
                codeContext,
                slot,
                new SlotProbe(),
                DynamicHelpers.GetPythonTypeFromType(typeof(SlotProbe)),
                out _);

            Assert.IsFalse(invoked);
        }
    }
}
