// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.DecisionDag
{
    abstract class Decision
    {
        private Decision() { }

        public abstract class ValueDecision : Decision
        {
            public readonly ValueIdentifier Input;
            public readonly TypeSymbol InputType;

            public ValueDecision(ValueIdentifier Input, TypeSymbol InputType)
            {
                this.Input = Input;
                this.InputType = InputType;
            }
            public void Deconstruct(out ValueIdentifier Input, out TypeSymbol InputType)
            {
                Input = this.Input;
                InputType = this.InputType;
            }
        }

        public sealed class ConstantDecision : ValueDecision
        {
            public readonly ConstantValue Value;

            public ConstantDecision(ValueIdentifier Input, TypeSymbol InputType, ConstantValue Value) : base(Input, InputType)
            {
                this.Value = Value;
            }

            public void Deconstruct(out ValueIdentifier Input, out TypeSymbol InputType, out ConstantValue Value)
            {
                base.Deconstruct(out Input, out InputType);
                Value = this.Value;
            }
        }

        public sealed class NonNullDecision : ValueDecision
        {
            public NonNullDecision(ValueIdentifier Input, TypeSymbol InputType) : base(Input, InputType)
            {
            }
        }
    }
}
