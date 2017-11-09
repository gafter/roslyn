// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.Patterns)]
    public class PatternMatchingTests2 : PatternMatchingTestBase
    {
        [Fact]
        public void Patterns2_01()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Point p = new Point();
        Check(true, p is Point(3, 4) { Length: 5 } q && p == q);
        Check(false, p is Point(1, 4) { Length: 5 });
        Check(false, p is Point(3, 1) { Length: 5 });
        Check(false, p is Point(3, 4) { Length: 1 });
    }
    private static void Check(bool expected, bool actual)
    {
        if (expected != actual) throw new Exception($""expected: {expected}; actual: {actual}"");
    }
}
public class Point
{
    public void Deconstruct(out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
    public int Length => 5;
}
";
            var compilation = CreateCompilationWithMscorlib45(source, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularWithRecursivePatterns);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "True");
        }

        [Fact]
        public void Patterns2_02()
        {
            var source =
@"
class Program
{
    public static void Main()
    {
        Point p = new Point();
        switch (p)
        {
            case Point(3, 4) { Length: 5 }:
                System.Console.WriteLine(true);
                break;
            default:
                System.Console.WriteLine(false);
                break;
        }
    }
}
public class Point
{
    public void Deconstruct(out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
    public int Length => 5;
}
";
            var compilation = CreateCompilationWithMscorlib45(source, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularWithRecursivePatterns);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "True");
        }
    }
}
