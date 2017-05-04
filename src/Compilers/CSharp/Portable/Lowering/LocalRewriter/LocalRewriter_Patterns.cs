﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        public override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node)
        {
            var loweredExpression = VisitExpression(node.Expression);
            var loweredPattern = LowerPattern(node.Pattern);
            return MakeIsPattern(loweredPattern, loweredExpression);
        }

        // Input must be used no more than once in the result. If it is needed repeatedly store its value in a temp and use the temp.
        BoundExpression MakeIsPattern(BoundPattern loweredPattern, BoundExpression loweredInput)
        {
            var syntax = _factory.Syntax = loweredPattern.Syntax;
            switch (loweredPattern.Kind)
            {
                case BoundKind.DeclarationPattern:
                    {
                        var declPattern = (BoundDeclarationPattern)loweredPattern;
                        return MakeIsDeclarationPattern(declPattern, loweredInput);
                    }

                case BoundKind.WildcardPattern:
                    return _factory.Literal(true);

                case BoundKind.ConstantPattern:
                    {
                        var constantPattern = (BoundConstantPattern)loweredPattern;
                        return MakeIsConstantPattern(constantPattern, loweredInput);
                    }

                default:
                    throw ExceptionUtilities.UnexpectedValue(loweredPattern.Kind);
            }
        }

        BoundPattern LowerPattern(BoundPattern pattern)
        {
            switch (pattern.Kind)
            {
                case BoundKind.DeclarationPattern:
                    {
                        var declPattern = (BoundDeclarationPattern)pattern;
                        return declPattern.Update(declPattern.Variable, VisitExpression(declPattern.VariableAccess), declPattern.DeclaredType, declPattern.IsVar);
                    }
                case BoundKind.ConstantPattern:
                    {
                        var constantPattern = (BoundConstantPattern)pattern;
                        return constantPattern.Update(VisitExpression(constantPattern.Value), constantPattern.ConstantValue);
                    }
                default:
                    return pattern;
            }
        }

        private BoundExpression MakeIsConstantPattern(BoundConstantPattern loweredPattern, BoundExpression loweredInput)
        {
            return CompareWithConstant(loweredInput, loweredPattern.Value);
        }

        private BoundExpression MakeIsDeclarationPattern(BoundDeclarationPattern loweredPattern, BoundExpression loweredInput)
        {
            Debug.Assert(((object)loweredPattern.Variable == null && loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression) ||
                         loweredPattern.Variable.GetTypeOrReturnType() == loweredPattern.DeclaredType.Type);

            if (loweredPattern.IsVar)
            {
                var result = _factory.Literal(true);

                if (loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression)
                {
                    return result;
                }

                Debug.Assert((object)loweredPattern.Variable != null && loweredInput.Type.Equals(loweredPattern.Variable.GetTypeOrReturnType(), TypeCompareKind.IgnoreDynamicAndTupleNames));

                var assignment = _factory.AssignmentExpression(loweredPattern.VariableAccess, loweredInput);
                return _factory.MakeSequence(assignment, result);
            }

            if (loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression)
            {
                LocalSymbol temp;
                BoundLocal discard = _factory.MakeTempForDiscard((BoundDiscardExpression)loweredPattern.VariableAccess, out temp);

                return _factory.Sequence(ImmutableArray.Create(temp),
                         sideEffects: ImmutableArray<BoundExpression>.Empty,
                         result: MakeIsDeclarationPattern(loweredPattern.Syntax, loweredInput, discard, requiresNullTest: loweredInput.Type.CanContainNull()));
            }

            return MakeIsDeclarationPattern(loweredPattern.Syntax, loweredInput, loweredPattern.VariableAccess, requiresNullTest: loweredInput.Type.CanContainNull());
        }

        /// <summary>
        /// Produce a 'logical and' operation that is clearly irrefutable (<see cref="IsIrrefutablePatternTest(BoundExpression)"/>) when it can be.
        /// </summary>
        BoundExpression LogicalAndForPatterns(BoundExpression left, BoundExpression right)
        {
            return IsIrrefutablePatternTest(left) ? _factory.MakeSequence(left, right) : _factory.LogicalAnd(left, right);
        }

        /// <summary>
        /// Is the test, produced as a result of a pattern-matching operation, always true?
        /// Knowing that enables us to construct slightly more efficient code.
        /// </summary>
        private bool IsIrrefutablePatternTest(BoundExpression test)
        {
            while (true)
            {
                switch (test.Kind)
                {
                    case BoundKind.Literal:
                        {
                            var value = ((BoundLiteral)test).ConstantValue;
                            return value.IsBoolean && value.BooleanValue;
                        }
                    case BoundKind.Sequence:
                        test = ((BoundSequence)test).Value;
                        continue;
                    default:
                        return false;
                }
            }
        }

        private BoundExpression CompareWithConstant(BoundExpression input, BoundExpression boundConstant)
        {
            return _factory.StaticCall(
                _factory.SpecialType(SpecialType.System_Object),
                "Equals",
                _factory.Convert(_factory.SpecialType(SpecialType.System_Object), boundConstant),
                _factory.Convert(_factory.SpecialType(SpecialType.System_Object), input)
                );
        }

        private bool MatchIsIrrefutable(TypeSymbol sourceType, TypeSymbol targetType, bool requiredNullTest)
        {
            // use site diagnostics will already have been reported during binding.
            HashSet<DiagnosticInfo> ignoredDiagnostics = null;
            switch (_compilation.Conversions.ClassifyBuiltInConversion(sourceType, targetType, ref ignoredDiagnostics).Kind)
            {
                case ConversionKind.Boxing:
                case ConversionKind.ImplicitReference:
                case ConversionKind.Identity:
                    return true;
                default:
                    return false;
            }
        }

        BoundExpression MakeIsDeclarationPattern(SyntaxNode syntax, BoundExpression loweredInput, BoundExpression loweredTarget, bool requiresNullTest)
        {
            var type = loweredTarget.Type;
            requiresNullTest = requiresNullTest && !loweredInput.Type.CanContainNull();

            // It is possible that the input value is already of the correct type, in which case the pattern
            // is irrefutable, and we can just do the assignment and return true (or perform the null test).
            if (MatchIsIrrefutable(loweredInput.Type, loweredTarget.Type, requiresNullTest))
            {
                var convertedInput = _factory.Convert(loweredTarget.Type, loweredInput);
                var assignment = _factory.AssignmentExpression(loweredTarget, convertedInput);
                return requiresNullTest
                    ? _factory.ObjectNotEqual(assignment, _factory.Null(type))
                    : _factory.MakeSequence(assignment, _factory.Literal(true));
            }

            // a pattern match of the form "expression is Type identifier" is equivalent to
            // an invocation of one of these helpers:
            if (type.IsReferenceType)
            {
                // bool Is<T>(object e, out T t) where T : class // reference type
                // {
                //     t = e as T;
                //     return t != null;
                // }
                return _factory.ObjectNotEqual(
                    _factory.AssignmentExpression(loweredTarget, _factory.As(loweredInput, type)),
                    _factory.Null(type));
            }
            else if (type.IsValueType)
            {
                // The type here is not a Nullable<T> instance type, as that would have led to the semantic error:
                // ERR_PatternNullableType: It is not legal to use nullable type '{0}' in a pattern; use the underlying type '{1}' instead.
                Debug.Assert(!type.IsNullableType());

                // It may be possible to improve this code by only assigning t when returning
                // true (avoid returning a new default value)
                // bool Is<T>(object e, out T t) where T : struct // non-Nullable value type
                // {
                //     T? tmp = e as T?;
                //     t = tmp.GetValueOrDefault();
                //     return tmp.HasValue;
                // }
                var tmpType = _factory.SpecialType(SpecialType.System_Nullable_T).Construct(type);
                var tmp = _factory.SynthesizedLocal(tmpType, syntax);
                var asg1 = _factory.AssignmentExpression(_factory.Local(tmp), tmpType == loweredInput.Type ? loweredInput : _factory.As(loweredInput, tmpType));
                var value = _factory.Call(
                    _factory.Local(tmp),
                    UnsafeGetNullableMethod(syntax, tmpType, SpecialMember.System_Nullable_T_GetValueOrDefault));
                var asg2 = _factory.AssignmentExpression(loweredTarget, value);
                var result = MakeNullableHasValue(syntax, _factory.Local(tmp));
                return _factory.MakeSequence(tmp, asg1, asg2, result);
            }
            else // type parameter
            {
                Debug.Assert(type.IsTypeParameter());
                // bool Is<T>(this object i, out T o)
                // {
                //     // inefficient because it performs the type test twice, and also because it boxes the input.
                //     bool s = i is T;
                //     o = s ? (T)i : default(T);
                //     return s;
                // }

                // Because a cast involving a type parameter is not necessarily a valid conversion (or, if it is, it might not
                // be of a kind appropriate for pattern-matching), we use `object` as an intermediate type for the input expression.
                var tmpType = _factory.SpecialType(SpecialType.System_Object);
                var s = _factory.SynthesizedLocal(_factory.SpecialType(SpecialType.System_Boolean), syntax);
                var i = _factory.SynthesizedLocal(tmpType, syntax); // we copy the input to avoid double evaluation
                return _factory.Sequence(
                    ImmutableArray.Create(s, i),
                    ImmutableArray.Create<BoundExpression>(
                        _factory.AssignmentExpression(_factory.Local(i), _factory.Convert(tmpType, loweredInput)),
                        _factory.AssignmentExpression(_factory.Local(s), _factory.Is(_factory.Local(i), type)),
                        _factory.AssignmentExpression(loweredTarget, _factory.Conditional(_factory.Local(s), _factory.Convert(type, _factory.Local(i)), _factory.Default(type), type))
                        ),
                    _factory.Local(s)
                    );
            }
        }
    }
}
