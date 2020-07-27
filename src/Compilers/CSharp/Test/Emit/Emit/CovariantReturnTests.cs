// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETCOREAPP

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Emit
{
    public class CovariantReturnTests : EmitMetadataTestBase
    {
        private static MetadataReference _corelibraryWithCovariantReturnSupport;
        private static MetadataReference CorelibraryWithCovariantReturnSupport
        {
            get
            {
                if (_corelibraryWithCovariantReturnSupport == null)
                {
                    _corelibraryWithCovariantReturnSupport = MakeCoreLibrary();
                }

                return _corelibraryWithCovariantReturnSupport;
            }
        }

        private static MetadataReference MakeCoreLibrary()
        {
            const string corLibraryCore = @"
namespace System
{
    public class Array
    {
        public static T[] Empty<T>() => throw null;
    }
    public class Console
    {
        public static void WriteLine(string message) => throw null;
    }
    public class Attribute { }
    [Flags]
    public enum AttributeTargets
    {
        Assembly = 0x1,
        Module = 0x2,
        Class = 0x4,
        Struct = 0x8,
        Enum = 0x10,
        Constructor = 0x20,
        Method = 0x40,
        Property = 0x80,
        Field = 0x100,
        Event = 0x200,
        Interface = 0x400,
        Parameter = 0x800,
        Delegate = 0x1000,
        ReturnValue = 0x2000,
        GenericParameter = 0x4000,
        All = 0x7FFF
    }
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets validOn) { }
        public bool AllowMultiple
        {
            get => throw null;
            set { }
        }
        public bool Inherited
        {
            get => throw null;
            set { }
        }
        public AttributeTargets ValidOn => throw null;
    }
    public struct Boolean { }
    public struct Byte { }
    public class Delegate
    {
        public static Delegate CreateDelegate(Type type, object firstArgument, Reflection.MethodInfo method) => null;
    }
    public abstract class Enum : IComparable { }
    public class Exception
    {
        public Exception(string message) => throw null;
    }
    public class FlagsAttribute : Attribute { }
    public delegate T Func<out T>();
    public delegate U Func<in T, out U>(T arg);
    public interface IComparable { }
    public interface IDisposable
    {
        void Dispose();
    }
    public struct Int16 { }
    public struct Int32 { }
    public struct IntPtr { }
    public class MulticastDelegate : Delegate { }
    public struct Nullable<T> { }
    public class Object
    {
        public virtual string ToString() => throw null;
        public virtual int GetHashCode() => throw null;
        public virtual bool Equals(object other) => throw null;
    }
    public sealed class ParamArrayAttribute : Attribute { }
    public struct RuntimeMethodHandle { }
    public struct RuntimeTypeHandle { }
    public class String : IComparable { 
        public static String Empty = null;
        public override string ToString() => throw null;
        public static bool operator ==(string a, string b) => throw null;
        public static bool operator !=(string a, string b) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
    }
    public class Type
    {
        public Reflection.FieldInfo GetField(string name) => null;
        public static Type GetType(string name) => null;
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => null;
    }
    public class ValueType { }
    public struct Void { }

    namespace Collections
    {
        public interface IEnumerable
        {
            IEnumerator GetEnumerator();
        }
        public interface IEnumerator
        {
            object Current
            {
                get;
            }
            bool MoveNext();
            void Reset();
        }
    }
    namespace Collections.Generic
    {
        public interface IEnumerable<out T> : IEnumerable
        {
            new IEnumerator<T> GetEnumerator();
        }
        public interface IEnumerator<out T> : IEnumerator, IDisposable
        {
            new T Current
            {
                get;
            }
        }
    }
    namespace Linq.Expressions
    {
        public class Expression
        {
            public static ParameterExpression Parameter(Type type) => throw null;
            public static ParameterExpression Parameter(Type type, string name) => throw null;
            public static MethodCallExpression Call(Expression instance, Reflection.MethodInfo method, params Expression[] arguments) => throw null;
            public static Expression<TDelegate> Lambda<TDelegate>(Expression body, params ParameterExpression[] parameters) => throw null;
            public static MemberExpression Property(Expression expression, Reflection.MethodInfo propertyAccessor) => throw null;
            public static ConstantExpression Constant(object value, Type type) => throw null;
            public static UnaryExpression Convert(Expression expression, Type type) => throw null;
        }
        public class ParameterExpression : Expression { }
        public class MethodCallExpression : Expression { }
        public abstract class LambdaExpression : Expression { }
        public class Expression<T> : LambdaExpression { }
        public class MemberExpression : Expression { }
        public class ConstantExpression : Expression { }
        public sealed class UnaryExpression : Expression { }
    }
    namespace Reflection
    {
        public class AssemblyVersionAttribute : Attribute
        {
            public AssemblyVersionAttribute(string version) { }
        }
        public class DefaultMemberAttribute : Attribute
        {
            public DefaultMemberAttribute(string name) { }
        }
        public abstract class MemberInfo { }
        public abstract class MethodBase : MemberInfo
        {
            public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle) => throw null;
        }
        public abstract class MethodInfo : MethodBase
        {
            public virtual Delegate CreateDelegate(Type delegateType, object target) => throw null;
        }
        public abstract class FieldInfo : MemberInfo
        {
            public abstract object GetValue(object obj);
        }
    }
    namespace Runtime.CompilerServices
    {
        public static class RuntimeHelpers
        {
            public static object GetObjectValue(object obj) => null;
        }
    }
}
";
            const string corlibWithCovariantSupport = corLibraryCore + @"
namespace System.Runtime.CompilerServices
{
    public static class RuntimeFeature
    {
        public const string CovariantReturnsOfClasses = nameof(CovariantReturnsOfClasses);
        public const string DefaultImplementationsOfInterfaces = nameof(DefaultImplementationsOfInterfaces);
    }
    public sealed class PreserveBaseOverridesAttribute : Attribute { }
}
";
            var compilation = CreateEmptyCompilation(new string[] {
                corlibWithCovariantSupport,
                @"[assembly: System.Reflection.AssemblyVersion(""4.0.0.0"")]"
            }, assemblyName: "mscorlib");
            compilation.VerifyDiagnostics();
            return compilation.EmitToImageReference(options: new CodeAnalysis.Emit.EmitOptions(runtimeMetadataVersion: "v5.1"));
        }

        private static CSharpCompilation CreateCovariantCompilation(
            string source,
            CSharpCompilationOptions options = null,
            IEnumerable<MetadataReference> references = null,
            string assemblyName = null)
        {
            Assert.NotNull(CorelibraryWithCovariantReturnSupport);
            references = (references == null) ?
                new[] { CorelibraryWithCovariantReturnSupport } :
                references.ToArray().Prepend(CorelibraryWithCovariantReturnSupport);
            return CreateEmptyCompilation(
                source,
                options: options,
                parseOptions: TestOptions.WithCovariantReturns,
                references: references,
                assemblyName: assemblyName);
        }

        [ConditionalFact(typeof(CovariantReturnRuntimeOnly))]
        public void SimpleCovariantReturnEndToEndTest_01()
        {
            var source = @"
using System;
class Base
{
    public virtual object M() => ""Base.M"";
}
class Derived : Base
{
    public override string M() => ""Derived.M"";
}
class Program
{
    static void Main()
    {
        Derived d = new Derived();
        Base b = d;
        string s = d.M();
        object o = b.M();
        Console.WriteLine(s.ToString());
        Console.WriteLine(o.ToString());
    }
}
";
            var compilation = CreateCovariantCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput =
@"Derived.M
Derived.M";
            CompileAndVerify(compilation, expectedOutput: expectedOutput, verify: Verification.Skipped);
        }

        [ConditionalFact(typeof(CovariantReturnRuntimeOnly))]
        public void SimpleCovariantReturnEndToEndTest_02()
        {
            var source = @"
using System;
class Base
{
    public virtual object P => ""Base.P"";
}
class Derived : Base
{
    public override string P => ""Derived.P"";
}
class Program
{
    static void Main()
    {
        Derived d = new Derived();
        Base b = d;
        string s = d.P;
        object o = b.P;
        Console.WriteLine(s.ToString());
        Console.WriteLine(o.ToString());
    }
}
";
            var compilation = CreateCovariantCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput =
@"Derived.P
Derived.P";
            CompileAndVerify(compilation, expectedOutput: expectedOutput, verify: Verification.Skipped);
        }

        [ConditionalFact(typeof(CovariantReturnRuntimeOnly))]
        public void BinaryCompatibility_PreserveBaseOverride_01()
        {
            var sourceA = @"
public class A
{
    public virtual object P => ""A.P"";
}";
            var sourceB1 = @"
public class B : A
{
}";
            var sourceB2 = @"
public class B : A
{
    public override string P => ""B.P"";
}";
            var sourceC = @"
public class C : B
{
    public override string P => ""C.P"";
}";
            var sourceMain = @"
using System;
public class Program0
{
    static void Main()
    {
        C c = new C();
        B b = c;
        A a = b;
        Console.WriteLine(a.P.ToString());
        Console.WriteLine(b.P);
        Console.WriteLine(c.P);
    }
}";
            var compA = CreateCovariantCompilation(sourceA, options: TestOptions.DebugDll, assemblyName: "A");
            compA.VerifyDiagnostics();
            var imageA = compA.EmitToImageReference();

            var compB1 = CreateCovariantCompilation(sourceB1, options: TestOptions.DebugDll, references: new[] { imageA }, assemblyName: "B");
            compB1.VerifyDiagnostics();
            var imageB1 = compB1.EmitToImageReference();

            var compB2 = CreateCovariantCompilation(sourceB2, options: TestOptions.DebugDll, references: new[] { imageA }, assemblyName: "B");
            compB2.VerifyDiagnostics();
            var imageB2 = compB2.EmitToImageReference();

            var compC = CreateCovariantCompilation(sourceC, options: TestOptions.DebugDll, references: new[] { imageA, imageB1 }, assemblyName: "C");
            compC.VerifyDiagnostics();
            var imageC = compC.EmitToImageReference();

            var expectedOutput =
@"C.P
C.P
C.P";
            // The point of this test is that B.P is seen by the runtime as being overridden by C.P
            var compMain = CreateCovariantCompilation(sourceMain, options: TestOptions.DebugExe, references: new[] { imageA, imageB2, imageC }, assemblyName: "Main").VerifyDiagnostics();
            CompileAndVerify(compMain, expectedOutput: expectedOutput);
        }

        [ConditionalFact(typeof(CovariantReturnRuntimeOnly))]
        public void BinaryCompatibility_PreserveBaseOverride_02()
        {
            var sourceA = @"
public class A
{
    public virtual object M() => ""A.M"";
}";
            var sourceB1 = @"
public class B : A
{
}";
            var sourceB2 = @"
public class B : A
{
    public override string M() => ""B.M"";
}";
            var sourceC = @"
public class C : B
{
    public override string M() => ""C.M"";
}";
            var sourceMain = @"
using System;
public class Program0
{
    static void Main()
    {
        C c = new C();
        B b = c;
        A a = b;
        Console.WriteLine(a.M().ToString());
        Console.WriteLine(b.M());
        Console.WriteLine(c.M());
    }
}";
            var compA = CreateCovariantCompilation(sourceA, options: TestOptions.DebugDll, assemblyName: "A");
            compA.VerifyDiagnostics();
            var imageA = compA.EmitToImageReference();

            var compB1 = CreateCovariantCompilation(sourceB1, options: TestOptions.DebugDll, references: new[] { imageA }, assemblyName: "B");
            compB1.VerifyDiagnostics();
            var imageB1 = compB1.EmitToImageReference();

            var compB2 = CreateCovariantCompilation(sourceB2, options: TestOptions.DebugDll, references: new[] { imageA }, assemblyName: "B");
            compB2.VerifyDiagnostics();
            var imageB2 = compB2.EmitToImageReference();

            var compC = CreateCovariantCompilation(sourceC, options: TestOptions.DebugDll, references: new[] { imageA, imageB1 }, assemblyName: "C");
            compC.VerifyDiagnostics();
            var imageC = compC.EmitToImageReference();

            var expectedOutput =
@"C.M
C.M
C.M";
            // The point of this test is that B.M is seen by the runtime as being overridden by C.M
            var compMain = CreateCovariantCompilation(sourceMain, options: TestOptions.DebugExe, references: new[] { imageA, imageB2, imageC }, assemblyName: "Main").VerifyDiagnostics(
                //// There should be no errors; See https://github.com/dotnet/roslyn/issues/45798
                //// (12,29): error CS0121: The call is ambiguous between the following methods or properties: 'A.M()' and 'A.M()'
                ////         Console.WriteLine(c.M());
                //Diagnostic(ErrorCode.ERR_AmbigCall, "M").WithArguments("A.M()", "A.M()").WithLocation(12, 29)
                );
            CompileAndVerify(compMain, expectedOutput: expectedOutput);
        }

        [ConditionalFact(typeof(CovariantReturnRuntimeOnly))]
        public void CovariantRuntimeHasRequiredMembers()
        {
            var source = @"
using System;
class Base
{
    public virtual object M() => ""Base.M"";
}
class Derived : Base
{
    public override string M() => ""Derived.M"";
}
class Program
{
    static void Main()
    {
        var value = (string)Type.GetType(""System.Runtime.CompilerServices.RuntimeFeature"").GetField(""CovariantReturnsOfClasses"").GetValue(null);
        if (value != ""CovariantReturnsOfClasses"")
            throw new Exception(value.ToString());

        var attr = Type.GetType(""System.Runtime.CompilerServices.PreserveBaseOverridesAttribute"");
        if (attr == null)
            throw new Exception(""missing System.Runtime.CompilerServices.PreserveBaseOverridesAttribute"");
    }
}
";
            var compilation = CreateCovariantCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"";
            CompileAndVerify(compilation, expectedOutput: expectedOutput, verify: Verification.Skipped);
        }
    }
}

#endif
