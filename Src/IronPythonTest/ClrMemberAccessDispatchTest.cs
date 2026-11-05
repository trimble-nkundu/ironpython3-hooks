// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Reflection;

using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;

using NUnit.Framework;

namespace IronPythonTest.ClrMemberAccess {
    public class ClrMemberAccessDispatchTest {
        private class SampleTarget {
            public int ValueField = 1;
            public int ValueProperty { get; set; }
#pragma warning disable 0067 // event used only for reflection in tests
            public event System.EventHandler SampleEvent;
#pragma warning restore 0067
            public void M() { }
        }

        [Test]
        public void NotifyClrMemberAccess_NoListener_DoesNotInvokeCallback() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            pyContext.SetClrMemberAccessListener(null);
            Assert.IsFalse(pyContext.HasClrMemberAccessListener);

            var invoked = false;
            pyContext.SetClrMemberAccessListener(_ => invoked = true);
            pyContext.SetClrMemberAccessListener(null);

            var field = typeof(SampleTarget).GetField(nameof(SampleTarget.ValueField));
            PythonOps.NotifyClrMemberAccess(
                codeContext,
                ClrMemberAccessKind.Get,
                field,
                new SampleTarget());

            Assert.IsFalse(invoked);
        }

        [Test]
        public void NotifyClrMemberAccess_MemberInfo_InvokesListener() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            ClrMemberAccessEventArgs seen = null;
            pyContext.SetClrMemberAccessListener(e => seen = e);

            var target = new SampleTarget();
            var property = typeof(SampleTarget).GetProperty(nameof(SampleTarget.ValueProperty));

            PythonOps.NotifyClrMemberAccess(
                codeContext,
                ClrMemberAccessKind.Set,
                property,
                target);

            Assert.IsNotNull(seen);
            Assert.AreEqual(ClrMemberAccessKind.Set, seen.AccessKind);
            Assert.AreEqual(ClrMemberKind.Property, seen.MemberKind);
            Assert.AreSame(property, seen.Member);
            Assert.AreSame(target, seen.Instance);
        }

        [Test]
        public void NotifyClrMemberAccess_MemberTracker_InvokesListener() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            ClrMemberAccessEventArgs seen = null;
            pyContext.SetClrMemberAccessListener(e => seen = e);

            var tracker = MemberTracker.FromMemberInfo(typeof(SampleTarget).GetMethod(nameof(SampleTarget.M)));

            PythonOps.NotifyClrMemberAccess(
                codeContext,
                ClrMemberAccessKind.Call,
                tracker,
                new SampleTarget());

            Assert.IsNotNull(seen);
            Assert.AreEqual(ClrMemberAccessKind.Call, seen.AccessKind);
            Assert.AreEqual(ClrMemberKind.Method, seen.MemberKind);
        }

        [Test]
        public void NotifyClrMemberAccess_ReflectedFieldSlot_InvokesListener() {
            var engine = Python.CreateEngine();
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var codeContext = pyContext.SharedContext;

            ClrMemberAccessEventArgs seen = null;
            pyContext.SetClrMemberAccessListener(e => seen = e);

            var fieldInfo = typeof(SampleTarget).GetField(nameof(SampleTarget.ValueField));
            var slot = new ReflectedField(fieldInfo);

            PythonOps.NotifyClrMemberAccess(
                codeContext,
                ClrMemberAccessKind.Get,
                slot,
                new SampleTarget());

            Assert.IsNotNull(seen);
            Assert.AreEqual(ClrMemberKind.Field, seen.MemberKind);
            Assert.AreSame(fieldInfo, seen.Member);
        }

        [Test]
        public void CreateClrMemberAccessEventArgs_MapsEventMemberKind() {
            var eventInfo = typeof(SampleTarget).GetEvent(nameof(SampleTarget.SampleEvent));
            var args = PythonOps.CreateClrMemberAccessEventArgs(
                ClrMemberAccessKind.Get,
                eventInfo,
                new SampleTarget());

            Assert.AreEqual(ClrMemberKind.Event, args.MemberKind);
            Assert.AreSame(eventInfo, args.Member);
        }
    }
}
