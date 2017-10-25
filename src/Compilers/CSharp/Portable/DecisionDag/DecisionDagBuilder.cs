// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.DecisionDag
{
    internal class DecisionDagBuilder
    {
        public static DecisionDag CreateDecisionDag(BoundPatternSwitchStatement statement)
        {
            ImmutableArray<PartialCaseDecision> cases = MakeCases(statement);
            DecisionDag dag = MakeDecisionDag(cases);
            return dag;
        }

        private static ImmutableArray<PartialCaseDecision> MakeCases(BoundPatternSwitchStatement statement)
        {
            var rootIdentifier = new ValueIdentifier.Root(statement.Expression.Type);
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

        private static PartialCaseDecision MakePartialCaseDecision(int index, ValueIdentifier input, BoundPatternSwitchLabel label)
        {
            var decisions = ArrayBuilder<Decision>.GetInstance();
            var bindings = ArrayBuilder<Binding>.GetInstance();
            MakeDecisionsAndBindings(input, label.Pattern, decisions, bindings);
            return new PartialCaseDecision(index, decisions.ToImmutableAndFree(), bindings.ToImmutableAndFree(), label.Guard, label.Label);
        }

        private static void MakeDecisionsAndBindings(ValueIdentifier input, BoundPattern pattern, ArrayBuilder<Decision> decisions, ArrayBuilder<Binding> bindings)
        {
            switch (pattern)
            {
                case BoundDeclarationPattern declaration:
                case BoundConstantPattern constant:
                case BoundWildcardPattern wildcard:
                default:
                    throw new NotImplementedException(pattern.Kind.ToString());
            }
        }

        private static DecisionDag MakeDecisionDag(ImmutableArray<PartialCaseDecision> cases)
        {
            throw new NotImplementedException();
        }
    }

    struct Binding
    {
        public readonly ValueIdentifier Datum;
        public readonly LocalSymbol Variable;
    }

    internal class PartialCaseDecision
    {
        public readonly int Index;
        public readonly ImmutableArray<Decision> Decisions;
        public readonly ImmutableArray<Binding> Bindings;
        public readonly BoundExpression WhereClause;
        public readonly LabelSymbol CaseLabel;
        public PartialCaseDecision(int Index, ImmutableArray<Decision> Decisions, ImmutableArray<Binding> Bindings, BoundExpression WhereClause, LabelSymbol CaseLabel)
        {
            this.Index = Index;
            this.Decisions = Decisions;
            this.Bindings = Bindings;
            this.WhereClause = WhereClause;
            this.CaseLabel = CaseLabel;
        }
    }
}
