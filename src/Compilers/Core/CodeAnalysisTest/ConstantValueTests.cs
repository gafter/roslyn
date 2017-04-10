using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class ConstantValueTests : TestBase
    {
        [Fact]
        public void TestByteConstant()
        {
            unchecked
            {
                void TestByteHelper(byte value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestByteHelper((byte)(-2));
                TestByteHelper((byte)(-1));
                TestByteHelper((byte)(0));
                TestByteHelper((byte)(1));
                TestByteHelper((byte)(2));
                TestByteHelper(byte.MinValue);
                TestByteHelper((byte)(byte.MinValue + 1));
                TestByteHelper((byte)(byte.MinValue + 2));
                TestByteHelper((byte)(byte.MaxValue / 2 - 2));
                TestByteHelper((byte)(byte.MaxValue / 2 - 1));
                TestByteHelper((byte)(byte.MaxValue / 2));
                TestByteHelper((byte)(byte.MaxValue / 2 + 1));
                TestByteHelper((byte)(byte.MaxValue / 2 + 2));
                TestByteHelper((byte)(byte.MaxValue - 2));
                TestByteHelper((byte)(byte.MaxValue - 1));
                TestByteHelper(byte.MaxValue);
            }
        }

        [Fact]
        public void TestSByteConstant()
        {
            unchecked
            {
                void TestSByteHelper(sbyte value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestSByteHelper((sbyte)(-2));
                TestSByteHelper((sbyte)(-1));
                TestSByteHelper((sbyte)(0));
                TestSByteHelper((sbyte)(1));
                TestSByteHelper((sbyte)(2));
                TestSByteHelper(sbyte.MinValue);
                TestSByteHelper((sbyte)(sbyte.MinValue + 1));
                TestSByteHelper((sbyte)(sbyte.MinValue + 2));
                TestSByteHelper((sbyte)(sbyte.MaxValue / 2 - 2));
                TestSByteHelper((sbyte)(sbyte.MaxValue / 2 - 1));
                TestSByteHelper((sbyte)(sbyte.MaxValue / 2));
                TestSByteHelper((sbyte)(sbyte.MaxValue / 2 + 1));
                TestSByteHelper((sbyte)(sbyte.MaxValue / 2 + 2));
                TestSByteHelper((sbyte)(sbyte.MaxValue - 2));
                TestSByteHelper((sbyte)(sbyte.MaxValue - 1));
                TestSByteHelper(sbyte.MaxValue);
            }
        }

        [Fact]
        public void TestCharConstant()
        {
            unchecked
            {
                void TestCharHelper(char value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestCharHelper((char)(-2));
                TestCharHelper((char)(-1));
                TestCharHelper((char)(0));
                TestCharHelper((char)(1));
                TestCharHelper((char)(2));
                TestCharHelper(char.MinValue);
                TestCharHelper((char)(char.MinValue + 1));
                TestCharHelper((char)(char.MinValue + 2));
                TestCharHelper((char)(char.MaxValue / 2 - 2));
                TestCharHelper((char)(char.MaxValue / 2 - 1));
                TestCharHelper((char)(char.MaxValue / 2));
                TestCharHelper((char)(char.MaxValue / 2 + 1));
                TestCharHelper((char)(char.MaxValue / 2 + 2));
                TestCharHelper((char)(char.MaxValue - 2));
                TestCharHelper((char)(char.MaxValue - 1));
                TestCharHelper(char.MaxValue);
            }
        }

        [Fact]
        public void TestInt16Constant()
        {
            unchecked
            {
                void TestInt16Helper(short value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestInt16Helper((short)(-2));
                TestInt16Helper((short)(-1));
                TestInt16Helper((short)(0));
                TestInt16Helper((short)(1));
                TestInt16Helper((short)(2));
                TestInt16Helper(short.MinValue);
                TestInt16Helper((short)(short.MinValue + 1));
                TestInt16Helper((short)(short.MinValue + 2));
                TestInt16Helper((short)(short.MaxValue / 2 - 2));
                TestInt16Helper((short)(short.MaxValue / 2 - 1));
                TestInt16Helper((short)(short.MaxValue / 2));
                TestInt16Helper((short)(short.MaxValue / 2 + 1));
                TestInt16Helper((short)(short.MaxValue / 2 + 2));
                TestInt16Helper((short)(short.MaxValue - 2));
                TestInt16Helper((short)(short.MaxValue - 1));
                TestInt16Helper(short.MaxValue);
            }
        }

        [Fact]
        public void TestUInt16Constant()
        {
            unchecked
            {
                void TestUInt16Helper(ushort value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestUInt16Helper((ushort)(-2));
                TestUInt16Helper((ushort)(-1));
                TestUInt16Helper((ushort)(0));
                TestUInt16Helper((ushort)(1));
                TestUInt16Helper((ushort)(2));
                TestUInt16Helper(ushort.MinValue);
                TestUInt16Helper((ushort)(ushort.MinValue + 1));
                TestUInt16Helper((ushort)(ushort.MinValue + 2));
                TestUInt16Helper((ushort)(ushort.MaxValue / 2 - 2));
                TestUInt16Helper((ushort)(ushort.MaxValue / 2 - 1));
                TestUInt16Helper((ushort)(ushort.MaxValue / 2));
                TestUInt16Helper((ushort)(ushort.MaxValue / 2 + 1));
                TestUInt16Helper((ushort)(ushort.MaxValue / 2 + 2));
                TestUInt16Helper((ushort)(ushort.MaxValue - 2));
                TestUInt16Helper((ushort)(ushort.MaxValue - 1));
                TestUInt16Helper(ushort.MaxValue);
            }
        }

        [Fact]
        public void TestInt32Constant()
        {
            unchecked
            {
                void TestInt32Helper(int value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestInt32Helper((int)(-2));
                TestInt32Helper((int)(-1));
                TestInt32Helper((int)(0));
                TestInt32Helper((int)(1));
                TestInt32Helper((int)(2));
                TestInt32Helper(int.MinValue);
                TestInt32Helper((int)(int.MinValue + 1));
                TestInt32Helper((int)(int.MinValue + 2));
                TestInt32Helper((int)(int.MaxValue / 2 - 2));
                TestInt32Helper((int)(int.MaxValue / 2 - 1));
                TestInt32Helper((int)(int.MaxValue / 2));
                TestInt32Helper((int)(int.MaxValue / 2 + 1));
                TestInt32Helper((int)(int.MaxValue / 2 + 2));
                TestInt32Helper((int)(int.MaxValue - 2));
                TestInt32Helper((int)(int.MaxValue - 1));
                TestInt32Helper(int.MaxValue);
            }
        }

        [Fact]
        public void TestUInt32Constant()
        {
            unchecked
            {
                void TestUInt32Helper(uint value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestUInt32Helper((uint)(-2));
                TestUInt32Helper((uint)(-1));
                TestUInt32Helper((uint)(0));
                TestUInt32Helper((uint)(1));
                TestUInt32Helper((uint)(2));
                TestUInt32Helper(uint.MinValue);
                TestUInt32Helper((uint)(uint.MinValue + 1));
                TestUInt32Helper((uint)(uint.MinValue + 2));
                TestUInt32Helper((uint)(uint.MaxValue / 2 - 2));
                TestUInt32Helper((uint)(uint.MaxValue / 2 - 1));
                TestUInt32Helper((uint)(uint.MaxValue / 2));
                TestUInt32Helper((uint)(uint.MaxValue / 2 + 1));
                TestUInt32Helper((uint)(uint.MaxValue / 2 + 2));
                TestUInt32Helper((uint)(uint.MaxValue - 2));
                TestUInt32Helper((uint)(uint.MaxValue - 1));
                TestUInt32Helper(uint.MaxValue);
            }
        }

        [Fact]
        public void TestInt64Constant()
        {
            unchecked
            {
                void TestInt64Helper(long value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestInt64Helper((long)(-2));
                TestInt64Helper((long)(-1));
                TestInt64Helper((long)(0));
                TestInt64Helper((long)(1));
                TestInt64Helper((long)(2));
                TestInt64Helper(long.MinValue);
                TestInt64Helper((long)(long.MinValue + 1));
                TestInt64Helper((long)(long.MinValue + 2));
                TestInt64Helper((long)(long.MaxValue / 2 - 2));
                TestInt64Helper((long)(long.MaxValue / 2 - 1));
                TestInt64Helper((long)(long.MaxValue / 2));
                TestInt64Helper((long)(long.MaxValue / 2 + 1));
                TestInt64Helper((long)(long.MaxValue / 2 + 2));
                TestInt64Helper((long)(long.MaxValue - 2));
                TestInt64Helper((long)(long.MaxValue - 1));
                TestInt64Helper(long.MaxValue);
            }
        }

        [Fact]
        public void TestUInt64Constant()
        {
            unchecked
            {
                void TestUInt64Helper(ulong value)
                {
                    var k1 = ConstantValue.Create(value);
                    Assert.Equal((byte)value, k1.ByteValue);
                    Assert.Equal((sbyte)value, k1.SByteValue);
                    Assert.Equal((short)value, k1.Int16Value);
                    Assert.Equal((ushort)value, k1.UInt16Value);
                    Assert.Equal((int)value, k1.Int32Value);
                    Assert.Equal((uint)value, k1.UInt32Value);
                    Assert.Equal((long)value, k1.Int64Value);
                    Assert.Equal((ulong)value, k1.UInt64Value);
                }
                TestUInt64Helper((ulong)(-2));
                TestUInt64Helper((ulong)(-1));
                TestUInt64Helper((ulong)(0));
                TestUInt64Helper((ulong)(1));
                TestUInt64Helper((ulong)(2));
                TestUInt64Helper(ulong.MinValue);
                TestUInt64Helper((ulong)(ulong.MinValue + 1));
                TestUInt64Helper((ulong)(ulong.MinValue + 2));
                TestUInt64Helper((ulong)(ulong.MaxValue / 2 - 2));
                TestUInt64Helper((ulong)(ulong.MaxValue / 2 - 1));
                TestUInt64Helper((ulong)(ulong.MaxValue / 2));
                TestUInt64Helper((ulong)(ulong.MaxValue / 2 + 1));
                TestUInt64Helper((ulong)(ulong.MaxValue / 2 + 2));
                TestUInt64Helper((ulong)(ulong.MaxValue - 2));
                TestUInt64Helper((ulong)(ulong.MaxValue - 1));
                TestUInt64Helper(ulong.MaxValue);
            }
        }
    }
}
