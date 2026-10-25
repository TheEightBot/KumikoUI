using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Benchmarks;

[KumikoGridItem]
public class GeneratedRow
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public class ReflectedRow
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

/// <summary>
/// Reads one boxed int column for every row through <see cref="KumikoSource.GetCellValue"/>, the call the
/// renderer makes per visible cell, and sorts by it. "Pre17" replays the accessor lookup KumikoSource used
/// before #17 (a "{type}.{path}" string key into a dictionary of compiled expression trees).
/// </summary>
[MemoryDiagnoser]
public class PropertyAccessBenchmarks
{
    private const int Rows = 10_000;

    private KumikoSource _generated = null!;
    private KumikoSource _delegate = null!;
    private KumikoSource _reflection = null!;
    private KumikoColumn _generatedAge = null!;
    private KumikoColumn _delegateAge = null!;
    private KumikoColumn _reflectionAge = null!;
    private List<object> _pre17Items = null!;
    private readonly Dictionary<string, Func<object, object?>> _pre17Accessors = new();

    [GlobalSetup]
    public void Setup()
    {
        var generated = Enumerable.Range(0, Rows).Select(i => new GeneratedRow { Name = $"R{i}", Age = Rows - i }).ToList();
        var reflected = Enumerable.Range(0, Rows).Select(i => new ReflectedRow { Name = $"R{i}", Age = Rows - i }).ToList();

        _generatedAge = new KumikoColumn { PropertyName = "Age" };
        _generated = new KumikoSource();
        _generated.SetColumns(new[] { _generatedAge });
        _generated.SetItems(generated);

        _delegateAge = new KumikoColumn { PropertyName = "Age", ValueAccessor = static o => ((GeneratedRow)o).Age };
        _delegate = new KumikoSource();
        _delegate.SetColumns(new[] { _delegateAge });
        _delegate.SetItems(generated);

        _reflectionAge = new KumikoColumn { PropertyName = "Age" };
        _reflection = new KumikoSource();
        _reflection.SetColumns(new[] { _reflectionAge });
        _reflection.SetItems(reflected);

        _pre17Items = reflected.Cast<object>().ToList();
    }

    [Benchmark(Baseline = true)]
    public long Read_Pre17CompiledExpression()
    {
        long sum = 0;
        foreach (var item in _pre17Items)
        {
            var key = $"{item.GetType().FullName}.Age";
            if (!_pre17Accessors.TryGetValue(key, out var accessor))
                _pre17Accessors[key] = accessor = Compile(item.GetType(), "Age");
            sum += (int)accessor(item)!;
        }
        return sum;
    }

    [Benchmark]
    public long Read_Generated() => Sum(_generated, _generatedAge);

    [Benchmark]
    public long Read_ValueAccessor() => Sum(_delegate, _delegateAge);

    [Benchmark]
    public long Read_ReflectionFallback() => Sum(_reflection, _reflectionAge);

    [Benchmark]
    public void Sort_Generated()
    {
        _generated.ToggleSort(_generatedAge);
        _generated.ToggleSort(_generatedAge);
    }

    [Benchmark]
    public void Sort_ReflectionFallback()
    {
        _reflection.ToggleSort(_reflectionAge);
        _reflection.ToggleSort(_reflectionAge);
    }

    private static long Sum(KumikoSource source, KumikoColumn column)
    {
        long sum = 0;
        for (int row = 0; row < Rows; row++)
            sum += (int)source.GetCellValue(row, column)!;
        return sum;
    }

    private static Func<object, object?> Compile(Type type, string path)
    {
        var param = Expression.Parameter(typeof(object), "item");
        Expression body = Expression.Convert(param, type);
        foreach (var part in path.Split('.'))
            body = Expression.Property(body, type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!);
        if (body.Type.IsValueType) body = Expression.Convert(body, typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, param).Compile();
    }
}
