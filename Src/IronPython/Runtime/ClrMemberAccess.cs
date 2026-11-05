// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace IronPython.Runtime {
    /// <summary>
    /// Kind of CLR member access reported to a host listener.
    /// </summary>
    public enum ClrMemberAccessKind {
        Get,
        Set,
        Call,
    }

    /// <summary>
    /// Kind of CLR member being accessed.
    /// </summary>
    public enum ClrMemberKind {
        Method,
        Property,
        Field,
        Event,
    }

    /// <summary>
    /// Argument payload for <see cref="ClrMemberAccessEventHandler"/>.
    /// </summary>
    public sealed class ClrMemberAccessEventArgs {
        public ClrMemberAccessEventArgs(
            ClrMemberAccessKind accessKind,
            Type declaringType,
            MemberInfo member,
            ClrMemberKind memberKind,
            object instance = null,
            string memberName = null) {

            if (declaringType == null) {
                throw new ArgumentNullException(nameof(declaringType));
            }

            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }

            AccessKind = accessKind;
            DeclaringType = declaringType;
            Member = member;
            MemberKind = memberKind;
            Instance = instance;
            MemberName = memberName ?? member.Name;
        }

        /// <summary>Whether the access is a get, set, or call.</summary>
        public ClrMemberAccessKind AccessKind { get; }

        /// <summary>Declaring type of the member (see design notes for declaring vs runtime type).</summary>
        public Type DeclaringType { get; }

        /// <summary>The CLR member being accessed.</summary>
        public MemberInfo Member { get; }

        /// <summary>Classification of <see cref="Member"/>.</summary>
        public ClrMemberKind MemberKind { get; }

        /// <summary>Target instance, or <c>null</c> for static members.</summary>
        public object Instance { get; }

        /// <summary>Member name; may differ from <see cref="MemberInfo.Name"/> when overload resolution is ambiguous on call.</summary>
        public string MemberName { get; }
    }

    /// <summary>
    /// Host callback invoked when IronPython accesses a CLR field, property, method, or event.
    /// Register via <see cref="PythonContext.SetClrMemberAccessListener"/> (see repo design doc under <c>.cursor/plans/clr-member-access-callback-design.md</c>).
    /// </summary>
    public delegate void ClrMemberAccessEventHandler(ClrMemberAccessEventArgs e);
}
