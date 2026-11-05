// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Reflection;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;

using NUnit.Framework;

namespace IronPythonTest.ClrMemberAccess {
    public class ClrMemberAccessRegistrationTest {
        private class SampleTarget {
            public int ValueField;
            public int ValueProperty { get; set; }
            public void M() { }
        }

        [Test]
        public void RegisterListener_DispatchInvokes_UnregisterStops() {
            var engine = Python.CreateEngine();
            var context = (PythonContext)HostingHelpers.GetLanguageContext(engine);

            ClrMemberAccessEventArgs seen = null;
            context.SetClrMemberAccessListener(e => seen = e);

            Assert.IsTrue(context.HasClrMemberAccessListener);

            var target = new SampleTarget() { ValueField = 1 };
            var field = typeof(SampleTarget).GetField(nameof(SampleTarget.ValueField));
            var args = new ClrMemberAccessEventArgs(
                ClrMemberAccessKind.Get,
                typeof(SampleTarget),
                field,
                ClrMemberKind.Field,
                target);

            context.NotifyClrMemberAccess(args);

            Assert.IsNotNull(seen);
            Assert.AreEqual(ClrMemberAccessKind.Get, seen.AccessKind);
            Assert.AreEqual(ClrMemberKind.Field, seen.MemberKind);
            Assert.AreSame(field, seen.Member);
            Assert.AreSame(target, seen.Instance);

            context.SetClrMemberAccessListener(null);

            Assert.IsFalse(context.HasClrMemberAccessListener);

            seen = null;
            context.NotifyClrMemberAccess(args);
            Assert.IsNull(seen);
        }

        [Test]
        public void HostingExtension_RegisterListener_DispatchInvokes() {
            var engine = Python.CreateEngine();

            ClrMemberAccessEventArgs seen = null;
            engine.SetClrMemberAccessListener(e => seen = e);

            var context = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.M));
            context.NotifyClrMemberAccess(new ClrMemberAccessEventArgs(
                ClrMemberAccessKind.Call,
                typeof(SampleTarget),
                method,
                ClrMemberKind.Method,
                new SampleTarget()));

            Assert.IsNotNull(seen);
            Assert.AreEqual(ClrMemberAccessKind.Call, seen.AccessKind);
            Assert.AreEqual(ClrMemberKind.Method, seen.MemberKind);
        }
    }
}
