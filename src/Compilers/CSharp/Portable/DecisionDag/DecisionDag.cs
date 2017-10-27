// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.DecisionDag
{
    internal abstract class DecisionDag
    {
        private DecisionDag() { }

        /// <summary>
        /// A single test in the directed acyclic control graph (flow chart?) of decision points.
        /// </summary>
        internal class Test : DecisionDag
        {
            public readonly Decision Condition;
            public readonly DecisionDag WhenTrue;
            public readonly DecisionDag WhenFalse;
        }

        /// <summary>
        /// The final test that may take us to user code (the where clause and/or the case block)
        /// </summary>
        internal class Where : DecisionDag
        {
            public readonly ImmutableArray<(ValueIdentifier Datum, LocalSymbol Variable)> Bindings;
            public readonly BoundExpression Condition;
            public readonly LabelSymbol WhenTrue;
            public readonly DecisionDag WhenFalse;
        }

        /// <summary>
        /// Indicates failure (no case matches)
        /// </summary>
        internal class NoDecision : DecisionDag
        {
        }
    }

}
