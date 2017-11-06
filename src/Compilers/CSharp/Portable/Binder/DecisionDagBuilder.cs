// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class Decision
    {
        public BoundExpression Input;
        public BoundExpression Evaluation;

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    internal class DecisionDagBuilder
    {
        private readonly Conversions Conversions;
        private readonly TypeSymbol BooleanType;

        internal DecisionDagBuilder(CSharpCompilation compilation)
        {
            this.Conversions = compilation.Conversions;
            this.BooleanType = compilation.GetSpecialType(SpecialType.System_Boolean);
        }

        public BoundDecisionDag CreateDecisionDag(BoundPatternSwitchStatement statement)
        {
            ImmutableArray<PartialCaseDecision> cases = MakeCases(statement);
            BoundDecisionDag dag = MakeDecisionDag(cases);
            return dag;
        }

        private ImmutableArray<PartialCaseDecision> MakeCases(BoundPatternSwitchStatement statement)
        {
            var rootIdentifier = new RootValueIdentifier(statement.Expression.Syntax, statement.Expression.Type);
            int i = 0;
            var builder = ArrayBuilder<PartialCaseDecision>.GetInstance();
            foreach (var section in statement.SwitchSections)
            {
                foreach (var label in section.SwitchLabels)
                {
                    builder.Add(MakePartialCaseDecision(++i, rootIdentifier, label));
                }
            }

            return builder.ToImmutableAndFree();
        }

        private PartialCaseDecision MakePartialCaseDecision(int index, BoundExpression input, BoundPatternSwitchLabel label)
        {
            var decisions = ArrayBuilder<Decision>.GetInstance();
            var bindings = ArrayBuilder<BoundAssignmentOperator>.GetInstance();
            MakeDecisionsAndBindings(input, label.Pattern, decisions, bindings);
            return new PartialCaseDecision(index, decisions.ToImmutableAndFree(), bindings.ToImmutableAndFree(), label.Guard, label.Label);
        }

        private void MakeDecisionsAndBindings(BoundExpression input, BoundPattern pattern, ArrayBuilder<Decision> decisions, ArrayBuilder<BoundAssignmentOperator> bindings)
        {
            // Use-site diagnostics will have been produced when binding the pattern, so would be redundant if reported here.
            HashSet<DiagnosticInfo> discardedUseSiteDiagnostics = null;
            switch (pattern)
            {
                case BoundDeclarationPattern declaration:
                    {
                        var type = declaration.DeclaredType.Type;
                        switch (Binder.ExpressionOfTypeMatchesPatternType(Conversions, input.Type, type, ref discardedUseSiteDiagnostics, out Conversion conversion))
                        {
                            case false:
                                {
                                    // The match is not possible.
                                    // This should not occur unless the pattern is erroneous, which will have been reported earlier in binding
                                    var evaluation = new BoundLiteral(pattern.Syntax, ConstantValue.False, BooleanType);
                                    decisions.Add(new Decision() { Input = input, Evaluation = evaluation });
                                    goto case true;
                                }
                            case null:
                                {
                                    // The match is possible. Add a test.
                                    var evaluation = new BoundIsOperator(pattern.Syntax, input, declaration.DeclaredType, conversion, BooleanType);
                                    decisions.Add(new Decision() { Input = input, Evaluation = evaluation });
                                    goto case true;
                                }
                            case true:
                                {
                                    var left = declaration.VariableAccess;
                                    if (left != null)
                                    {
                                        // Add a binding.
                                        var right = new BoundConversion(pattern.Syntax, input, conversion, false, false, null, type);
                                        bindings.Add(new BoundAssignmentOperator(pattern.Syntax, left, right, RefKind.None, type));
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case BoundConstantPattern constant:
                    {
                        throw new NotImplementedException();
                    }
                case BoundWildcardPattern wildcard:
                    {
                        throw new NotImplementedException();
                    }
                case BoundRecursivePattern recursive:
                    {
                        throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException(pattern.Kind.ToString());
            }
        }

        private static BoundDecisionDag MakeDecisionDag(ImmutableArray<PartialCaseDecision> cases)
        {
            throw new NotImplementedException();
        }
    }

    internal class PartialCaseDecision
    {
        public readonly int Index;
        public readonly ImmutableArray<Decision> Decisions;
        public readonly ImmutableArray<BoundAssignmentOperator> Bindings;
        public readonly BoundExpression WhereClause;
        public readonly LabelSymbol CaseLabel;
        public PartialCaseDecision(int Index, ImmutableArray<Decision> Decisions, ImmutableArray<BoundAssignmentOperator> Bindings, BoundExpression WhereClause, LabelSymbol CaseLabel)
        {
            this.Index = Index;
            this.Decisions = Decisions;
            this.Bindings = Bindings;
            this.WhereClause = WhereClause;
            this.CaseLabel = CaseLabel;
        }
    }
}
