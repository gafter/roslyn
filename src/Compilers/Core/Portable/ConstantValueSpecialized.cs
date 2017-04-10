// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal partial class ConstantValue
    {
        private sealed class ConstantValueBad : ConstantValue
        {
            private ConstantValueBad() { }

            public readonly static ConstantValueBad Instance = new ConstantValueBad();

            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Bad;

            internal override SpecialType SpecialType => SpecialType.None;

            // all instances of this class are singletons
            public override bool Equals(ConstantValue other) => ReferenceEquals(this, other);

            public override int GetHashCode()
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            }

            internal override string GetValueToDisplay()
            {
                return "bad";
            }
        }

        private sealed class ConstantValueNull : ConstantValue
        {
            private ConstantValueNull() { }

            public readonly static ConstantValueNull Instance = new ConstantValueNull();

            public readonly static ConstantValueNull Uninitialized = new ConstantValueNull();

            public override ConstantValueTypeDiscriminator Discriminator =>  ConstantValueTypeDiscriminator.Null;

            internal override SpecialType SpecialType => SpecialType.None;

            public override string StringValue => null;

            // all instances of this class are singletons
            public override bool Equals(ConstantValue other) => ReferenceEquals(this, other);

            public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

            public override bool IsDefaultValue => true;

            internal override string GetValueToDisplay()
            {
                return ((object)this == (object)Uninitialized) ? "unset" : "null";
            }
        }

        private sealed class ConstantValueString : ConstantValue
        {
            private readonly string _value;

            public ConstantValueString(string value)
            {
                // we should have just one Null regardless string or object.
                System.Diagnostics.Debug.Assert(value != null, "null strings should be represented as Null constant.");
                _value = value;
            }

            public override ConstantValueTypeDiscriminator Discriminator
            {
                get
                {
                    return ConstantValueTypeDiscriminator.String;
                }
            }

            internal override SpecialType SpecialType
            {
                get { return SpecialType.System_String; }
            }

            public override string StringValue
            {
                get
                {
                    return _value;
                }
            }

            public override int GetHashCode()
            {
                return Hash.Combine(base.GetHashCode(), _value.GetHashCode());
            }

            public override bool Equals(ConstantValue other)
            {
                return base.Equals(other) && _value == other.StringValue;
            }

            internal override string GetValueToDisplay()
            {
                return (_value == null) ? "null" : string.Format("\"{0}\"", _value);
            }
            public override bool IsDefaultValue => _value == null;
        }

        private sealed class ConstantValueDecimal : ConstantValue
        {
            private readonly decimal _value;

            public ConstantValueDecimal(decimal value)
            {
                _value = value;
            }

            public override ConstantValueTypeDiscriminator Discriminator
            {
                get
                {
                    return ConstantValueTypeDiscriminator.Decimal;
                }
            }

            internal override SpecialType SpecialType
            {
                get { return SpecialType.System_Decimal; }
            }

            public override decimal DecimalValue
            {
                get
                {
                    return _value;
                }
            }

            public override int GetHashCode()
            {
                return Hash.Combine(base.GetHashCode(), _value.GetHashCode());
            }

            public override bool Equals(ConstantValue other)
            {
                return base.Equals(other) && _value == other.DecimalValue;
            }
            public override bool IsDefaultValue => Equals(default(decimal));
        }

        private sealed class ConstantValueDateTime : ConstantValue
        {
            private readonly DateTime _value;

            public ConstantValueDateTime(DateTime value)
            {
                _value = value;
            }

            public override ConstantValueTypeDiscriminator Discriminator
            {
                get
                {
                    return ConstantValueTypeDiscriminator.DateTime;
                }
            }

            internal override SpecialType SpecialType
            {
                get { return SpecialType.System_DateTime; }
            }

            public override DateTime DateTimeValue
            {
                get
                {
                    return _value;
                }
            }

            public override int GetHashCode()
            {
                return Hash.Combine(base.GetHashCode(), _value.GetHashCode());
            }

            public override bool Equals(ConstantValue other)
            {
                return base.Equals(other) && _value == other.DateTimeValue;
            }

            public override bool IsDefaultValue => Equals(default(DateTime));
        }

        // default value of a value type constant. (reference type constants use Null as default)
        private static class ConstantValueDefault
        {
            public static readonly ConstantValue SByte = new ConstantValueS8(0);
            public static readonly ConstantValue Byte = new ConstantValueU8(0);
            public static readonly ConstantValue Int16 = new ConstantValueS16(0);
            public static readonly ConstantValue UInt16 = new ConstantValueU16(0);
            public static readonly ConstantValue Int32 = new ConstantValueS32(0);
            public static readonly ConstantValue UInt32 = new ConstantValueU32(0);
            public static readonly ConstantValue Int64 = new ConstantValueS64(0);
            public static readonly ConstantValue UInt64 = new ConstantValueU64(0);
            public static readonly ConstantValue Single = new ConstantValueSingle(0);
            public static readonly ConstantValue Double = new ConstantValueDouble(0);
            public static readonly ConstantValue Decimal = new ConstantValueDecimal(default(decimal));
            public static readonly ConstantValue Boolean = new ConstantValueBool(false);
            public static readonly ConstantValue Char = new ConstantValueChar(default(char));
            public static readonly ConstantValue DateTime = new ConstantValueDateTime(default(DateTime));
        }

        private static class ConstantValueOne
        {
            public static readonly ConstantValue SByte = new ConstantValueS8(1);
            public static readonly ConstantValue Byte = new ConstantValueU8(1);
            public static readonly ConstantValue Int16 = new ConstantValueS16(1);
            public static readonly ConstantValue UInt16 = new ConstantValueU16(1);
            public static readonly ConstantValue Int32 = new ConstantValueS32(1);
            public static readonly ConstantValue UInt32 = new ConstantValueU32(1);
            public static readonly ConstantValue Int64 = new ConstantValueS64(1);
            public static readonly ConstantValue UInt64 = new ConstantValueU64(1);
            public static readonly ConstantValue Single = new ConstantValueSingle(1.0f);
            public static readonly ConstantValue Double = new ConstantValueDouble(1.0);
            public static readonly ConstantValue Decimal = new ConstantValueDecimal(1);
            public static readonly ConstantValue Boolean = new ConstantValueBool(true);
        }

        private sealed class ConstantValueBool : ConstantValue
        {
            private readonly bool _value;
            public ConstantValueBool(bool value) => _value = value;
            public override bool BooleanValue => base.BooleanValue;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Boolean;
            internal override SpecialType SpecialType => SpecialType.System_Boolean;
            public override bool IsDefaultValue => _value == false;
        }

        private sealed class ConstantValueS8 : ConstantValue
        {
            private readonly sbyte _value;
            public ConstantValueS8(sbyte value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.SByte;
            internal override SpecialType SpecialType => SpecialType.System_SByte;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueU8 : ConstantValue
        {
            private readonly byte _value;
            public ConstantValueU8(byte value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Byte;
            internal override SpecialType SpecialType => SpecialType.System_Byte;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueS16 : ConstantValue
        {
            private readonly short _value;
            public ConstantValueS16(short value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Int16;
            internal override SpecialType SpecialType => SpecialType.System_Int16;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueU16 : ConstantValue
        {
            private readonly ushort _value;
            public ConstantValueU16(ushort value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.UInt16;
            internal override SpecialType SpecialType => SpecialType.System_UInt16;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueS32 : ConstantValue
        {
            private readonly int _value;
            public ConstantValueS32(int value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Int32;
            internal override SpecialType SpecialType => SpecialType.System_Int32;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueU32 : ConstantValue
        {
            private readonly uint _value;
            public ConstantValueU32(uint value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.UInt32;
            internal override SpecialType SpecialType => SpecialType.System_UInt32;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueS64 : ConstantValue
        {
            private readonly long _value;
            public ConstantValueS64(long value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Int64;
            internal override SpecialType SpecialType => SpecialType.System_Int64;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueU64 : ConstantValue
        {
            private readonly ulong _value;
            public ConstantValueU64(ulong value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.UInt64;
            internal override SpecialType SpecialType => SpecialType.System_UInt64;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == 0;
        }

        private sealed class ConstantValueChar : ConstantValue
        {
            private readonly char _value;
            public ConstantValueChar(char value) => _value = value;
            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Char;
            internal override SpecialType SpecialType => SpecialType.System_Char;
            public override byte ByteValue => unchecked((byte)_value);
            public override sbyte SByteValue => unchecked((sbyte)_value);
            public override char CharValue => unchecked((char)_value);
            public override short Int16Value => unchecked((short)_value);
            public override ushort UInt16Value => unchecked((ushort)_value);
            public override int Int32Value => unchecked((int)_value);
            public override uint UInt32Value => unchecked((uint)_value);
            public override long Int64Value => unchecked((long)_value);
            public override ulong UInt64Value => unchecked((ulong)_value);
            public override bool IsDefaultValue => _value == default(char);
        }

        private sealed class ConstantValueDouble : ConstantValue
        {
            private readonly double _value;

            public ConstantValueDouble(double value) => _value = value;

            public override double DoubleValue => _value;

            public override float SingleValue => (float)_value;

            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Double;

            internal override SpecialType SpecialType => SpecialType.System_Double;

            public override int GetHashCode()
            {
                return Hash.Combine(base.GetHashCode(), _value.GetHashCode());
            }

            public override bool Equals(ConstantValue other)
            {
                return base.Equals(other) && _value.Equals(other.DoubleValue);
            }
            public override bool IsDefaultValue => Equals(0.0);
        }

        private sealed class ConstantValueSingle : ConstantValue
        {
            private readonly float _value;

            public ConstantValueSingle(float value) => _value = value;

            public override double DoubleValue => _value;

            public override float SingleValue => _value;

            public override ConstantValueTypeDiscriminator Discriminator => ConstantValueTypeDiscriminator.Single;

            internal override SpecialType SpecialType => throw new NotImplementedException();

            public override int GetHashCode()
            {
                return Hash.Combine(base.GetHashCode(), _value.GetHashCode());
            }

            public override bool Equals(ConstantValue other)
            {
                return base.Equals(other) && _value.Equals(other.DoubleValue);
            }
            public override bool IsDefaultValue => Equals(0.0f);
        }
    }
}
