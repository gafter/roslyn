// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class BoundSequence
    {
        private readonly ImmutableArray<BoundNode> _sideEffects;

        public BoundSequence(SyntaxNode syntax, ImmutableArray<LocalSymbol> locals, ImmutableArray<BoundExpression> sideEffects, BoundExpression value, TypeSymbol type, bool hasErrors = false)
            : base(BoundKind.Sequence, syntax, type, hasErrors || sideEffects.HasErrors() || value.HasErrors())
        {
            Debug.Assert(!locals.IsDefault, "Field 'locals' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");
            Debug.Assert(!sideEffects.IsDefault, "Field 'sideEffects' cannot be null");
            Debug.Assert(value != null, "Field 'value' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");
            Debug.Assert(type != null, "Field 'type' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");

            this.Locals = locals;
            this._sideEffects = sideEffects.CastArray<BoundNode>();
            this.Value = value;
        }

        internal BoundSequence(SyntaxNode syntax, ImmutableArray<LocalSymbol> locals, ImmutableArray<BoundNode> sideEffects, BoundExpression value, TypeSymbol type, bool hasErrors = false)
            : base(BoundKind.Sequence, syntax, type, hasErrors || sideEffects.HasErrors() || value.HasErrors())
        {
            Debug.Assert(!locals.IsDefault, "Field 'locals' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");
            Debug.Assert(!sideEffects.IsDefault, "Field 'sideEffects' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");
            Debug.Assert(value != null, "Field 'value' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");
            Debug.Assert(type != null, "Field 'type' cannot be null (use Null=\"allow\" in BoundNodes.xml to remove this check)");

#if DEBUG
            // Ensure nested side effects are of the permitted kinds only
            foreach (var node in sideEffects)
            {
                switch (node.Kind)
                {
                    case BoundKind.ExpressionStatement:
                    case BoundKind.GotoStatement:
                    case BoundKind.ConditionalGoto:
                    case BoundKind.SwitchStatement:
                    case BoundKind.SequencePoint:
                    case BoundKind.LabelStatement:
                    case BoundKind.ForwardLabels:
                    case BoundKind.ThrowStatement:
                        break;
                    default:
                        Debug.Assert(node is BoundExpression);
                        break;
                }
            }
#endif

            this.Locals = locals;
            this._sideEffects = sideEffects;
            this.Value = value;
        }

        public BoundSequence Update(ImmutableArray<LocalSymbol> locals, ImmutableArray<BoundNode> sideEffects, BoundExpression value, TypeSymbol type)
        {
            if (locals != this.Locals || sideEffects != this._sideEffects || value != this.Value || type != this.Type)
            {
                var result = new BoundSequence(this.Syntax, locals, sideEffects, value, type, this.HasErrors);
                result.WasCompilerGenerated = this.WasCompilerGenerated;
                return result;
            }
            return this;
        }

        public BoundSequence Update(ImmutableArray<LocalSymbol> locals, ImmutableArray<BoundExpression> sideEffects, BoundExpression value, TypeSymbol type)
        {
            return Update(locals, sideEffects.CastArray<BoundNode>(), value, type);
        }

        public ImmutableArray<BoundNode> SideEffects => _sideEffects;

        /// <summary>
        /// PROTOTYPE(patterns2): Used to assist in migrating components of the compiler that are not yet capable of
        /// handling statements in side-effects, which can result from lowering the switch expression.
        /// </summary>
        public ImmutableArray<BoundExpression> SideEffectsExprOnlyUNSAFE
        {
            get
            {
                var builder = ArrayBuilder<BoundExpression>.GetInstance();
                foreach (var effect in _sideEffects)
                {
                    builder.Add((BoundExpression)effect);
                }

                return builder.ToImmutableAndFree();
            }
        }
    }
}
