// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        public class DagTempAllocator
        {
            private readonly SyntheticBoundNodeFactory _factory;
            private readonly Dictionary<BoundDagTemp, BoundExpression> map = new Dictionary<BoundDagTemp, BoundExpression>();
            private readonly ArrayBuilder<LocalSymbol> temps = new ArrayBuilder<LocalSymbol>();

            public DagTempAllocator(SyntheticBoundNodeFactory factory)
            {
                this._factory = factory;
            }

            public BoundExpression GetTemp(BoundDagTemp dagTemp)
            {
                if (!map.TryGetValue(dagTemp, out var result))
                {
                    // PROTOTYPE(patterns2): Not sure what temp kind should be used for `is pattern`.
                    var temp = _factory.SynthesizedLocal(dagTemp.Type, syntax: _factory.Syntax, kind: SynthesizedLocalKind.SwitchCasePatternMatching);
                    map.Add(dagTemp, _factory.Local(temp));
                    temps.Add(temp);
                    result = _factory.Local(temp);
                }

                return result;
            }

            public ImmutableArray<LocalSymbol> AllTemps()
            {
                return temps.ToImmutableArray();
            }

            public void AssignTemp(BoundDagTemp dagTemp, BoundExpression value)
            {
                map.Add(dagTemp, value);
            }
        }

        public override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node)
        {
            var decisionBuilder = new DecisionDagBuilder(this._compilation);
            var loweredInput = VisitExpression(node.Expression);
            var inputTemp = decisionBuilder.LowerPattern(node.Expression, node.Pattern, out var decisions, out var bindings);
            var tempAllocator = new DagTempAllocator(this._factory);
            var conjunctBuilder = ArrayBuilder<BoundExpression>.GetInstance();
            var resultBuilder = ArrayBuilder<BoundExpression>.GetInstance();

            void addConjunct(BoundExpression expression)
            {
                // PROTOTYPE(patterns2): could handle constant expressions more efficiently.
                if (resultBuilder.Count != 0)
                {
                    expression = _factory.Sequence(ImmutableArray<LocalSymbol>.Empty, resultBuilder.ToImmutable(), expression);
                    resultBuilder.Clear();
                }

                conjunctBuilder.Add(expression);
            }

            try
            {
                // first, copy the input expression into the input temp
                if (node.Pattern.Kind != BoundKind.RecursivePattern &&
                    (loweredInput.Kind == BoundKind.Local || loweredInput.Kind == BoundKind.Parameter || loweredInput.ConstantValue != null))
                {
                    // Since non-recursive patterns cannot have side-effects on locals, we reuse an existing local
                    // if present. A recursive pattern, on the other hand, may mutate a local through a captured lambda
                    // when a `Deconstruct` method is invoked.
                    tempAllocator.AssignTemp(inputTemp, loweredInput);
                }
                else
                {
                    resultBuilder.Add(_factory.AssignmentExpression(tempAllocator.GetTemp(inputTemp), loweredInput));
                }

                // then process all of the individual decisions
                foreach (var decision in decisions)
                {
                    switch (decision)
                    {
                        case BoundNonNullDecision d:
                            // If the actual input is a constant, short-circuit this test
                            if (d.Input == inputTemp && loweredInput.ConstantValue != null)
                            {
                                var decisionResult = loweredInput.ConstantValue != ConstantValue.Null;
                                if (!decisionResult)
                                {
                                    // short-circuit the whole thing and return the constant result (e.g. `null is string s`)
                                    // No need to do the other tests or bindings.
                                    return _factory.Literal(decisionResult);
                                }
                            }
                            else
                            {
                                var input =tempAllocator.GetTemp(d.Input);
                                addConjunct(MakeNullCheck(d.Syntax, input, input.Type.IsNullableType() ? BinaryOperatorKind.NullableNullNotEqual : BinaryOperatorKind.NotEqual));
                            }
                            break;
                        case BoundTypeDecision d:
                            {
                                var input = tempAllocator.GetTemp(d.Input);
                                addConjunct(_factory.Is(input, d.Type));
                            }
                            break;
                        case BoundValueDecision d:
                            // If the actual input is a constant, short-circuit this test
                            if (d.Input == inputTemp && loweredInput.ConstantValue != null)
                            {
                                var decisionResult = loweredInput.ConstantValue == d.Value;
                                if (!decisionResult)
                                {
                                    // short-circuit the whole thing and return the constant result (e.g. `3 is 4`)
                                    return _factory.Literal(decisionResult);
                                }
                            }
                            else if (d.Value == ConstantValue.Null)
                            {
                                var input = tempAllocator.GetTemp(d.Input);
                                addConjunct(MakeNullCheck(d.Syntax, input, input.Type.IsNullableType() ? BinaryOperatorKind.NullableNullEqual : BinaryOperatorKind.Equal));
                            }
                            else
                            {
                                var input = tempAllocator.GetTemp(d.Input);
                                var systemObject = _factory.SpecialType(SpecialType.System_Object);
                                addConjunct(MakeEqual(_factory.Literal(d.Value, input.Type), input));
                            }
                            break;
                        case BoundDagEvaluation e:
                            {
                                // e.Symbol is used as follows:
                                // 1. if it is a Deconstruct method, in indicates an invocation of that deconstruct and creation of N new temps.
                                // 2. if it is a Type, it indicates a type cast to that type, and the creation of one new temp of that type.
                                // 3. if it is a Field or Property symbol, it indicates a fetch and the creation of one new temp.
                                // 4. We ignore ITuple-based deconstruction for now.
                                var input = tempAllocator.GetTemp(e.Input);
                                switch (e.Symbol)
                                {
                                    case FieldSymbol field:
                                        {
                                            var outputTemp = new BoundDagTemp(e.Syntax, field.Type, e, 0);
                                            var output = tempAllocator.GetTemp(outputTemp);
                                            resultBuilder.Add(_factory.AssignmentExpression(output, _factory.Field(input, field)));
                                        }
                                        break;
                                    case PropertySymbol property:
                                        {
                                            var outputTemp = new BoundDagTemp(e.Syntax, property.Type, e, 0);
                                            var output = tempAllocator.GetTemp(outputTemp);
                                            resultBuilder.Add(_factory.AssignmentExpression(output, _factory.Property(input, property)));
                                        }
                                        break;
                                    case MethodSymbol method:
                                        {
                                            var refKindBuilder = ArrayBuilder<RefKind>.GetInstance();
                                            var argBuilder = ArrayBuilder<BoundExpression>.GetInstance();
                                            BoundExpression receiver;
                                            void addArg(RefKind refKind, BoundExpression expression)
                                            {
                                                refKindBuilder.Add(refKind);
                                                argBuilder.Add(expression);
                                            }
                                            Debug.Assert(method.Name == "Deconstruct");
                                            int extensionExtra;
                                            if (method.IsStatic)
                                            {
                                                Debug.Assert(method.IsExtensionMethod);
                                                receiver = _factory.Type(method.ContainingType);
                                                addArg(RefKind.None, input);
                                                extensionExtra = 1;
                                            }
                                            else
                                            {
                                                receiver = input;
                                                extensionExtra = 0;
                                            }
                                            for (int i = extensionExtra; i < method.ParameterCount; i++)
                                            {
                                                var parameter = method.Parameters[i];
                                                Debug.Assert(parameter.RefKind == RefKind.Out);
                                                var outputTemp = new BoundDagTemp(e.Syntax, parameter.Type, e, i - extensionExtra);
                                                addArg(RefKind.Out, tempAllocator.GetTemp(outputTemp));
                                            }
                                            resultBuilder.Add(_factory.Call(receiver, method, refKindBuilder.ToImmutableAndFree(), argBuilder.ToImmutableAndFree()));
                                        }
                                        break;
                                    case TypeSymbol type:
                                        {
                                            var outputTemp = new BoundDagTemp(e.Syntax, type, e, 0);
                                            var output = tempAllocator.GetTemp(outputTemp);
                                            resultBuilder.Add(_factory.AssignmentExpression(output, _factory.As(input, type)));
                                        }
                                        break;
                                    default:
                                        throw ExceptionUtilities.UnexpectedValue(e.Symbol?.Kind);
                                }
                            }
                            break;
                    }
                }

                if (resultBuilder.Count != 0)
                {
                    conjunctBuilder.Add(_factory.Sequence(ImmutableArray<LocalSymbol>.Empty, resultBuilder.ToImmutable(), _factory.Literal(true)));
                    resultBuilder.Clear();
                }
                BoundExpression result = null;
                foreach (var conjunct in conjunctBuilder)
                {
                    result = (result == null) ? conjunct : _factory.LogicalAnd(result, conjunct);
                }

                var bindingsBuilder = ArrayBuilder<BoundExpression>.GetInstance();
                foreach (var (left, right) in bindings)
                {
                    bindingsBuilder.Add(_factory.AssignmentExpression(left, tempAllocator.GetTemp(right)));
                }

                if (bindingsBuilder.Count > 0)
                {
                    var c = _factory.Sequence(ImmutableArray<LocalSymbol>.Empty, bindingsBuilder.ToImmutableAndFree(), _factory.Literal(true));
                    result = (result == null) ? c : (BoundExpression)_factory.LogicalAnd(result, c);
                }
                else if (result == null)
                {
                    result = _factory.Literal(true);
                }

                return _factory.Sequence(tempAllocator.AllTemps(), ImmutableArray<BoundExpression>.Empty, result);
            }
            finally
            {
                conjunctBuilder.Free();
                resultBuilder.Free();
            }
        }

        private BoundExpression MakeEqual(BoundLiteral boundLiteral, BoundExpression input)
        {
            if (boundLiteral.Type.SpecialType == SpecialType.System_Double && Double.IsNaN(boundLiteral.ConstantValue.DoubleValue) ||
                boundLiteral.Type.SpecialType == SpecialType.System_Single && Single.IsNaN(boundLiteral.ConstantValue.SingleValue))
            {
                // NaN must be treated specially, as operator== doesn't treat it as equal to anything, even itself.
                Debug.Assert(boundLiteral.Type == input.Type);
                return _factory.InstanceCall(boundLiteral, "Equals", input);
            }

            var booleanType = _factory.SpecialType(SpecialType.System_Boolean);
            var intType = _factory.SpecialType(SpecialType.System_Int32);
            switch (boundLiteral.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.BoolEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_Byte:
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_SByte:
                case SpecialType.System_UInt16:
                    // PROTOTYPE(patterns2): need to check that this produces efficient code
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.IntEqual, _factory.Convert(intType, boundLiteral), _factory.Convert(intType, input), booleanType, method: null);
                case SpecialType.System_Decimal:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.DecimalEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_Double:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.DoubleEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_Int32:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.IntEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_Int64:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.LongEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_Single:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.FloatEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_String:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.StringEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_UInt32:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.UIntEqual, boundLiteral, input, booleanType, method: null);
                case SpecialType.System_UInt64:
                    return MakeBinaryOperator(_factory.Syntax, BinaryOperatorKind.ULongEqual, boundLiteral, input, booleanType, method: null);
                default:
                    // PROTOTYPE(patterns2): need more efficient code for enum test, e.g. `color is Color.Red`
                    // This is the (correct but inefficient) fallback for any type that isn't yet implemented (e.g. enums)
                    var systemObject = _factory.SpecialType(SpecialType.System_Object);
                    return _factory.StaticCall(
                        systemObject,
                        "Equals",
                        _factory.Convert(systemObject, boundLiteral),
                        _factory.Convert(systemObject, input)
                        );
            }
        }

        //public override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node)
        //{
        //    var loweredExpression = VisitExpression(node.Expression);
        //    var loweredPattern = LowerPattern(node.Pattern);
        //    return MakeIsPattern(loweredPattern, loweredExpression);
        //}

        //// Input must be used no more than once in the result. If it is needed repeatedly store its value in a temp and use the temp.
        //BoundExpression MakeIsPattern(BoundPattern loweredPattern, BoundExpression loweredInput)
        //{
        //    var syntax = _factory.Syntax = loweredPattern.Syntax;
        //    switch (loweredPattern.Kind)
        //    {
        //        case BoundKind.DeclarationPattern:
        //            {
        //                var declPattern = (BoundDeclarationPattern)loweredPattern;
        //                return MakeIsDeclarationPattern(declPattern, loweredInput);
        //            }

        //        case BoundKind.WildcardPattern:
        //            return _factory.Literal(true);

        //        case BoundKind.ConstantPattern:
        //            {
        //                var constantPattern = (BoundConstantPattern)loweredPattern;
        //                return MakeIsConstantPattern(constantPattern, loweredInput);
        //            }

        //        default:
        //            throw ExceptionUtilities.UnexpectedValue(loweredPattern.Kind);
        //    }
        //}

        BoundPattern LowerPattern(BoundPattern pattern)
        {
            switch (pattern.Kind)
            {
                case BoundKind.DeclarationPattern:
                    {
                        var declPattern = (BoundDeclarationPattern)pattern;
                        return declPattern.Update(declPattern.Variable, VisitExpression(declPattern.VariableAccess), declPattern.DeclaredType, declPattern.IsVar);
                    }
                case BoundKind.RecursivePattern:
                    {
                        throw ExceptionUtilities.UnexpectedValue(pattern.Kind);
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

        //private BoundExpression MakeIsConstantPattern(BoundConstantPattern loweredPattern, BoundExpression loweredInput)
        //{
        //    return CompareWithConstant(loweredInput, loweredPattern.Value);
        //}

        //private BoundExpression MakeIsDeclarationPattern(BoundDeclarationPattern loweredPattern, BoundExpression loweredInput)
        //{
        //    Debug.Assert(((object)loweredPattern.Variable == null && loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression) ||
        //                 loweredPattern.Variable.GetTypeOrReturnType() == loweredPattern.DeclaredType.Type);

        //    if (loweredPattern.IsVar)
        //    {
        //        var result = _factory.Literal(true);

        //        if (loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression)
        //        {
        //            return result;
        //        }

        //        Debug.Assert((object)loweredPattern.Variable != null && loweredInput.Type.Equals(loweredPattern.Variable.GetTypeOrReturnType(), TypeCompareKind.AllIgnoreOptions));

        //        var assignment = _factory.AssignmentExpression(loweredPattern.VariableAccess, loweredInput);
        //        return _factory.MakeSequence(assignment, result);
        //    }

        //    if (loweredPattern.VariableAccess.Kind == BoundKind.DiscardExpression)
        //    {
        //        LocalSymbol temp;
        //        BoundLocal discard = _factory.MakeTempForDiscard((BoundDiscardExpression)loweredPattern.VariableAccess, out temp);

        //        return _factory.Sequence(ImmutableArray.Create(temp),
        //                 sideEffects: ImmutableArray<BoundExpression>.Empty,
        //                 result: MakeIsDeclarationPattern(loweredPattern.Syntax, loweredInput, discard, requiresNullTest: loweredInput.Type.CanContainNull()));
        //    }

        //    return MakeIsDeclarationPattern(loweredPattern.Syntax, loweredInput, loweredPattern.VariableAccess, requiresNullTest: loweredInput.Type.CanContainNull());
        //}

        ///// <summary>
        ///// Is the test, produced as a result of a pattern-matching operation, always true?
        ///// Knowing that enables us to construct slightly more efficient code.
        ///// </summary>
        //private bool IsIrrefutablePatternTest(BoundExpression test)
        //{
        //    while (true)
        //    {
        //        switch (test.Kind)
        //        {
        //            case BoundKind.Literal:
        //                {
        //                    var value = ((BoundLiteral)test).ConstantValue;
        //                    return value.IsBoolean && value.BooleanValue;
        //                }
        //            case BoundKind.Sequence:
        //                test = ((BoundSequence)test).Value;
        //                continue;
        //            default:
        //                return false;
        //        }
        //    }
        //}

        //private BoundExpression CompareWithConstant(BoundExpression input, BoundExpression boundConstant)
        //{
        //    var systemObject = _factory.SpecialType(SpecialType.System_Object);
        //    if (boundConstant.ConstantValue == ConstantValue.Null)
        //    {
        //        if (input.Type.IsNonNullableValueType())
        //        {
        //            var systemBoolean = _factory.SpecialType(SpecialType.System_Boolean);
        //            return RewriteNullableNullEquality(
        //                syntax: _factory.Syntax,
        //                kind: BinaryOperatorKind.NullableNullEqual,
        //                loweredLeft: input,
        //                loweredRight: boundConstant,
        //                returnType: systemBoolean);
        //        }
        //        else
        //        {
        //            return _factory.ObjectEqual(_factory.Convert(systemObject, input), boundConstant);
        //        }
        //    }
        //    else if (input.Type.IsNullableType() && boundConstant.NullableNeverHasValue())
        //    {
        //        return _factory.Not(MakeNullableHasValue(_factory.Syntax, input));
        //    }
        //    else
        //    {
        //        return _factory.StaticCall(
        //            systemObject,
        //            "Equals",
        //            _factory.Convert(systemObject, boundConstant),
        //            _factory.Convert(systemObject, input)
        //            );
        //    }
        //}

        private bool? MatchConstantValue(BoundExpression source, TypeSymbol targetType, bool requiredNullTest)
        {
            // use site diagnostics will already have been reported during binding.
            HashSet<DiagnosticInfo> ignoredDiagnostics = null;
            var sourceType = source.Type.IsDynamic() ? _compilation.GetSpecialType(SpecialType.System_Object) : source.Type;
            var conversionKind = _compilation.Conversions.ClassifyConversionFromType(sourceType, targetType, ref ignoredDiagnostics).Kind;
            var constantResult = Binder.GetIsOperatorConstantResult(sourceType, targetType, conversionKind, source.ConstantValue, requiredNullTest);
            return
                constantResult == ConstantValue.True ? true :
                constantResult == ConstantValue.False ? false :
                constantResult == null ? (bool?)null :
                throw ExceptionUtilities.UnexpectedValue(constantResult);
        }

        BoundExpression MakeIsDeclarationPattern(SyntaxNode syntax, BoundExpression loweredInput, BoundExpression loweredTarget, bool requiresNullTest)
        {
            var type = loweredTarget.Type;
            requiresNullTest = requiresNullTest && loweredInput.Type.CanContainNull();

            // If the match is impossible, we simply evaluate the input and yield false.
            var matchConstantValue = MatchConstantValue(loweredInput, type, false);
            if (matchConstantValue == false)
            {
                return _factory.MakeSequence(loweredInput, _factory.Literal(false));
            }

            // It is possible that the input value is already of the correct type, in which case the pattern
            // is irrefutable, and we can just do the assignment and return true (or perform the null test).
            if (matchConstantValue == true)
            {
                requiresNullTest = requiresNullTest && MatchConstantValue(loweredInput, type, true) != true;
                if (loweredInput.Type.IsNullableType())
                {
                    var getValueOrDefault = _factory.SpecialMethod(SpecialMember.System_Nullable_T_GetValueOrDefault).AsMember((NamedTypeSymbol)loweredInput.Type);
                    if (requiresNullTest)
                    {
                        //bool Is<T>(T? input, out T output) where T : struct
                        //{
                        //    output = input.GetValueOrDefault();
                        //    return input.HasValue;
                        //}

                        var input = _factory.SynthesizedLocal(loweredInput.Type, syntax); // we copy the input to avoid double evaluation
                        var getHasValue = _factory.SpecialMethod(SpecialMember.System_Nullable_T_get_HasValue).AsMember((NamedTypeSymbol)loweredInput.Type);
                        return _factory.MakeSequence(input,
                            _factory.AssignmentExpression(_factory.Local(input), loweredInput),
                            _factory.AssignmentExpression(loweredTarget, _factory.Convert(type, _factory.Call(_factory.Local(input), getValueOrDefault))),
                            _factory.Call(_factory.Local(input), getHasValue)
                            );
                    }
                    else
                    {
                        var convertedInput = _factory.Convert(type, _factory.Call(loweredInput, getValueOrDefault));
                        var assignment = _factory.AssignmentExpression(loweredTarget, convertedInput);
                        return _factory.MakeSequence(assignment, _factory.Literal(true));
                    }
                }
                else
                {
                    var convertedInput = _factory.Convert(type, loweredInput);
                    var assignment = _factory.AssignmentExpression(loweredTarget, convertedInput);
                    return requiresNullTest
                        ? _factory.ObjectNotEqual(assignment, _factory.Null(type))
                        : _factory.MakeSequence(assignment, _factory.Literal(true));
                }
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
            else // type parameter or value type
            {
                // bool Is<T>(this object i, out T o)
                // {
                //     // inefficient because it performs the type test twice, and also because it boxes the input.
                //     bool s;
                //     o = (s = i is T) ? (T)i : default(T);
                //     return s;
                // }

                // Because a cast involving a type parameter is not necessarily a valid conversion (or, if it is, it might not
                // be of a kind appropriate for pattern-matching), we use `object` as an intermediate type for the input expression.
                var objectType = _factory.SpecialType(SpecialType.System_Object);
                var s = _factory.SynthesizedLocal(_factory.SpecialType(SpecialType.System_Boolean), syntax);
                var i = _factory.SynthesizedLocal(objectType, syntax); // we copy the input to avoid double evaluation
                return _factory.Sequence(
                    ImmutableArray.Create(s, i),
                    ImmutableArray.Create<BoundExpression>(
                        _factory.AssignmentExpression(_factory.Local(i), _factory.Convert(objectType, loweredInput)),
                        _factory.AssignmentExpression(loweredTarget, _factory.Conditional(
                            _factory.AssignmentExpression(_factory.Local(s), _factory.Is(_factory.Local(i), type)),
                            _factory.Convert(type, _factory.Local(i)),
                            _factory.Default(type), type))
                        ),
                    _factory.Local(s)
                    );
            }
        }
    }
}
