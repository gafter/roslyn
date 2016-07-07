// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class LocalRewriter
    {
        // PROTOTYPE(typeswitch): as a temporary hack while this code is in development, we
        // only use the new translation machinery when this bool is set to true. If it is false
        // then we use the transitional code which translates a pattern switch into a series of
        // if-then-else statements. Ultimately we need the new translation to be used to generate
        // switch IL instructions for ordinary old-style switch statements.
        private static bool UseNewTranslation(BoundPatternSwitchStatement node) => true;

        public override BoundNode VisitPatternSwitchStatement(BoundPatternSwitchStatement node)
        {
            // Until this is all implemented, we use a dumb series of if-then-else
            // statements to translate the switch statement.
            _factory.Syntax = node.Syntax;
            if (!UseNewTranslation(node)) return VisitPatternSwitchStatement_Ifchain(node);

            var usedTemps = new HashSet<LocalSymbol>();  // PROTOTYPE(typeswitch): worry about deterministic ordering
            var result = ArrayBuilder<BoundStatement>.GetInstance();

            // PROTOTYPE(typeswitch): do we need to do anything with node.ConstantTargetOpt, given that we
            // have the decision tree? If not, can we remove it from the bound trees?
            var expression = VisitExpression(node.Expression);

            // output the decision tree part
            LowerDecisionTree(expression, node.DecisionTree, usedTemps, result);

            // if the endpoint is reachable, we exit the switch
            if (!node.DecisionTree.MatchIsComplete) result.Add(_factory.Goto(node.BreakLabel));

            // output the sections of code (that were reachable)
            foreach (var section in node.SwitchSections)
            {
                ArrayBuilder<BoundStatement> sectionBuilder = ArrayBuilder<BoundStatement>.GetInstance();
                foreach (var label in section.SwitchLabels)
                {
                    sectionBuilder.Add(_factory.Label(label.Label));
                }

                sectionBuilder.AddRange(VisitList(section.Statements));
                sectionBuilder.Add(_factory.Goto(node.BreakLabel));
                result.Add(_factory.Block(section.Locals, sectionBuilder.ToImmutableAndFree()));
            }

            result.Add(_factory.Label(node.BreakLabel));
            var translatedSwitch = _factory.Block(usedTemps.ToImmutableArray().Concat(node.InnerLocals), node.InnerLocalFunctions, result.ToImmutableAndFree());
            return translatedSwitch;
        }

        private void LowerDecisionTree(BoundExpression expression, DecisionTree decisionTree, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            if (decisionTree == null) return;

            // If the input expression was a constant or a simple read of a local, then that is the
            // decision tree's expression. Otherwise it is a newly created temp, to which we must
            // assign the switch expression.
            if (decisionTree.Temp != null)
            {
                // Store the input expression into a temp
                if (decisionTree.Expression != expression)
                {
                    result.Add(_factory.Assignment(decisionTree.Expression, expression));
                }

                usedTemps.Add(decisionTree.Temp);
            }

            switch (decisionTree.Kind)
            {
                case DecisionTree.DecisionKind.ByType:
                    {
                        LowerDecisionTree((DecisionTree.ByType)decisionTree, usedTemps, result);
                        return;
                    }
                case DecisionTree.DecisionKind.ByValue:
                    {
                        LowerDecisionTree((DecisionTree.ByValue)decisionTree, usedTemps, result);
                        return;
                    }
                case DecisionTree.DecisionKind.Guarded:
                    {
                        LowerDecisionTree((DecisionTree.Guarded)decisionTree, usedTemps, result);
                        return;
                    }
                default:
                    throw ExceptionUtilities.UnexpectedValue(decisionTree.Kind);
            }
        }

        private void LowerDecisionTree(DecisionTree.ByType byType, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            var inputConstant = byType.Expression.ConstantValue;
            if (inputConstant != null)
            {
                if (inputConstant.IsNull)
                {
                    // input is the constant null
                    LowerDecisionTree(byType.Expression, byType.WhenNull, usedTemps, result);
                    if (byType.WhenNull?.MatchIsComplete != true)
                    {
                        LowerDecisionTree(byType.Expression, byType.Default, usedTemps, result);
                    }
                }
                else
                {
                    // input is a non-null constant
                    foreach (var kvp in byType.TypeAndDecision)
                    {
                        LowerDecisionTree(byType.Expression, kvp.Value, usedTemps, result);
                        if (kvp.Value.MatchIsComplete) return;
                    }
                    LowerDecisionTree(byType.Expression, byType.Default, usedTemps, result);
                }
            }
            else
            {
                var defaultLabel = _factory.GenerateLabel("byTypeDefault");

                // input is not a constant
                if (byType.Type.CanBeAssignedNull())
                {
                    // first test for null
                    var notNullLabel = _factory.GenerateLabel("notNull");
                    var inputExpression = byType.Expression;
                    var nullValue = _factory.Null(byType.Type);
                    BoundExpression notNull = byType.Type.IsNullableType()
                        ? this.RewriteNullableNullEquality(_factory.Syntax, BinaryOperatorKind.NullableNullNotEqual, byType.Expression, nullValue, _factory.SpecialType(SpecialType.System_Boolean))
                        : _factory.ObjectNotEqual(byType.Expression, nullValue);
                    result.Add(_factory.ConditionalGoto(notNull, notNullLabel, true));
                    LowerDecisionTree(byType.Expression, byType.WhenNull, usedTemps, result);
                    if (byType.WhenNull?.MatchIsComplete != true) result.Add(_factory.Goto(defaultLabel));
                    result.Add(_factory.Label(notNullLabel));
                }
                else
                {
                    Debug.Assert(byType.WhenNull == null);
                }

                foreach (var td in byType.TypeAndDecision)
                {
                    // then test for each type, sequentially
                    var type = td.Key;
                    var decision = td.Value;
                    var failLabel = _factory.GenerateLabel("failedDecision");
                    var testAndCopy = TypeTestAndCopyToTemp(byType.Expression, decision.Expression);
                    result.Add(_factory.ConditionalGoto(testAndCopy, failLabel, false));
                    LowerDecisionTree(decision.Expression, decision, usedTemps, result);
                    result.Add(_factory.Label(failLabel));
                }

                // finally, the default for when no type matches
                result.Add(_factory.Label(defaultLabel));
                LowerDecisionTree(byType.Expression, byType.Default, usedTemps, result);
            }
        }

        private BoundExpression TypeTestAndCopyToTemp(BoundExpression input, BoundExpression temp)
        {
            // invariant: the input has already been tested, to ensure it is not null
            if (input == temp)
            {
                return _factory.Literal(true);
            }

            Debug.Assert(temp.Kind == BoundKind.Local);
            return MakeDeclarationPattern(_factory.Syntax, input, ((BoundLocal)temp).LocalSymbol, requiresNullTest: false);
        }

        private void LowerDecisionTree(DecisionTree.ByValue byValue, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            if (byValue.Expression.ConstantValue != null)
            {
                LowerConstantValueDecision(byValue, usedTemps, result);
                return;
            }

            switch (byValue.Type.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_SByte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_String: // switch on a string
                    // switch on an integral or string type
                    LowerBasicSwitch(byValue, usedTemps, result);
                    return;

                case SpecialType.System_Boolean: // switch on a boolean
                    LowerBooleanSwitch(byValue, usedTemps, result);
                    return;

                // switch on a type requiring sequential comparisons. Note that we use constant.Equals(value), depending if
                // possible on the one from IEquatable<T>. If that does not exist, we use instance method object.Equals(object)
                // with the (now boxed) constant on the left.
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                case SpecialType.System_Single:
                    LowerOtherSwitch(byValue, usedTemps, result);
                    return;

                default:
                    if (byValue.Type.TypeKind == TypeKind.Enum)
                    {
                        LowerBasicSwitch(byValue, usedTemps, result);
                        return;
                    }

                    // There are no other types of constants that could be used as patterns.
                    throw ExceptionUtilities.UnexpectedValue(byValue.Type);
            }
        }

        private void LowerConstantValueDecision(DecisionTree.ByValue byValue, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            var value = byValue.Expression.ConstantValue.Value;
            Debug.Assert(value != null);
            DecisionTree onValue;
            if (byValue.ValueAndDecision.TryGetValue(value, out onValue))
            {
                LowerDecisionTree(byValue.Expression, onValue, usedTemps, result);
                if (!onValue.MatchIsComplete) LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
            }
            else
            {
                LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
            }
        }

        private void LowerDecisionTree(DecisionTree.Guarded guarded, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            var targetLabel = guarded.Label.Label;
            Debug.Assert(guarded.Guard?.ConstantValue != ConstantValue.False);
            if (guarded.Guard == null || guarded.Guard.ConstantValue == ConstantValue.True)
            {
                // unconditional
                result.Add(_factory.Goto(targetLabel));
            }
            else
            {
                result.Add(_factory.ConditionalGoto(guarded.Guard, targetLabel, true));
            }
        }

        // For switch statements, we have an option of completely rewriting the switch header
        // and switch sections into simpler constructs, i.e. we can rewrite the switch header
        // using bound conditional goto statements and the rewrite the switch sections into
        // bound labeled statements.
        //
        // However, all the logic for emitting the switch jump tables is language agnostic
        // and includes IL optimizations. Hence we delay the switch jump table generation
        // till the emit phase. This way we also get additional benefit of sharing this code
        // between both VB and C# compilers.
        //
        // For string switch statements, we need to determine if we are generating a hash
        // table based jump table or a non hash jump table, i.e. linear string comparisons
        // with each case label. We use the Dev10 Heuristic to determine this
        // (see SwitchStringJumpTableEmitter.ShouldGenerateHashTableSwitch() for details).
        // If we are generating a hash table based jump table, we use a simple
        // hash function to hash the string constants corresponding to the case labels.
        // See SwitchStringJumpTableEmitter.ComputeStringHash().
        // We need to emit this same function to compute the hash value into the compiler generated
        // <PrivateImplementationDetails> class.
        // If we have at least one string switch statement in a module that needs a
        // hash table based jump table, we generate a single public string hash synthesized method
        // that is shared across the module.
        private void LowerBasicSwitch(DecisionTree.ByValue byValue, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            var switchSections = ArrayBuilder<BoundSwitchSection>.GetInstance();
            var noValueMatches = _factory.GenerateLabel("noValueMatches");
            var underlyingSwitchType = byValue.Type.IsEnumType() ? byValue.Type.GetEnumUnderlyingType() : byValue.Type;
            foreach (var vd in byValue.ValueAndDecision)
            {
                var value = vd.Key;
                var decision = vd.Value;
                var constantValue = ConstantValue.Create(value, underlyingSwitchType.SpecialType);
                var constantExpression = new BoundLiteral(_factory.Syntax, constantValue, underlyingSwitchType);
                var label = _factory.GenerateLabel("case+" + value);
                var switchLabel = new BoundSwitchLabel(_factory.Syntax, label, constantExpression);
                var forValue = ArrayBuilder<BoundStatement>.GetInstance();
                LowerDecisionTree(byValue.Expression, decision, usedTemps, forValue);
                if (!decision.MatchIsComplete) forValue.Add(_factory.Goto(noValueMatches));
                var section = new BoundSwitchSection(_factory.Syntax, ImmutableArray.Create(switchLabel), forValue.ToImmutableAndFree());
                switchSections.Add(section);
            }

            var rewrittenSections = switchSections.ToImmutableAndFree();
            MethodSymbol stringEquality = null;
            if (byValue.Type.SpecialType == SpecialType.System_String)
            {
                EnsureStringHashFunction(rewrittenSections, _factory.Syntax);
                stringEquality = GetSpecialTypeMethod(_factory.Syntax, SpecialMember.System_String__op_Equality);
            }

            // CONSIDER: can we get better code generated by giving a constant target more often here,
            // e.g. when the switch expression is a constant?
            var constantTarget = rewrittenSections.IsEmpty ? noValueMatches : null;
            var switchStatement = new BoundSwitchStatement(
                _factory.Syntax, null, _factory.Convert(underlyingSwitchType, byValue.Expression),
                constantTarget,
                ImmutableArray<LocalSymbol>.Empty, ImmutableArray<LocalFunctionSymbol>.Empty,
                rewrittenSections, noValueMatches, stringEquality);
            result.Add(switchStatement);
            // The bound switch statement implicitly defines the label noValueMatches at the end, so we do not add it explicitly.
            LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
        }

        private void LowerBooleanSwitch(DecisionTree.ByValue byValue, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            switch (byValue.ValueAndDecision.Count)
            {
                case 0:
                    {
                        LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
                        break;
                    }
                case 1:
                    {
                        DecisionTree decision;
                        bool onBoolean = byValue.ValueAndDecision.TryGetValue(true, out decision);
                        if (!onBoolean) byValue.ValueAndDecision.TryGetValue(false, out decision);
                        Debug.Assert(decision != null);
                        var onOther = _factory.GenerateLabel("on" + !onBoolean);
                        result.Add(_factory.ConditionalGoto(byValue.Expression, onOther, !onBoolean));
                        LowerDecisionTree(byValue.Expression, decision, usedTemps, result);
                        // if we fall through here, that means the match was not complete and we invoke the default part
                        result.Add(_factory.Label(onOther));
                        LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
                        break;
                    }
                case 2:
                    {
                        DecisionTree trueDecision, falseDecision;
                        bool hasTrue = byValue.ValueAndDecision.TryGetValue(true, out trueDecision);
                        bool hasFalse = byValue.ValueAndDecision.TryGetValue(false, out falseDecision);
                        Debug.Assert(hasTrue && hasFalse);
                        var tryAnother = _factory.GenerateLabel("tryAnother");
                        var onFalse = _factory.GenerateLabel("onFalse");
                        result.Add(_factory.ConditionalGoto(byValue.Expression, onFalse, false));
                        LowerDecisionTree(byValue.Expression, trueDecision, usedTemps, result);
                        result.Add(_factory.Goto(tryAnother));
                        result.Add(_factory.Label(onFalse));
                        LowerDecisionTree(byValue.Expression, falseDecision, usedTemps, result);
                        result.Add(_factory.Label(tryAnother));
                        // if both true and false (i.e. all values) are fully handled, there should be no default.
                        Debug.Assert(!trueDecision.MatchIsComplete || !falseDecision.MatchIsComplete || byValue.Default == null);
                        LowerDecisionTree(byValue.Expression, byValue.Default, usedTemps, result);
                        break;
                    }
                default:
                    throw ExceptionUtilities.UnexpectedValue(byValue.ValueAndDecision.Count);
            }
        }

        /// <summary>
        /// We handle "other" types, such as float, double, and decimal here. We compare the constant values using IEquatable.
        /// For other value types, since there is no literal notation, there will be no constants to test.
        /// </summary>
        private void LowerOtherSwitch(DecisionTree.ByValue byValue, HashSet<LocalSymbol> usedTemps, ArrayBuilder<BoundStatement> result)
        {
            throw new NotImplementedException();
        }

        // Rewriting for pattern-matching switch statements.
        // This is a temporary translation into a series of if-then-else statements.
        // Ultimately it will be replaced by a translation based on the decision tree.
        private BoundNode VisitPatternSwitchStatement_Ifchain(BoundPatternSwitchStatement node)
        {
            var statements = ArrayBuilder<BoundStatement>.GetInstance();

            // copy the original switch expression into a temp
            BoundAssignmentOperator initialStore;
            var switchExpressionTemp = _factory.StoreToTemp(VisitExpression(node.Expression), out initialStore, syntaxOpt: node.Expression.Syntax);
            statements.Add(_factory.ExpressionStatement(initialStore));

            // save the default label, if and when we find it.
            LabelSymbol defaultLabel = null;

            foreach (var section in node.SwitchSections)
            {
                BoundExpression sectionCondition = _factory.Literal(false);
                bool isDefaultSection = false;
                foreach (var label in section.SwitchLabels)
                {
                    if (label.Syntax.Kind() == SyntaxKind.DefaultSwitchLabel)
                    {
                        // The default label was handled in initial tail, above
                        Debug.Assert(label.Pattern.Kind == BoundKind.WildcardPattern && label.Guard == null);
                        isDefaultSection = true;
                        defaultLabel = _factory.GenerateLabel("default");
                        continue;
                    }

                    var labelCondition = LowerPattern(label.Pattern, switchExpressionTemp);
                    if (label.Guard != null)
                    {
                        labelCondition = _factory.LogicalAnd(labelCondition, VisitExpression(label.Guard));
                    }

                    sectionCondition = _factory.LogicalOr(sectionCondition, labelCondition);
                }

                var sectionBuilder = ArrayBuilder<BoundStatement>.GetInstance();
                if (isDefaultSection)
                {
                    sectionBuilder.Add(_factory.Label(defaultLabel));
                }
                sectionBuilder.AddRange(VisitList(section.Statements));
                sectionBuilder.Add(_factory.Goto(node.BreakLabel));
                statements.Add(_factory.If(sectionCondition, section.Locals, _factory.Block(sectionBuilder.ToImmutableAndFree())));
            }

            if (defaultLabel != null)
            {
                statements.Add(_factory.Goto(defaultLabel));
            }

            statements.Add(_factory.Label(node.BreakLabel));
            _factory.Syntax = node.Syntax;
            return _factory.Block(node.InnerLocals.Add(switchExpressionTemp.LocalSymbol), node.InnerLocalFunctions, statements.ToImmutableAndFree());
        }
    }
}
