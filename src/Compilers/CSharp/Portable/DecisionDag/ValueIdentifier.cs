// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.DecisionDag
{
    internal abstract class ValueIdentifier
    {
        public readonly TypeSymbol Type;

        private ValueIdentifier(TypeSymbol Type)
        {
            this.Type = Type;
        }

        public void Deconstruct(out TypeSymbol Type) => Type = this.Type;
        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();

        internal sealed class Root : ValueIdentifier
        {
            public Root(TypeSymbol Type) : base(Type) { }
            public override string ToString() => "";
            public override int GetHashCode() => Type.GetHashCode();
            public override bool Equals(object obj) => obj is Root other && Type.Equals(other.Type);
        }

        internal sealed class DeconstuctionElement : ValueIdentifier
        {
            public readonly ValueIdentifier Base;
            public readonly int Index;

            public DeconstuctionElement(ValueIdentifier Base, int Index, TypeSymbol Type) : base(Type)
            {
                this.Base = Base;
                this.Index = Index;
            }

            public override string ToString() => Base.ToString() + "." + Index;
            public override int GetHashCode() => Base.GetHashCode() * 17 + Index + Type.GetHashCode();
            public bool Equals(DeconstuctionElement other) => Base.Equals(other.Base) && Index.Equals(other.Index) && Type.Equals(other.Type);
            public override bool Equals(object obj) => obj is DeconstuctionElement other && this.Equals(other);
            public void Deconstruct(out ValueIdentifier Base, out int Index, out TypeSymbol Type)
            {
                Base = this.Base;
                Index = this.Index;
                base.Deconstruct(out Type);
            }
        }

        internal sealed class PropertyElement : ValueIdentifier
        {
            public readonly ValueIdentifier Base;
            public readonly string Name;

            public PropertyElement(ValueIdentifier Base, string Name, TypeSymbol Type) : base(Type)
            {
                this.Base = Base;
                this.Name = Name;
            }

            public override string ToString() => Base.ToString() + "." + Name;
            public override int GetHashCode() => Base.GetHashCode() * 17 + Name.GetHashCode() + Type.GetHashCode();
            public bool Equals(PropertyElement other) => this.Base.Equals(other.Base) && this.Name.Equals(other.Name) && this.Type.Equals(other.Type);
            public override bool Equals(object obj) => obj is PropertyElement other && this.Equals(other);
            public void Deconstruct(out ValueIdentifier Base, out string Name, out TypeSymbol Type)
            {
                Base = this.Base;
                Name = this.Name;
                base.Deconstruct(out Type);
            }
        }
    }
}
