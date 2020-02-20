﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp
{
    using static BinaryOperatorKind;

    internal static partial class ValueSetFactory
    {
        private struct SByteTC : NumericTC<sbyte>
        {
            sbyte NumericTC<sbyte>.MinValue => sbyte.MinValue;

            sbyte NumericTC<sbyte>.MaxValue => sbyte.MaxValue;

            (sbyte leftMax, sbyte rightMin) NumericTC<sbyte>.Partition(sbyte min, sbyte max)
            {
                Debug.Assert(min < max);
                int half = (max - min) / 2;
                sbyte leftMax = (sbyte)(min + half);
                sbyte rightMin = (sbyte)(leftMax + 1);
                return (leftMax, rightMin);
            }

            bool NumericTC<sbyte>.Related(BinaryOperatorKind relation, sbyte left, sbyte right)
            {
                switch (relation)
                {
                    case Equal:
                        return left == right;
                    case GreaterThanOrEqual:
                        return left >= right;
                    case GreaterThan:
                        return left > right;
                    case LessThanOrEqual:
                        return left <= right;
                    case LessThan:
                        return left < right;
                    default:
                        throw new ArgumentException("relation");
                }
            }

            sbyte NumericTC<sbyte>.Next(sbyte value)
            {
                Debug.Assert(value != sbyte.MaxValue);
                return (sbyte)(value + 1);
            }

            sbyte EqualableValueTC<sbyte>.FromConstantValue(ConstantValue constantValue) => constantValue.SByteValue;

            string NumericTC<sbyte>.ToString(sbyte value) => value.ToString();
        }
    }
}
