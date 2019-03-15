// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class NullableWalker
    {
        /// <summary>
        /// Learn something about the input from a test of a given expression against a given pattern.  The given
        /// state is updated to note that any slots that are tested against `null` may be null.
        /// </summary>
        private void LearnFromPattern(
            BoundExpression expression,
            TypeSymbol expressionType,
            BoundPattern pattern,
            ref LocalState stateToUpdate)
        {
            int slot = MakeSlot(expression);
            LearnFromPattern(slot, expressionType, pattern, ref stateToUpdate);
        }

        /// <summary>
        /// Learn from any constant null patterns appearing in the pattern.
        /// </summary>
        /// <param name="originalInputType">Tye type of the input expression (before nullable analysis).
        /// Used to determine which types can contain null.</param>
        private void LearnFromPattern(
            int inputSlot,
            TypeSymbol originalInputType,
            BoundPattern pattern,
            ref LocalState stateToUpdate)
        {
            if (inputSlot <= 0)
                return;

            switch (pattern)
            {
                case BoundConstantPattern cp:
                    if (cp.Value.ConstantValue == ConstantValue.Null)
                    {
                        LearnFromNullTest(inputSlot, originalInputType, ref stateToUpdate);
                    }
                    break;
                case BoundDeclarationPattern dp:
                    if (dp.Variable != null && dp.DeclaredType is null)
                    {
                        // we permit var-declared pattern variables to be assigned null.
                        _variableTypes[dp.Variable] = new TypeWithState(originalInputType, NullableFlowState.MaybeNull).ToTypeSymbolWithAnnotations();
                    }
                    break;
                case BoundDiscardPattern _:
                case BoundITuplePattern _:
                    break; // nothing to learn
                case BoundRecursivePattern rp:
                    {
                        if (!rp.ConvertedType.Equals(originalInputType, TypeCompareKind.AllIgnoreOptions))
                            break;

                        // for positional part: we only learn from tuples (not Deconstruct invocations or ITuple indexing)
                        if (!rp.Deconstruction.IsDefault)
                        {
                            if (rp.DeconstructMethod is null)
                            {
                                var elements = originalInputType.TupleElements;
                                for (int i = 0, n = Math.Min(rp.Deconstruction.Length, elements.IsDefault ? 0 : elements.Length); i < n; i++)
                                {
                                    BoundSubpattern item = rp.Deconstruction[i];
                                    FieldSymbol element = elements[i];
                                    LearnFromPattern(GetOrCreateSlot(element, inputSlot), element.Type.TypeSymbol, item.Pattern, ref stateToUpdate);
                                }
                            }
                        }

                        // for property part
                        if (!rp.Properties.IsDefault)
                        {
                            for (int i = 0, n = rp.Properties.Length; i < n; i++)
                            {
                                BoundSubpattern item = rp.Properties[i];
                                Symbol symbol = item.Symbol;
                                if (symbol is null)
                                    continue;
                                LearnFromPattern(GetOrCreateSlot(symbol, inputSlot), symbol.GetTypeOrReturnType().TypeSymbol, item.Pattern, ref stateToUpdate);
                            }
                        }
                    }
                    break;
            }
        }

        protected override LocalState VisitSwitchStatementDispatch(BoundSwitchStatement node)
        {
            // first, learn from any null tests in the patterns
            int slot = MakeSlot(node.Expression);
            if (slot > 0)
            {
                var originalInputType = node.Expression.Type;
                foreach (var section in node.SwitchSections)
                {
                    foreach (var label in section.SwitchLabels)
                    {
                        LearnFromPattern(slot, originalInputType, label.Pattern, ref this.State);
                    }
                }
            }

            // visit switch header
            var expressionState = VisitRvalueWithState(node.Expression);
            LocalState initialState = this.State.Clone();
            var labelStateMap = LearnFromDecisionDag(node.Syntax, node.DecisionDag, node.Expression, expressionState, ref initialState);

            foreach (var section in node.SwitchSections)
            {
                foreach (var label in section.SwitchLabels)
                {
                    var labelResult = labelStateMap.TryGetValue(label.Label, out var s1) ? s1 : (state: UnreachableState(), believedReachable: false);
                    SetState(labelResult.state);
                    PendingBranches.Add(new PendingBranch(label, this.State, label.Label));
                }
            }

            labelStateMap.Free();
            return initialState;
        }

        private PooledDictionary<LabelSymbol, (LocalState state, bool believedReachable)>
            LearnFromDecisionDag(
            SyntaxNode node,
            BoundDecisionDag decisionDag,
            BoundExpression expression,
            TypeWithState expressionType,
            ref LocalState initialState)
        {
            var nodeStateMap = PooledDictionary<BoundDecisionDagNode, (LocalState state, bool believedReachable)>.GetInstance();
            nodeStateMap.Add(decisionDag.RootNode, (state: initialState.Clone(), believedReachable: true));

            var tempMap = PooledDictionary<BoundDagTemp, (int slot, TypeWithState type)>.GetInstance();
            var rootTemp = BoundDagTemp.ForOriginalInput(expression);

            // We create a fresh slot to track the switch expression, as it is copied at the start of the switch.
            // We use the syntax to identify the root slot to ensure we don't share the slots between possibly nested switches.
            int originalInputSlot = makeDagTempSlot(expressionType.ToTypeSymbolWithAnnotations(), rootTemp);
            Debug.Assert(originalInputSlot > 0);
            tempMap.Add(rootTemp, (originalInputSlot, expressionType));

            var labelStateMap = PooledDictionary<LabelSymbol, (LocalState state, bool believedReachable)>.GetInstance();

            foreach (var dagNode in decisionDag.TopologicallySortedNodes)
            {
                bool found = nodeStateMap.TryGetValue(dagNode, out var nodeStateAndBelievedReachable);
                Debug.Assert(found); // the topologically sorted nodes should contain only reachable nodes
                (LocalState nodeState, bool nodeBelievedReachable) = nodeStateAndBelievedReachable;
                SetState(nodeState);

                switch (dagNode)
                {
                    case BoundEvaluationDecisionDagNode p:
                        {
                            var evaluation = p.Evaluation;
                            (int inputSlot, TypeWithState inputType) = tempMap.TryGetValue(evaluation.Input, out var slotAndType) ? slotAndType : throw ExceptionUtilities.Unreachable;
                            Debug.Assert(inputSlot > 0);
                            if (inputSlot > 0)
                                inputType = new TypeWithState(inputType.Type, this.State[inputSlot]);

                            //BoundDagTemp output;
                            switch (evaluation)
                            {
                                case BoundDagDeconstructEvaluation e:
                                    {
                                        var method = e.DeconstructMethod;
                                        int extensionExtra = method.IsStatic ? 1 : 0;
                                        for (int i = 0; i < method.ParameterCount - extensionExtra; i++)
                                        {
                                            var parameterType = method.Parameters[i + extensionExtra].Type;
                                            var output = new BoundDagTemp(e.Syntax, parameterType.TypeSymbol, e, i);
                                            int outputSlot = makeDagTempSlot(parameterType, output);
                                            Debug.Assert(outputSlot > 0);
                                            addToTempMap(output, outputSlot, parameterType.ToTypeWithState());
                                        }
                                        break;
                                    }
                                case BoundDagTypeEvaluation e:
                                    {
                                        var output = new BoundDagTemp(e.Syntax, e.Type, e);
                                        int outputSlot = inputSlot;
                                        var outputType = new TypeWithState(e.Type, inputType.State);
                                        addToTempMap(output, outputSlot, outputType);
                                        break;
                                    }
                                case BoundDagFieldEvaluation e:
                                    {
                                        // PROTOTYPE(ngafter): Need to create placeholder slot for dag temps
                                        Debug.Assert(inputSlot > 0);
                                        int outputSlot = GetOrCreateSlot(e.Field, inputSlot);
                                        Debug.Assert(outputSlot > 0);
                                        // PROTOTYPE(ngafter): ensure we initialize the state from the field when creating a slot
                                        var type = e.Field.Type.TypeSymbol;
                                        var output = new BoundDagTemp(e.Syntax, type, e);
                                        addToTempMap(output, outputSlot, new TypeWithState(type, this.State[outputSlot]));
                                        break;
                                    }
                                case BoundDagPropertyEvaluation e:
                                    {
                                        // PROTOTYPE(ngafter): Need to create placeholder slot for dag temps
                                        Debug.Assert(inputSlot > 0);
                                        // PROTOTYPE(ngafter): ensure we initialize the state from the property when creating a slot
                                        var type = e.Property.Type.TypeSymbol;
                                        var output = new BoundDagTemp(e.Syntax, type, e);
                                        int outputSlot = GetOrCreateSlot(e.Property, inputSlot);
                                        Debug.Assert(outputSlot > 0);
                                        addToTempMap(output, outputSlot, new TypeWithState(type, this.State[outputSlot]));
                                        break;
                                    }
                                case BoundDagIndexEvaluation e:
                                    {
                                        var type = e.Property.Type;
                                        var output = new BoundDagTemp(e.Syntax, type.TypeSymbol, e);
                                        int outputSlot = makeDagTempSlot(type, output);
                                        Debug.Assert(outputSlot > 0);
                                        addToTempMap(output, outputSlot, type.ToTypeWithState());
                                        break;
                                    }
                                default:
                                    throw ExceptionUtilities.UnexpectedValue(p.Evaluation.Kind);
                            }
                            gotoNode(p.Next, this.State, nodeBelievedReachable);
                            break;
                        }
                    case BoundTestDecisionDagNode p:
                        {
                            var test = p.Test;
                            bool foundTemp = tempMap.TryGetValue(test.Input, out var slotAndType);
                            Debug.Assert(foundTemp);

                            (int inputSlot, TypeWithState inputType) = slotAndType;
                            if (inputSlot > 0)
                            {
                                inputType = new TypeWithState(inputType.Type, this.State[inputSlot]);
                            }
                            Split();
                            switch (test)
                            {
                                case BoundDagTypeTest t:
                                    if (inputSlot > 0)
                                    {
                                        this.StateWhenTrue[inputSlot] = NullableFlowState.NotNull;
                                        if (inputSlot == originalInputSlot)
                                            LearnFromNonNullTest(expression, ref this.StateWhenTrue);
                                    }
                                    gotoNode(p.WhenTrue, this.StateWhenTrue, nodeBelievedReachable);
                                    gotoNode(p.WhenFalse, this.StateWhenFalse, nodeBelievedReachable);
                                    break;
                                case BoundDagNonNullTest t:
                                    if (inputSlot > 0)
                                    {
                                        this.StateWhenTrue[inputSlot] = NullableFlowState.NotNull;
                                        if (inputSlot == originalInputSlot)
                                            LearnFromNonNullTest(expression, ref this.StateWhenTrue);
                                    }
                                    gotoNode(p.WhenTrue, this.StateWhenTrue, nodeBelievedReachable);
                                    gotoNode(p.WhenFalse, this.StateWhenFalse, nodeBelievedReachable & inputType.MayBeNull);
                                    break;
                                case BoundDagExplicitNullTest t:
                                    if (inputSlot > 0)
                                    {
                                        this.StateWhenTrue[inputSlot] = NullableFlowState.MaybeNull;
                                        this.StateWhenFalse[inputSlot] = NullableFlowState.NotNull;
                                        if (inputSlot == originalInputSlot)
                                            LearnFromNonNullTest(expression, ref this.StateWhenFalse);
                                    }
                                    gotoNode(p.WhenTrue, this.StateWhenTrue, nodeBelievedReachable);
                                    gotoNode(p.WhenFalse, this.StateWhenFalse, nodeBelievedReachable);
                                    break;
                                case BoundDagValueTest t:
                                    Debug.Assert(t.Value != ConstantValue.Null);
                                    if (inputSlot > 0)
                                    {
                                        this.StateWhenTrue[inputSlot] = NullableFlowState.NotNull;
                                        if (inputSlot == originalInputSlot)
                                            LearnFromNonNullTest(expression, ref this.StateWhenTrue);
                                    }
                                    gotoNode(p.WhenTrue, this.StateWhenTrue, nodeBelievedReachable);
                                    gotoNode(p.WhenFalse, this.StateWhenFalse, nodeBelievedReachable);
                                    break;
                                default:
                                    throw ExceptionUtilities.UnexpectedValue(test.Kind);
                            }
                            break;
                        }
                    case BoundLeafDecisionDagNode d:
                        bool labelBelievedReachable = nodeBelievedReachable;
                        if (labelStateMap.TryGetValue(d.Label, out var existingLabelStateAndBelievedReachable))
                        {
                            Join(ref this.State, ref existingLabelStateAndBelievedReachable.state);
                            labelBelievedReachable |= existingLabelStateAndBelievedReachable.believedReachable;
                        }
                        labelStateMap[d.Label] = (this.State, labelBelievedReachable);
                        break;
                    case BoundWhenDecisionDagNode w:
                        // bind the pattern variables, inferring their types as well
                        foreach (var binding in w.Bindings)
                        {
                            var variableAccess = binding.VariableAccess;
                            var tempSource = binding.TempContainingValue;
                            var foundTemp = tempMap.TryGetValue(tempSource, out var tempType);
                            Debug.Assert(foundTemp);
                            if (variableAccess is BoundLocal { LocalSymbol: SourceLocalSymbol { IsVar: true } local })
                            {
                                var inferredType = tempType.type.ToTypeSymbolWithAnnotations();
                                if (_variableTypes.TryGetValue(local, out var existingType))
                                {
                                    // merge inferred nullable annotation from different branches of the decision tree
                                    _variableTypes[local] = TypeSymbolWithAnnotations.Create(existingType.TypeSymbol, existingType.NullableAnnotation.Join(inferredType.NullableAnnotation));
                                }
                                else
                                {
                                    _variableTypes[local] = inferredType;
                                }

                                int localSlot = GetOrCreateSlot(local);
                                this.State[localSlot] = tempType.type.State;
                            }
                            else
                            {
                                // https://github.com/dotnet/roslyn/issues/34144 perform inference for top-level var-declared fields in scripts
                            }
                        }

                        if (w.WhenExpression != null && w.WhenExpression.ConstantValue != ConstantValue.True)
                        {
                            VisitCondition(w.WhenExpression);
                            Debug.Assert(this.IsConditionalState);
                            gotoNode(w.WhenTrue, this.StateWhenTrue, nodeBelievedReachable);
                            gotoNode(w.WhenFalse, this.StateWhenFalse, nodeBelievedReachable);
                        }
                        else
                        {
                            Debug.Assert(w.WhenFalse is null);
                            gotoNode(w.WhenTrue, this.State, nodeBelievedReachable);
                        }
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(dagNode.Kind);
                }
            }

            SetUnreachable(); // the decision dag is always complete (no fall-through)
            tempMap.Free();
            nodeStateMap.Free();
            return labelStateMap;

            void addToTempMap(BoundDagTemp output, int slot, TypeWithState state)
            {
                // We need to track all dag temps, so there should be a slot
                Debug.Assert(slot > 0);
                if (tempMap.TryGetValue(output, out var outputSlotAndType))
                {
                    Debug.Assert(outputSlotAndType.slot == slot);
                    Debug.Assert(outputSlotAndType.type.Type.Equals(state.Type, TypeCompareKind.AllIgnoreOptions));
                    // PROTOTYPE(ngafter): merge the nullability from the map with the new computed nullability
                }
                else
                {
                    tempMap.Add(output, (slot, state));
                }
            }

            void gotoNode(BoundDecisionDagNode node, LocalState state, bool believedReachable)
            {
                if (nodeStateMap.TryGetValue(node, out var stateAndReachable))
                {
                    Join(ref state, ref stateAndReachable.state);
                    believedReachable |= stateAndReachable.believedReachable;
                }

                nodeStateMap[node] = (state, believedReachable);
            }

            int makeDagTempSlot(TypeSymbolWithAnnotations type, BoundDagTemp temp)
            {
                object slotKey = (node, temp);
                return GetOrCreatePlaceholderSlot(slotKey, type);
            }
        }

        public override BoundNode VisitSwitchExpression(BoundSwitchExpression node)
        {
            // first, learn from any null tests in the patterns
            int slot = MakeSlot(node.Expression);
            if (slot > 0)
            {
                var originalInputType = node.Expression.Type;
                foreach (var arm in node.SwitchArms)
                {
                    LearnFromPattern(slot, originalInputType, arm.Pattern, ref this.State);
                }
            }

            var expressionState = VisitRvalueWithState(node.Expression);
            var labelStateMap = LearnFromDecisionDag(node.Syntax, node.DecisionDag, node.Expression, expressionState, ref this.State);
            var endState = UnreachableState();

            if (!node.ReportedNotExhaustive && node.DefaultLabel != null &&
                labelStateMap.TryGetValue(node.DefaultLabel, out var defaultLabelState) && defaultLabelState.believedReachable)
            {
                this.ReportSafetyDiagnostic(ErrorCode.WRN_SwitchExpressionNotExhaustiveForNull, node.Syntax);
            }

            foreach (var arm in node.SwitchArms)
            {
                LocalState stateForLabel = labelStateMap.TryGetValue(arm.Label, out var labelState) ? labelState.state : UnreachableState();
                SetState(stateForLabel);
                if (arm.Pattern.HasErrors)
                    SetUnreachable();

                VisitRvalue(arm.Value);
                Join(ref endState, ref this.State);
            }

            SetState(endState);

            // PROTOTYPE(ngafter): re-infer the result type of the switch from the values
            this.ResultType = TypeSymbolWithAnnotations.Create(node.Type).ToTypeWithState();
            return null;
        }

        public override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node)
        {
            Debug.Assert(!IsConditionalState);
            var expressionState = VisitRvalueWithState(node.Expression);
            LearnFromPattern(node.Expression, expressionState.Type, node.Pattern, ref this.State);
            var labelStateMap = LearnFromDecisionDag(node.Syntax, node.DecisionDag, node.Expression, expressionState, ref this.State);
            // PROTOTYPE(ngafter): simplify this code once code coverage has been confirmed
            //var trueState = labelStateMap.TryGetValue(node.WhenTrueLabel, out var s1) ? s1.state : UnreachableState();
            //var falseState = labelStateMap.TryGetValue(node.WhenFalseLabel, out var s2) ? s2.state : UnreachableState();
            LocalState trueState, falseState;
            if (labelStateMap.TryGetValue(node.WhenTrueLabel, out var s1))
                trueState = s1.state;
            else
                trueState = UnreachableState();

            if (labelStateMap.TryGetValue(node.WhenFalseLabel, out var s2))
                falseState = s2.state;
            else
                falseState = UnreachableState();

            labelStateMap.Free();
            SetConditionalState(trueState, falseState);
            SetNotNullResult(node);
            return null;
        }
    }
}
