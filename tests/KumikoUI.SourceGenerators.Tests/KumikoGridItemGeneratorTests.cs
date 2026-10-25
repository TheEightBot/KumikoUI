using System.Collections.Immutable;
using KumikoUI.Core.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KumikoUI.SourceGenerators.Tests;

public class KumikoGridItemGeneratorTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "mscorlib.dll" or "netstandard.dll" or "Microsoft.CSharp.dll")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(KumikoAccessorRegistry).Assembly.Location))
            .ToImmutableArray();

    private static (Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics, GeneratorDriverRunResult Run) Generate(string source)
    {
        var compilation = CSharpCompilation.Create("Tests",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new KumikoGridItemGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return (output, diagnostics, driver.GetRunResult());
    }

    private static string GeneratedFor(GeneratorDriverRunResult run, string typeName) =>
        run.Results.Single().GeneratedSources.Single(s => s.HintName.Contains(typeName, StringComparison.Ordinal)).SourceText.ToString();

    private static void AssertCompilesCleanly(Compilation output)
    {
        var problems = output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private const string Models = """
        using System;
        using System.Collections.Generic;
        using KumikoUI.Core.Data;

        namespace Demo;

        public class Base
        {
            public virtual string Label { get; set; } = "";
            public int Shared { get; set; }
            public int Hidden { get; set; }
        }

        [KumikoGridItem]
        public class Item : Base
        {
            public override string Label { get; set; } = "";
            public new string Hidden { get; set; } = "";
            public string? Nullable { get; set; }
            public int? MaybeNumber { get; set; }
            public List<string?> Tags { get; set; } = new();
            public (int A, string B) Pair { get; set; }
            public dynamic? Anything { get; set; }
            public string InitOnly { get; init; } = "";
            public string PrivateSet { get; private set; } = "";
            public string ProtectedGet { protected get; set; } = "";
            public string InternalSet { get; internal set; } = "";
            public string @class { get; set; } = "";
            public static string Static { get; set; } = "";
            public string this[int i] => "";
            [Obsolete("old")] public string Old { get; set; } = "";
            [Obsolete("gone", true)] public string Gone { get; set; } = "";
            internal string InternalProperty { get; set; } = "";
            private string Private { get; set; } = "";
        }

        [KumikoGridItem]
        public struct Point
        {
            public int X { get; set; }
            public readonly int Length => X;
        }

        [KumikoGridItem]
        public sealed record Line(string Code, int Quantity);

        internal static class Outer
        {
            [KumikoGridItem]
            internal class Nested
            {
                public string Value { get; set; } = "";
            }
        }
        """;

    [Fact]
    public void GeneratedCode_CompilesWithoutWarnings()
    {
        var (output, diagnostics, run) = Generate(Models);

        Assert.Empty(diagnostics);
        Assert.Equal(4, run.Results.Single().GeneratedSources.Length);
        AssertCompilesCleanly(output);
    }

    [Fact]
    public void PublicInstanceProperties_GetAccessors_ByUsableAccessor()
    {
        var code = GeneratedFor(Generate(Models).Run, "Demo_Item");

        foreach (var name in new[] { "Label", "Shared", "Hidden", "Nullable", "MaybeNumber", "Tags", "Pair", "Anything",
                     "InitOnly", "PrivateSet", "ProtectedGet", "InternalSet", "class", "Old" })
            Assert.Contains($"[\"{name}\"]", code);

        foreach (var name in new[] { "Static", "Gone", "InternalProperty", "Private", "Item" })
            Assert.DoesNotContain($"[\"{name}\"]", code);

        // Read-only for code outside the type: init-only and private setters.
        Assert.Contains("[\"InitOnly\"] = new global::KumikoUI.Core.Data.KumikoPropertyAccessor(\"InitOnly\", typeof(string),\n                    static item => ((global::Demo.Item)item).InitOnly,\n                    null)", Normalize(code));
        Assert.Contains("(\"PrivateSet\", typeof(string),\n                    static item => ((global::Demo.Item)item).PrivateSet,\n                    null)", Normalize(code));
        // Write-only for code outside the type: a protected getter.
        Assert.Contains("(\"ProtectedGet\", typeof(string),\n                    null,", Normalize(code));
        // Types are fully qualified, without nullable annotations; dynamic is object.
        Assert.Contains("typeof(global::System.Collections.Generic.List<string>)", code);
        Assert.Contains("typeof(int?)", code);
        Assert.Contains("(\"Anything\", typeof(object)", code);
        Assert.Contains("((global::Demo.Item)item).@class", code);
        // 'new' and override members resolve to the most-derived declaration.
        Assert.Contains("(\"Hidden\", typeof(string)", code);
    }

    [Fact]
    public void StructSetters_WriteThroughTheBox()
    {
        var code = GeneratedFor(Generate(Models).Run, "Demo_Point");

        Assert.Contains("global::System.Runtime.CompilerServices.Unsafe.Unbox<global::Demo.Point>(item).X = ", code);
        Assert.Contains("(\"Length\", typeof(int),\n                    static item => ((global::Demo.Point)item).Length,\n                    null)", Normalize(code));
    }

    [Fact]
    public void RegistersTheProviderAtModuleLoad()
    {
        var code = GeneratedFor(Generate(Models).Run, "Demo_Outer_Nested");

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", code);
        Assert.Contains("KumikoAccessorRegistry.Register(typeof(global::Demo.Outer.Nested), new __KumikoAccessors_Demo_Outer_Nested());", code);
    }

    [Theory]
    [InlineData("[KumikoGridItem] public class Box<T> { public T? Value { get; set; } }", "KUMIKO001")]
    [InlineData("public class Host { [KumikoGridItem] private class Secret { public int A { get; set; } } }", "KUMIKO002")]
    [InlineData("public class Host { [KumikoGridItem] protected class Secret { public int A { get; set; } } }", "KUMIKO002")]
    [InlineData("[KumikoGridItem] file class Local { public int A { get; set; } }", "KUMIKO002")]
    public void UnsupportedTypes_ReportADiagnostic_AndGenerateNothing(string declaration, string id)
    {
        var (output, diagnostics, run) = Generate("using KumikoUI.Core.Data;\nnamespace Demo;\n" + declaration);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Empty(run.Results.Single().GeneratedSources);
        AssertCompilesCleanly(output);
    }

    [Fact]
    public void UnrelatedEdits_ReuseCachedOutput()
    {
        var compilation = CSharpCompilation.Create("Tests",
            new[] { CSharpSyntaxTree.ParseText(Models, new CSharpParseOptions(LanguageVersion.Latest)) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new KumikoGridItemGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Demo; public class Unrelated { }",
            new CSharpParseOptions(LanguageVersion.Latest)));
        var outputs = driver.RunGenerators(edited).GetRunResult().Results.Single().TrackedOutputSteps
            .SelectMany(s => s.Value).SelectMany(s => s.Outputs);

        Assert.All(outputs, o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, o.Reason.ToString()));
    }

    private static string Normalize(string code) => code.Replace("\r\n", "\n");
}
