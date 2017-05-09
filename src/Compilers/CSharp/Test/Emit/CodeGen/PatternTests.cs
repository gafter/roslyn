// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class PatternTests : EmitMetadataTestBase
    {
        [Fact, WorkItem(18811, "https://github.com/dotnet/roslyn/issues/18811")]
        public void MissingNullable_01()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
}
static class C {
    public static bool M() => ((object)123) is int i;
}
";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1)
                );
        }

        [Fact, WorkItem(18811, "https://github.com/dotnet/roslyn/issues/18811")]
        public void MissingNullable_02()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
    public struct Nullable<T> where T : struct { }
}
static class C {
    public static bool M() => ((object)123) is int i;
}
";
            var compilation = CreateCompilation(source, options: TestOptions.UnsafeReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion)
                );
        }

        [Fact, WorkItem(17266, "https://github.com/dotnet/roslyn/issues/17266")]
        public void DoubleEvaluation01()
        {
            var source =
@"using System;
public class C
{
    public static void Main()
    {
        if (TryGet() is int index)
        {
            Console.WriteLine(index);
        }
    }

    public static int? TryGet()
    {
        Console.WriteLine(""eval"");
        return null;
    }
}";
            var compilation = CreateStandardCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"eval";
            var compVerifier = CompileAndVerify(compilation, verify: false);
            compVerifier.VerifyIL("C.Main",
@"{
  // Code size       33 (0x21)
  .maxstack  2
  .locals init (int V_0, //index
                bool V_1,
                int? V_2)
  IL_0000:  nop
  IL_0001:  call       ""int? C.TryGet()""
  IL_0006:  stloc.2
  IL_0007:  ldloca.s   V_2
  IL_0009:  call       ""int int?.Value.get""
  IL_000e:  dup
  IL_000f:  stloc.0
  IL_0010:  ldnull
  IL_0011:  cgt.un
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  brfalse.s  IL_0020
  IL_0017:  nop
  IL_0018:  ldloc.0
  IL_0019:  call       ""void System.Console.WriteLine(int)""
  IL_001e:  nop
  IL_001f:  nop
  IL_0020:  ret
}");
            CompileAndVerify(compilation, expectedOutput: expectedOutput);
        }
    }
}
