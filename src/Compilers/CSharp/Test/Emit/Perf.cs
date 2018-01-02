// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Emit
{
    public class Perf : CSharpTestBase
    {
        [Fact]
        public void Test()
        {
            // This test ensures that our perf benchmark code compiles without problems.
            // Benchmark code can be found in the following file under the 
            // "CompilerTestResources" project that is part of Roslyn.sln -
            //      $/Roslyn/Main/Open/Compilers/Test/Resources/Core/PerfTests/CSPerfTest.cs

            // You can also use VS's "Navigate To" feature to find the above file easily -
            // Just hit "Ctrl + ," and type "CSPerfTest.cs" in the dialog that pops up.

            // Please note that if this test fails, it is likely because of a bug in the
            // *product* and not in the *test* / *benchmark code* :)
            // The benchmark code has been verified to compile fine against Dev10.
            // So if the test fails we should fix the product bug that is causing the failure
            // as opposed to 'fixing' the test by updating the benchmark code.

            //GNAMBOO: Changing this code has implications for perf tests.
            CompileAndVerify(TestResources.PerfTests.CSPerfTest,
                             additionalRefs: new[] { SystemCoreRef }).
                             VerifyDiagnostics(
                                // (2416,9): info CS8019: Unnecessary using directive.
                                //         using nested;
                                Diagnostic(ErrorCode.HDN_UnusedUsingDirective, "using nested;"));
        }

        [Fact, WorkItem(18631, "https://github.com/dotnet/roslyn/issues/18631")]
        public void LargeNumberOfMembers()
        {
            // This test is for the performance of the compiler in the presence of a class
            // with a very large number of members.
            (string refString, string experimentString) makeExperiment(int count)
            {
                var refBuilder = new StringBuilder();
                var experimentBuilder = new StringBuilder();

                var l1 = "class foo { static int Main(string[] argv) { return 0; }";
                refBuilder.Append($"{l1} }}\n");
                experimentBuilder.Append($"{l1}\n");

                for (int i = 0; i < count; i++)
                {
                    var method = $"public int m{i}(byte arg) {{ return arg+{i}; }}";
                    refBuilder.Append($"class c{i} {{ {method} }}\n");
                    experimentBuilder.Append($"  {method}\n");
                }

                experimentBuilder.Append("}\n");
                return (refBuilder.ToString(), experimentBuilder.ToString());
            }

            const int N = 40000;
            var (reference, experiment) = makeExperiment(N);

            var refClock = Stopwatch.StartNew();
            var refComp = CreateStandardCompilation(reference, options: TestOptions.ReleaseDll.WithConcurrentBuild(true));
            refComp.VerifyDiagnostics();
            refClock.Stop();
            var refElapsed = refClock.ElapsedMilliseconds;

            var expClock = Stopwatch.StartNew();
            var expComp = CreateStandardCompilation(experiment, options: TestOptions.ReleaseDll.WithConcurrentBuild(true));
            expComp.VerifyDiagnostics();
            expClock.Stop();
            var expElapsed = expClock.ElapsedMilliseconds;

            Assert.True(refElapsed > expElapsed, $"refTime {refElapsed}; expTime {expElapsed}");
        }
    }
}
