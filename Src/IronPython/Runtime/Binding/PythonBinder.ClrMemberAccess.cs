// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal partial class PythonBinder {
        private static readonly MethodInfo NotifyClrMemberAccessMemberInfo = typeof(PythonOps).GetMethod(
            nameof(PythonOps.NotifyClrMemberAccess),
            new[] { typeof(CodeContext), typeof(ClrMemberAccessKind), typeof(MemberInfo), typeof(object), typeof(string) })!;

        public new DynamicMetaObject GetMember(
            string name,
            DynamicMetaObject target,
            OverloadResolverFactory resolverFactory,
            bool isNoThrow,
            DynamicMetaObject errorSuggestion) {

            DynamicMetaObject res = base.GetMember(name, target, resolverFactory, isNoThrow, errorSuggestion);
            return WrapClrMemberAccessForGetSet(name, target, resolverFactory, res, ClrMemberAccessKind.Get);
        }

        public new DynamicMetaObject SetMember(
            string name,
            DynamicMetaObject target,
            DynamicMetaObject value,
            DynamicMetaObject errorSuggestion,
            OverloadResolverFactory resolverFactory) {

            DynamicMetaObject res = base.SetMember(name, target, value, errorSuggestion, resolverFactory);
            return WrapClrMemberAccessForGetSet(name, target, resolverFactory, res, ClrMemberAccessKind.Set);
        }

        public new DynamicMetaObject Call(
            CallSignature signature,
            DynamicMetaObject errorSuggestion,
            OverloadResolverFactory resolverFactory,
            DynamicMetaObject target,
            params DynamicMetaObject[] args) {

            DynamicMetaObject res = base.Call(signature, errorSuggestion, resolverFactory, target, args);
            if (!_context.HasClrMemberAccessListener || res is ErrorMetaObject) {
                return res;
            }

            if (!TryGetInvokeMethodBase(target, out MethodBase method)) {
                return res;
            }

            Expression codeContext = GetCodeContextExpression(resolverFactory);
            Expression instance = AstUtils.Convert(target.Expression, typeof(object));

            return PrependClrMemberAccessNotify(
                res,
                codeContext,
                ClrMemberAccessKind.Call,
                method,
                instance,
                method.Name);
        }

        public new DynamicMetaObject CallMethod(
            DefaultOverloadResolver resolver,
            IList<MethodBase> targets,
            BindingRestrictions restrictions,
            string name,
            NarrowingLevel minLevel,
            NarrowingLevel maxLevel,
            out BindingTarget target) {

            DynamicMetaObject res = base.CallMethod(
                resolver,
                targets,
                restrictions,
                name,
                minLevel,
                maxLevel,
                out target);

            if (!_context.HasClrMemberAccessListener || !target.Success) {
                return res;
            }

            if (res is ErrorMetaObject) {
                return res;
            }

            MethodBase method = target.Overload?.ReflectionInfo;
            if (method == null) {
                return res;
            }

            Expression codeContext = GetCodeContextExpression(resolver);
            Expression instance = GetCallInstanceExpression(resolver);
            string notifyName = name ?? target.Name ?? method.Name;

            return PrependClrMemberAccessNotify(
                res,
                codeContext,
                ClrMemberAccessKind.Call,
                method,
                instance,
                notifyName);
        }

        private DynamicMetaObject WrapClrMemberAccessForGetSet(
            string name,
            DynamicMetaObject target,
            OverloadResolverFactory resolverFactory,
            DynamicMetaObject res,
            ClrMemberAccessKind kind) {

            if (!_context.HasClrMemberAccessListener) {
                return res;
            }

            if (res is ErrorMetaObject) {
                return res;
            }

            Type limitType = target.GetLimitType();
            if (!ShouldNotifyClrInstance(limitType)) {
                return res;
            }

            MemberTracker tracker = ResolveMemberTracker(
                kind == ClrMemberAccessKind.Set ? MemberRequestKind.Set : MemberRequestKind.Get,
                limitType,
                name);

            if (tracker == null || !PythonOps.TryGetMemberInfoFromTracker(tracker, out MemberInfo member)) {
                return res;
            }

            Expression codeContext = GetCodeContextExpression(resolverFactory);
            Expression instance = AstUtils.Convert(target.Expression, typeof(object));

            return PrependClrMemberAccessNotify(res, codeContext, kind, member, instance, name);
        }

        private static bool ShouldNotifyClrInstance(Type limitType) {
            if (limitType == typeof(DynamicNull)) {
                return false;
            }

            if (IsPythonType(limitType) || IsPythonSupportingType(limitType)) {
                return false;
            }

            if (typeof(TypeTracker).IsAssignableFrom(limitType)) {
                return false;
            }

            return true;
        }

        private MemberTracker ResolveMemberTracker(MemberRequestKind kind, Type instanceType, string name) {
            MemberGroup members = GetMember(kind, instanceType, name);
            if (members.Count == 0 && instanceType.IsInterface) {
                members = GetMember(kind, typeof(object), name);
            }

            if (members.Count == 0) {
                return null;
            }

            if (members.Count == 1) {
                return members[0];
            }

            MemberTracker bestMember = members[0];
            Type bestMemberDeclaringType = members[0].DeclaringType;
            for (int i = 1; i < members.Count; i++) {
                MemberTracker mt = members[i];
                if (!IsTrackerApplicableForType(instanceType, mt)) {
                    continue;
                }

                if (mt.DeclaringType.IsSubclassOf(bestMemberDeclaringType) ||
                    !IsTrackerApplicableForType(instanceType, bestMember)) {
                    bestMember = mt;
                    bestMemberDeclaringType = mt.DeclaringType;
                }
            }

            return bestMember;
        }

        private static bool IsTrackerApplicableForType(Type type, MemberTracker mt) {
            return mt.DeclaringType == type || type.IsSubclassOf(mt.DeclaringType);
        }

        private Expression GetCodeContextExpression(OverloadResolverFactory factory) {
            if (factory is PythonOverloadResolverFactory pyFactory) {
                return pyFactory._codeContext;
            }

            return AstUtils.Constant(_context.SharedContext);
        }

        private Expression GetCodeContextExpression(DefaultOverloadResolver resolver) {
            if (resolver is PythonOverloadResolver pyResolver) {
                return pyResolver.ContextExpression;
            }

            return AstUtils.Constant(_context.SharedContext);
        }

        private static Expression GetCallInstanceExpression(DefaultOverloadResolver resolver) {
            if (resolver.CallType == CallTypes.ImplicitInstance && resolver.Arguments.Count > 0) {
                return AstUtils.Convert(resolver.Arguments[0].Expression, typeof(object));
            }

            return Ast.Constant(null, typeof(object));
        }

        private DynamicMetaObject PrependClrMemberAccessNotify(
            DynamicMetaObject result,
            Expression codeContext,
            ClrMemberAccessKind kind,
            MemberInfo member,
            Expression instance,
            string memberName) {

            Expression notify = Ast.Call(
                NotifyClrMemberAccessMemberInfo,
                AstUtils.Convert(codeContext, typeof(CodeContext)),
                Ast.Constant(kind),
                Ast.Constant(member),
                instance,
                Ast.Constant(memberName, typeof(string)));

            Expression body = Ast.Block(notify, result.Expression);

            return new DynamicMetaObject(body, result.Restrictions, result.Value);
        }

        private static bool TryGetInvokeMethodBase(DynamicMetaObject target, out MethodBase method) {
            method = null;
            if (!target.HasValue) {
                return false;
            }

            object value = target.Value;
            if (value is BuiltinFunction bf && bf.Targets.Count == 1) {
                method = bf.Targets[0];
                return true;
            }

            if (value is BuiltinMethodDescriptor bmd && bmd.Template.Targets.Count == 1) {
                method = bmd.Template.Targets[0];
                return true;
            }

            if (value is MethodGroup mg && mg.Methods.Count == 1) {
                method = mg.Methods[0].Method;
                return true;
            }

            if (value is BoundMemberTracker bmt) {
                switch (bmt.BoundTo.MemberType) {
                    case TrackerTypes.Method:
                        if (bmt.BoundTo is MethodTracker mt) {
                            method = mt.Method;
                            return true;
                        }

                        break;
                    case TrackerTypes.MethodGroup:
                        MethodGroup boundGroup = (MethodGroup)bmt.BoundTo;
                        if (boundGroup.Methods.Count == 1) {
                            method = boundGroup.Methods[0].Method;
                            return true;
                        }

                        break;
                }
            }

            return false;
        }
    }
}
