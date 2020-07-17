// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    /// <summary>
    /// Test binding of the target-typed conditional (aka ternary) operator.
    /// </summary>
    public class TargetTypedConditionalOperatorTests : CSharpTestBase
    {
        [Fact]
        public void TestImplicitConversions_Good()
        {
            // Implicit constant expression conversions
            TestConditional("b ? 1 : 2", "System.Int16", "System.Int32");
            TestConditional("b ? -1L : 1UL", "System.Double", null);

            // Implicit reference conversions
            TestConditional("b ? GetB() : GetC()", "A", null);
            TestConditional("b ? Get<IOut<B>>() : Get<IOut<C>>()", "IOut<A>", null);
            TestConditional("b ? Get<IOut<IOut<B>>>() : Get<IOut<IOut<C>>>()", "IOut<IOut<A>>", null);
            TestConditional("b ? Get<IOut<B[]>>() : Get<IOut<C[]>>()", "IOut<A[]>", null);
            TestConditional("b ? Get<U>() : Get<V>()", "T", null);

            // Implicit numeric conversions
            TestConditional("b ? GetUInt() : GetInt()", "System.Int64", null);

            // Implicit enumeration conversions
            TestConditional("b ? 0 : 0", "color", "System.Int32");

            // Implicit interpolated string conversions
            TestConditional(@"b ? $""x"" : $""x""", "System.FormattableString", "System.String");

            // Implicit nullable conversions
            // Null literal conversions
            TestConditional("b ? 1 : null", "System.Int64?", null);

            // Boxing conversions
            TestConditional("b ? GetUInt() : GetInt()", "System.IComparable", null);

            // User - defined implicit conversions
            TestConditional("b ? GetB() : GetC()", "X", null);

            // Anonymous function conversions
            TestConditional("b ? a=>a : b=>b", "Del", null);

            // Method group conversions
            TestConditional("b ? M1 : M2", "Del", null);

            // Pointer conversions
            TestConditional("b ? GetIntp() : GetLongp()", "void*", null);
            TestConditional("b ? null : null", "System.Int32*", null);
        }

        [Fact]
        public void TestImplicitConversions_Bad()
        {
            // Implicit constant expression conversions
            TestConditional("b ? 1000000 : 2", "System.Int16", "System.Int32",
                // (6,30): error CS0029: Cannot implicitly convert type 'int' to 'short'
                //         System.Int16 t = b ? 1000000 : 2;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "1000000").WithArguments("int", "short").WithLocation(7, 30)
                );

            // Implicit reference conversions
            TestConditional("b ? GetB() : GetC()", "System.String", null,
                // (6,31): error CS0029: Cannot implicitly convert type 'B' to 'string'
                //         System.String t = b ? GetB() : GetC();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "GetB()").WithArguments("B", "string").WithLocation(7, 31),
                // (6,40): error CS0029: Cannot implicitly convert type 'C' to 'string'
                //         System.String t = b ? GetB() : GetC();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "GetC()").WithArguments("C", "string").WithLocation(7, 40)
                );

            // Implicit numeric conversions
            TestConditional("b ? GetUInt() : GetInt()", "System.UInt64", null,
                // (6,43): error CS0029: Cannot implicitly convert type 'int' to 'ulong'
                //         System.UInt64 t = b ? GetUInt() : GetInt();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "GetInt()").WithArguments("int", "ulong").WithLocation(7, 43)
                );

            // Implicit enumeration conversions
            TestConditional("b ? 1 : 0", "color", "System.Int32",
                // (6,23): error CS0029: Cannot implicitly convert type 'int' to 'color'
                //         color t = b ? 1 : 0;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "1").WithArguments("int", "color").WithLocation(7, 23)
                );

            // Implicit interpolated string conversions
            TestConditional(@"b ? $""x"" : ""x""", "System.FormattableString", "System.String",
                // (6,49): error CS0029: Cannot implicitly convert type 'string' to 'System.FormattableString'
                //         System.FormattableString t = b ? $"x" : "x";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""x""").WithArguments("string", "System.FormattableString").WithLocation(7, 49)
                );

            // Implicit nullable conversions
            // Null literal conversions
            TestConditional(@"b ? """" : null", "System.Int64?", "System.String?",
                // (6,31): error CS0029: Cannot implicitly convert type 'string' to 'long?'
                //         System.Int64? t = b ? "" : null;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""""").WithArguments("string", "long?").WithLocation(7, 31)
                );
            TestConditional(@"b ? 1 : """"", "System.Int64?", null,
                // (6,35): error CS0029: Cannot implicitly convert type 'string' to 'long?'
                //         System.Int64? t = b ? 1 : "";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""""").WithArguments("string", "long?").WithLocation(7, 35)
                );

            // Boxing conversions
            TestConditional("b ? GetUInt() : GetInt()", "System.Collections.IList", null,
                // (6,42): error CS0029: Cannot implicitly convert type 'uint' to 'System.Collections.IList'
                //         System.Collections.IList t = b ? GetUInt() : GetInt();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "GetUInt()").WithArguments("uint", "System.Collections.IList").WithLocation(7, 42),
                // (6,54): error CS0029: Cannot implicitly convert type 'int' to 'System.Collections.IList'
                //         System.Collections.IList t = b ? GetUInt() : GetInt();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "GetInt()").WithArguments("int", "System.Collections.IList").WithLocation(7, 54)
                );

            // User - defined implicit conversions
            TestConditional("b ? GetB() : GetD()", "X", null,
                // (6,28): error CS0619: 'D.implicit operator X(D)' is obsolete: 'D'
                //         X t = b ? GetB() : GetD();
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "GetD()").WithArguments("D.implicit operator X(D)", "D").WithLocation(7, 28)
                );

            // Anonymous function conversions
            TestConditional(@"b ? a=>a : b=>""""", "Del", null,
                // (6,31): error CS0029: Cannot implicitly convert type 'string' to 'int'
                //         Del t = b ? a=>a : b=>"";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""""").WithArguments("string", "int").WithLocation(7, 31),
                // (6,31): error CS1662: Cannot convert lambda expression to intended delegate type because some of the return types in the block are not implicitly convertible to the delegate return type
                //         Del t = b ? a=>a : b=>"";
                Diagnostic(ErrorCode.ERR_CantConvAnonMethReturns, @"""""").WithArguments("lambda expression").WithLocation(7, 31)
                );

            // Method group conversions
            TestConditional("b ? M1 : M3", "Del", null,
                // (6,26): error CS0123: No overload for 'M3' matches delegate 'Del'
                //         Del t = b ? M1 : M3;
                Diagnostic(ErrorCode.ERR_MethDelegateMismatch, "M3").WithArguments("M3", "Del").WithLocation(7, 26)
                );
        }

        [Fact]
        public void NonbreakingChange()
        {
            var source = @"
class C
{
    static void M(short x) => System.Console.WriteLine(""M(short)"");
    static void M(long l) => System.Console.WriteLine(""M(long)"");
    static void Main()
    {
        bool b = true;
        M(b ? 1 : 2); // should call M(long)
    }
}
";
            foreach (var langVersion in new[] { LanguageVersion.CSharp8, MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion() })
            {
                var comp = CreateCompilation(
                    source, options: TestOptions.ReleaseExe,
                    parseOptions: TestOptions.Regular.WithLanguageVersion(langVersion))
                    .VerifyDiagnostics();
                CompileAndVerify(comp, expectedOutput: "M(long)");
            }
        }

        [Fact]
        public void BreakingChange_02()
        {
            // Prior to C# 9.0, this program compiles without error, as only the overload M(long, long)
            // is a candidate. With the semantic changes in C# 9.0, both are candidates, but neither is
            // more specific.
            var source = @"
class C
{
    static void M(short x, short y) { }
    static void M(long x, long y) { }
    static void Main()
    {
        bool b = true;
        M(b ? 1 : 2, 1);
    }
}
";
            foreach (var langVersion in new[] { LanguageVersion.CSharp8, MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion() })
            {
                var comp = CreateCompilation(
                    source, options: TestOptions.ReleaseExe,
                    parseOptions: TestOptions.Regular.WithLanguageVersion(langVersion))
                    .VerifyDiagnostics(
                        // (9,9): error CS0121: The call is ambiguous between the following methods or properties: 'C.M(short, short)' and 'C.M(long, long)'
                        //         M(b ? 1 : 2, 1);
                        Diagnostic(ErrorCode.ERR_AmbigCall, "M").WithArguments("C.M(short, short)", "C.M(long, long)").WithLocation(9, 9)
                    );
            }
        }

        [Fact]
        public void NonBreakingChange_01()
        {
            var source = @"
class C
{
    static void Main()
    {
        bool b = true;
        _ = (short)(b ? 1 : 2);
    }
}
";
            foreach (var langVersion in new[] { LanguageVersion.CSharp8, MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion() })
            {
                var comp = CreateCompilation(
                    source, options: TestOptions.ReleaseExe,
                    parseOptions: TestOptions.Regular.WithLanguageVersion(langVersion))
                    .VerifyDiagnostics(
                    );
            }
        }

        [Fact]
        public void NonBreakingChange_02()
        {
            var source = @"
class Program
{
    static void Main()
    {
        M(true, new A(), new B());
    }
    static void M(bool x, A a, B b)
    {
        _ = (C)(x ? a : b);
    }
}
class A
{
    public static implicit operator B(A a) { System.Console.WriteLine(""A->B""); return new B(); }
    public static implicit operator C(A a) { System.Console.WriteLine(""A->C""); return new C(); }
}
class B
{
    public static implicit operator C(B b) { System.Console.WriteLine(""B->C""); return new C(); }
}
class C { }
";
            foreach (var langVersion in new[] { LanguageVersion.CSharp8, MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion() })
            {
                var comp = CreateCompilation(
                    source, options: TestOptions.ReleaseExe,
                    parseOptions: TestOptions.Regular.WithLanguageVersion(langVersion))
                    .VerifyDiagnostics(
                    );
                CompileAndVerify(comp, expectedOutput:
@"A->B
B->C");
            }
        }

        private static void TestConditional(string conditionalExpression, string targetType, string? naturalType, params DiagnosticDescription[] expectedDiagnostics)
        {
            TestConditional(conditionalExpression, targetType, naturalType, null, expectedDiagnostics);
        }

        private static void TestConditional(
            string conditionalExpression,
            string targetType,
            string? naturalType,
            CSharpParseOptions? parseOptions,
            params DiagnosticDescription[] expectedDiagnostics)
        {
            string source = $@"
#nullable enable
class Program
{{
    unsafe void Test<T, U, V>(bool b) where T : class where U : class, T where V : class, T
    {{
        {targetType} t = {conditionalExpression};
        Use(t);
    }}

    A GetA() {{ return null!; }}
    B GetB() {{ return null!; }}
    C GetC() {{ return null!; }}
    D GetD() {{ return null!; }}
    int GetInt() {{ return 1; }}
    uint GetUInt() {{ return 1; }}
    T Get<T>() where T : class {{ return null!; }}
    void Use(object? t) {{ }}
    unsafe void Use(void* t) {{ }}
    unsafe int* GetIntp() {{ return null!; }}
    unsafe long* GetLongp() {{ return null!; }}

    static int M1(int x) => x;
    static int M2(int x) => x;
    static int M3(int x, int y) => x;
}}

public enum color {{ Red, Blue, Green }};

class A {{ }}
class B : A {{ public static implicit operator X(B self) => new X(); }}
class C : A {{ public static implicit operator X(C self) => new X(); }}
class D : A {{ [System.Obsolete(""D"", true)] public static implicit operator X(D self) => new X(); }}

class X {{ }}

interface IOut<out T> {{ }}
interface IIn<in T> {{ }}

delegate int Del(int x);
";

            parseOptions ??= TestOptions.Regular.WithLanguageVersion(MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion());
            var tree = Parse(source, options: parseOptions);

            var comp = CreateCompilation(tree, options: TestOptions.DebugDll.WithAllowUnsafe(true));
            comp.VerifyDiagnostics(expectedDiagnostics);

            var compUnit = tree.GetCompilationUnitRoot();
            var classC = (TypeDeclarationSyntax)compUnit.Members.First();
            var methodTest = (MethodDeclarationSyntax)classC.Members.First();
            var stmt = (LocalDeclarationStatementSyntax)methodTest.Body!.Statements.First();
            var conditionalExpr = (ConditionalExpressionSyntax)stmt.Declaration.Variables[0].Initializer!.Value;

            var model = comp.GetSemanticModel(tree);

            if (naturalType is null)
            {
                var actualType = model.GetTypeInfo(conditionalExpr).Type;
                if (actualType is { })
                {
                    Assert.NotEmpty(expectedDiagnostics);
                    Assert.Equal("?", actualType.ToTestDisplayString(includeNonNullable: false));
                }
            }
            else
            {
                Assert.Equal(naturalType, model.GetTypeInfo(conditionalExpr).Type.ToTestDisplayString(includeNonNullable: false));
            }

            var convertedType = targetType switch { "void*" => "System.Void*", _ => targetType };
            Assert.Equal(convertedType, model.GetTypeInfo(conditionalExpr).ConvertedType.ToTestDisplayString(includeNonNullable: false));

            if (!expectedDiagnostics.Any())
            {
                Assert.Equal(SpecialType.System_Boolean, model.GetTypeInfo(conditionalExpr.Condition).Type!.SpecialType);
                Assert.Equal(convertedType, model.GetTypeInfo(conditionalExpr.WhenTrue).ConvertedType.ToTestDisplayString(includeNonNullable: false)); //in parent to catch conversion
                Assert.Equal(convertedType, model.GetTypeInfo(conditionalExpr.WhenFalse).ConvertedType.ToTestDisplayString(includeNonNullable: false)); //in parent to catch conversion
            }
        }

        [Fact, WorkItem(45460, "https://github.com/dotnet/roslyn/issues/45460")]
        public void TestConstantConditional()
        {
            var source = @"
using System;
public class Program {
    static void Main()
    {
        Test1();
        Test2();
    }

    public static void Test1() {
        const bool b = true;
        uint u1 = M1<uint>(b ? 1 : 0);
        Console.WriteLine(u1); // 1
        uint s1 = M2(b ? 2 : 3);
        Console.WriteLine(s1); // 2
        uint u2 = b ? 4 : 5;
        Console.WriteLine(u2); // 4

        static uint M2(uint t) => t;
    }
    public static void Test2() {
        const bool b = true;
        short s1 = M1<short>(b ? 1 : 0);
        Console.WriteLine(s1); // 1
        short s2 = M2(b ? 2 : 3);
        Console.WriteLine(s2); // 2
        short s3 = b ? 4 : 5;
        Console.WriteLine(s3); // 4

        static short M2(short t) => t;
    }
    public static T M1<T>(T t) => t;
}";
            var expectedOutput = @"
1
2
4
1
2
4";
            var comp = CreateCompilation(source, parseOptions: TestOptions.Regular8, options: TestOptions.DebugExe)
                .VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: expectedOutput);
            comp = CreateCompilation(source, parseOptions: TestOptions.Regular.WithLanguageVersion(MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion()), options: TestOptions.DebugExe)
                .VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: expectedOutput);
        }

        [Fact]
        public void TestLanguageVersion()
        {
            TestConditional("b ? 1 : 2", "System.Int16", "System.Int32");
            TestConditional("b ? 1 : 2", "System.Int16", "System.Int32", parseOptions: TestOptions.Regular8,
                // (6,26): error CS8652: The feature 'target-typed conditional expression' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         System.Int16 t = b ? 1 : 2;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "b ? 1 : 2").WithArguments("target-typed conditional expression").WithLocation(7, 26)
                );
        }

        [Fact, WorkItem(46063, "https://github.com/dotnet/roslyn/issues/46063")]
        public void TestNullableReferenceType_01()
        {
            TestConditional("b ? \"x\" : 2", "System.Object", null);
            TestConditional("b ? null : 2", "System.Object", null);
        }

        [Fact, WorkItem(46063, "https://github.com/dotnet/roslyn/issues/46063")]
        public void TestNullableReferenceType_02()
        {
            var source = @"
#nullable enable
class Program
{
    static void Main()
    {
        bool b = false;
        object o = b ? null : 2; // should be warning
    }
}
";
            CreateCompilation(source, parseOptions: TestOptions.Regular8.WithLanguageVersion(MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion()))
                .VerifyDiagnostics(
                );
        }

        [Fact]
        public void TestClassifyConversion_01()
        {
            var source = @"
#nullable disable
class Program
{
    static void Main()
    {
        bool x = true;
        B b = new B();
        C c = new C();
        A a = x ? b : c;
    }
}
class A { }
class B : A { }
class C : A { }
";
            var comp = CreateCompilation(source, parseOptions: TestOptions.Regular8.WithLanguageVersion(MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion()));
            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);
            var declarators = tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().ToArray();
            Assert.Equal(4, declarators.Length);

            var expression = declarators[3].Initializer!.Value;
            Assert.Equal("x ? b : c", expression.ToString());

            var location = expression.Location.SourceSpan.Start;

            var a = (ILocalSymbol)model.GetDeclaredSymbol(declarators[3])!; // a = ...
            var destination = a.Type;

            Conversion c = model.ClassifyConversion(location, expression, destination);
            Assert.Equal(ConversionKind.ConditionalExpression, c.Kind);
            Assert.Equal(2, c.UnderlyingConversions.Length);
            Assert.Equal(ConversionKind.ImplicitReference, c.UnderlyingConversions[0].Kind);
            Assert.Equal(ConversionKind.ImplicitReference, c.UnderlyingConversions[1].Kind);
        }

        [Fact]
        public void TestClassifyConversion_02()
        {
            var source = @"
#nullable disable
class Program
{
    static void Main()
    {
        System.IComparable a = """".Length==0 ? """" : 3;
    }
}
";
            var comp = CreateCompilation(source, parseOptions: TestOptions.Regular8.WithLanguageVersion(MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion()));
            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);
            var declarators = tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().ToArray();
            Assert.Equal(1, declarators.Length);

            var expression = declarators[0].Initializer!.Value;
            Assert.Equal(@""""".Length==0 ? """" : 3", expression.ToString());

            var location = expression.Location.SourceSpan.Start;

            var a = (ILocalSymbol)model.GetDeclaredSymbol(declarators[0])!; // a = ...
            var destination = a.Type;

            Conversion c = model.ClassifyConversion(location, expression, destination);
            Assert.Equal(ConversionKind.ConditionalExpression, c.Kind);
            Assert.Equal(2, c.UnderlyingConversions.Length);
            Assert.Equal(ConversionKind.ImplicitReference, c.UnderlyingConversions[0].Kind);
            Assert.Equal(ConversionKind.Boxing, c.UnderlyingConversions[1].Kind);

            c = model.ClassifyConversion(expression, destination);
            Assert.Equal(ConversionKind.Identity, c.Kind);

            // Should be as follows (see https://github.com/dotnet/roslyn/issues/46067)
            //Assert.Equal(ConversionKind.ConditionalExpression, c.Kind);
            //Assert.Equal(2, c.UnderlyingConversions.Length);
            //Assert.Equal(ConversionKind.ImplicitReference, c.UnderlyingConversions[0].Kind);
            //Assert.Equal(ConversionKind.Boxing, c.UnderlyingConversions[1].Kind);
        }

        [Fact]
        public void TestUserDefinedConversionChain()
        {
            var source = @"
#nullable disable
using System;
class Program
{
    static void Main()
    {
        bool flag = true;
        C c = flag ? new A() : new B();
    }
}
public class A
{
    public static implicit operator B(A a)
    {
        Console.Write(""[A->B]"");
        return new B();
    }
    public static implicit operator C(A a)
    {
        Console.Write(""[A->C]"");
        return new C();
    }
}
public class B
{
    public static implicit operator C(B b)
    {
        Console.Write(""[B->C]"");
        return new C();
    }
}
public class C
{
}
";
            var expectedOutput = @"[A->B][B->C]";
            foreach (var langVersion in new[] { LanguageVersion.CSharp8, MessageID.IDS_FeatureTargetTypedConditional.RequiredVersion() })
            {
                var comp = CreateCompilation(source, parseOptions: TestOptions.Regular8.WithLanguageVersion(langVersion), options: TestOptions.ReleaseExe);
                CompileAndVerify(comp, expectedOutput: expectedOutput);
            }
        }
    }
}
