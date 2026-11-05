// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static partial class PythonOps {
        /// <summary>
        /// Notifies a registered host listener before CLR member access. No-ops when no listener is registered.
        /// </summary>
        /// <remarks>
        /// Exceptions thrown by the host callback propagate to the caller (same as <c>sys.settrace</c> handlers).
        /// </remarks>
        public static void NotifyClrMemberAccess(CodeContext context, ClrMemberAccessEventArgs e) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (e == null) {
                throw new ArgumentNullException(nameof(e));
            }

            var pyContext = context.LanguageContext;
            if (!pyContext.HasClrMemberAccessListener) {
                return;
            }

            pyContext.NotifyClrMemberAccess(e);
        }

        /// <summary>
        /// Notifies a registered host listener before CLR member access. No-ops when no listener is registered.
        /// </summary>
        public static void NotifyClrMemberAccess(
            CodeContext context,
            ClrMemberAccessKind accessKind,
            MemberInfo member,
            object? instance = null,
            string? memberName = null) {

            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }

            var pyContext = context.LanguageContext;
            if (!pyContext.HasClrMemberAccessListener) {
                return;
            }

            pyContext.NotifyClrMemberAccess(
                CreateClrMemberAccessEventArgs(accessKind, member, instance, memberName));
        }

        /// <summary>
        /// Notifies a registered host listener before CLR member access when the member is known from binding metadata.
        /// No-ops when no listener is registered or the tracker cannot be mapped to a CLR member.
        /// </summary>
        public static void NotifyClrMemberAccess(
            CodeContext context,
            ClrMemberAccessKind accessKind,
            MemberTracker tracker,
            object? instance = null,
            string? memberName = null) {

            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (tracker == null) {
                throw new ArgumentNullException(nameof(tracker));
            }

            var pyContext = context.LanguageContext;
            if (!pyContext.HasClrMemberAccessListener) {
                return;
            }

            if (!TryCreateClrMemberAccessEventArgs(accessKind, tracker, instance, memberName, out ClrMemberAccessEventArgs? e)) {
                return;
            }

            pyContext.NotifyClrMemberAccess(e);
        }

        /// <summary>
        /// Notifies a registered host listener before CLR member access via a reflected type slot.
        /// No-ops when no listener is registered or the slot is not a reflected CLR member.
        /// </summary>
        public static void NotifyClrMemberAccess(
            CodeContext context,
            ClrMemberAccessKind accessKind,
            PythonTypeSlot slot,
            object? instance = null,
            string? memberName = null) {

            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            }

            MemberInfo? member = null;
            if (slot is ReflectedField rf) {
                member = rf.Info;
            } else if (slot is ReflectedProperty rp) {
                member = rp.Info;
            } else if (slot is ReflectedEvent re) {
                member = re.Info;
            }

            if (member == null) {
                if (slot is BuiltinMethodDescriptor bmd && bmd.Template.Targets.Count > 0) {
                    member = bmd.Template.Targets[0];
                }
            }

            if (member == null) {
                return;
            }

            NotifyClrMemberAccess(context, accessKind, member, instance, memberName);
        }

        internal static void TryNotifyClrMemberAccessFromSlot(
            CodeContext context,
            ClrMemberAccessKind accessKind,
            PythonTypeSlot slot,
            object instance) {

            if (context == null || slot == null) {
                return;
            }

            if (!context.LanguageContext.HasClrMemberAccessListener) {
                return;
            }

            NotifyClrMemberAccess(context, accessKind, slot, instance);
        }

        /// <summary>
        /// Notifies before a CLR method is invoked via <see cref="BuiltinFunction"/> runtime call paths
        /// (not already covered by dynamic bind-time notify). Skips special-name accessors used by properties/events.
        /// </summary>
        internal static void TryNotifyClrMemberAccessForBuiltinFunctionCall(
            CodeContext context,
            BuiltinFunction function,
            object? instance) {

            if (context == null || function == null) {
                return;
            }

            if (!context.LanguageContext.HasClrMemberAccessListener) {
                return;
            }

            if (function.Targets.Count != 1) {
                return;
            }

            if (function.Targets[0] is not MethodInfo method) {
                return;
            }

            if (method.IsSpecialName) {
                return;
            }

            if (!ShouldNotifyClrDeclaringType(function.DeclaringType)) {
                return;
            }

            object? notifyInstance = instance;
            if (!function.IsUnbound) {
                notifyInstance ??= function.BindingSelf;
            } else if (notifyInstance == function) {
                notifyInstance = null;
            }

            NotifyClrMemberAccess(context, ClrMemberAccessKind.Call, method, notifyInstance, function.Name);
        }

        private static bool ShouldNotifyClrDeclaringType(Type? declaringType) {
            if (declaringType == null || declaringType == typeof(DynamicNull)) {
                return false;
            }

            if (typeof(TypeTracker).IsAssignableFrom(declaringType)) {
                return false;
            }

            return !PythonBinder.IsPythonType(declaringType) && !PythonBinder.IsPythonSupportingType(declaringType);
        }

        internal static ClrMemberAccessEventArgs CreateClrMemberAccessEventArgs(
            ClrMemberAccessKind accessKind,
            MemberInfo member,
            object? instance = null,
            string? memberName = null) {

            Type declaringType = member.DeclaringType
                ?? throw new ArgumentException("Member has no declaring type.", nameof(member));

            return new ClrMemberAccessEventArgs(
                accessKind,
                declaringType,
                member,
                GetClrMemberKind(member),
                instance,
                memberName);
        }

        internal static bool TryCreateClrMemberAccessEventArgs(
            ClrMemberAccessKind accessKind,
            MemberTracker tracker,
            object? instance,
            string? memberName,
            out ClrMemberAccessEventArgs? args) {

            args = null;
            if (!TryGetMemberInfo(tracker, out MemberInfo? member, out Type? declaringType)) {
                return false;
            }

            declaringType ??= member.DeclaringType;
            if (declaringType == null) {
                return false;
            }

            args = new ClrMemberAccessEventArgs(
                accessKind,
                declaringType,
                member,
                GetClrMemberKind(member),
                instance,
                memberName ?? member.Name);
            return true;
        }

        internal static bool TryGetMemberInfoFromTracker(MemberTracker tracker, out MemberInfo member) {
            Type? declaringType;
            return TryGetMemberInfo(tracker, out member, out declaringType);
        }

        internal static bool TryGetMemberInfo(MemberTracker tracker, out MemberInfo member, out Type? declaringType) {
            member = null!;
            declaringType = tracker.DeclaringType;

            switch (tracker) {
                case FieldTracker ft:
                    member = ft.Field;
                    return true;
                case MethodTracker mt:
                    member = mt.Method;
                    return true;
                case MethodGroup mg:
                    if (mg.Methods.Count == 1) {
                        member = mg.Methods[0].Method;
                        return true;
                    }

                    return false;
                case EventTracker et:
                    member = et.Event;
                    return true;
                case PropertyTracker pt:
                    if (pt is ReflectedPropertyTracker rpt) {
                        member = rpt.Property;
                        return true;
                    }

                    PropertyInfo? prop = pt.DeclaringType?.GetProperty(
                        pt.Name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (prop != null) {
                        member = prop;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static ClrMemberKind GetClrMemberKind(MemberInfo member) {
            switch (member.MemberType) {
                case MemberTypes.Method:
                    return ClrMemberKind.Method;
                case MemberTypes.Property:
                    return ClrMemberKind.Property;
                case MemberTypes.Field:
                    return ClrMemberKind.Field;
                case MemberTypes.Event:
                    return ClrMemberKind.Event;
                default:
                    throw new ArgumentException($"Unsupported member type: {member.MemberType}", nameof(member));
            }
        }
    }
}
