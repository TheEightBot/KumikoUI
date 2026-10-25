// Fails when a public top-level type in the given KumikoUI assemblies has the same simple name as a public
// type in Microsoft.Maui.Controls.*, Microsoft.UI.Xaml.* or System.Windows.Forms.*.
//
//   dotnet run --project eng/TypeNameCheck -c Release -- <KumikoUI assembly>...
//
// Exit codes: 0 no collisions, 1 collisions found, 2 bad usage or an unreadable/unexpected platform package.
//
// The platform names are read straight from the metadata of the pinned packages (see the csproj), with
// System.Reflection.Metadata, so nothing is loaded or executed and no workload is needed.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var assemblyPaths = args;
if (assemblyPaths.Length == 0 || assemblyPaths.Any(a => a is "-h" or "--help"))
{
    Console.Error.WriteLine("usage: TypeNameCheck <KumikoUI assembly (.dll)>...");
    return 2;
}

try
{
    string mauiVersion = Metadata("MauiControlsVersion");
    string winUIVersion = Metadata("WindowsAppSdkWinUIVersion");
    string unoVersion = Metadata("UnoWinUIVersion");
    string desktopRefVersion = Metadata("WindowsDesktopRefVersion");

    // Each source lists the metadata files to read, the namespace prefix to keep, and a type that must be
    // present: if it is not, the package layout changed and the check would silently compare nothing.
    PlatformSource[] sources =
    [
        new("Microsoft.Maui.Controls", "Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Button",
            Package("microsoft.maui.controls.core", mauiVersion, "lib/net10.0", "Microsoft.Maui.Controls.dll")
                .Concat(Package("microsoft.maui.controls.xaml", mauiVersion, "lib/net10.0", "Microsoft.Maui.Controls.Xaml.dll"))),
        new("Microsoft.UI.Xaml", "Microsoft.UI.Xaml", "Microsoft.UI.Xaml.Controls.Button",
            Package("microsoft.windowsappsdk.winui", winUIVersion, "metadata", "*.winmd")),
        // Uno implements Microsoft.UI.Xaml itself and adds public types of its own (for example SelectionModel),
        // which a KumikoUI.Uno consumer sees as well.
        new("Microsoft.UI.Xaml (Uno)", "Microsoft.UI.Xaml", "Microsoft.UI.Xaml.Controls.Button",
            Package("uno.winui", unoVersion, "lib/net10.0", "Uno.UI.dll")),
        new("System.Windows.Forms", "System.Windows.Forms", "System.Windows.Forms.Button",
            Package("microsoft.windowsdesktop.app.ref", desktopRefVersion, "ref", "System.Windows.Forms*.dll")),
    ];

    // simple name -> full names, per platform
    var platformNames = new Dictionary<string, Dictionary<string, SortedSet<string>>>();
    foreach (var source in sources)
    {
        var names = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var type in source.Files.SelectMany(PublicTopLevelTypes)
                     .Where(t => InNamespace(t.Namespace, source.NamespacePrefix)))
            Add(names, type);

        if (!names.Values.Any(set => set.Contains(source.Sentinel)))
            throw new InvalidOperationException(
                $"Read {names.Count} names for {source.Label} but not the expected {source.Sentinel}; the package layout probably changed.");

        platformNames[source.Label] = names;
    }

    var kumikoTypes = new List<TypeName>();
    foreach (string path in assemblyPaths)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"KumikoUI assembly not found: {path}");

        kumikoTypes.AddRange(PublicTopLevelTypes(path));
    }

    if (kumikoTypes.Count == 0)
        throw new InvalidOperationException("No public types found in the given assemblies.");

    var collisions = new List<(TypeName Kumiko, List<string> Platform)>();
    foreach (var type in kumikoTypes.OrderBy(t => t.FullName, StringComparer.Ordinal))
    {
        var clashes = sources
            .Select(source => platformNames[source.Label].GetValueOrDefault(type.SimpleName))
            .Where(fullNames => fullNames is not null)
            .SelectMany(fullNames => fullNames!)
            .ToList();

        if (clashes.Count > 0)
            collisions.Add((type, clashes.Distinct(StringComparer.Ordinal).ToList()));
    }

    Console.WriteLine(
        $"Checked {kumikoTypes.Count} public KumikoUI types from {assemblyPaths.Length} assembl{(assemblyPaths.Length == 1 ? "y" : "ies")} " +
        "against " + string.Join(", ", sources.Select(s => $"{platformNames[s.Label].Count} {s.Label} names")) + ".");

    if (collisions.Count == 0)
    {
        Console.WriteLine("No public type name collides.");
        return 0;
    }

    bool github = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    Console.Error.WriteLine($"{collisions.Count} public type name collision(s):");
    foreach (var (kumiko, platform) in collisions)
    {
        string message = $"{kumiko.FullName} ({kumiko.Assembly}) has the same simple name as {string.Join(", ", platform)}";
        Console.Error.WriteLine("  " + message);
        if (github)
            Console.Error.WriteLine($"::error title=Public type name collision::{message}");
    }

    Console.Error.WriteLine(
        "Rename the KumikoUI type (DataGrid*, Drawn* or Grid* prefix); see CONTRIBUTING.md, \"Public type names\".");
    return 1;
}
catch (Exception ex) when (ex is IOException or InvalidOperationException or BadImageFormatException)
{
    Console.Error.WriteLine($"TypeNameCheck could not run: {ex.Message}");
    return 2;
}

string Metadata(string key) =>
    typeof(PlatformSource).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value
    is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Assembly metadata '{key}' is missing; build the project with its csproj.");

IEnumerable<string> Package(string id, string version, string folder, string pattern)
{
    // NuGet's global-packages layout: <root>/<lower-case id>/<version>/...
    string directory = Path.Join(Metadata("NuGetPackageRoot"), id, version.ToLowerInvariant(), folder);
    if (!Directory.Exists(directory))
        throw new DirectoryNotFoundException($"Package folder not found: {directory}. Restore eng/TypeNameCheck first.");

    var files = Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Design{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToList();
    if (files.Count == 0)
        throw new FileNotFoundException($"No {pattern} under {directory}.");
    return files;
}

static bool InNamespace(string ns, string prefix) =>
    ns.Equals(prefix, StringComparison.Ordinal) || ns.StartsWith(prefix + ".", StringComparison.Ordinal);

static void Add(Dictionary<string, SortedSet<string>> names, TypeName type)
{
    if (!names.TryGetValue(type.SimpleName, out var set))
        names[type.SimpleName] = set = new SortedSet<string>(StringComparer.Ordinal);
    set.Add(type.FullName);
}

// Public, non-nested types only: those are the names that can clash when a consumer imports both namespaces.
static IEnumerable<TypeName> PublicTopLevelTypes(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata)
        throw new BadImageFormatException($"{path} has no .NET metadata.");

    var reader = pe.GetMetadataReader();
    string assemblyName = Path.GetFileName(path);
    return reader.TypeDefinitions
        .Select(handle => reader.GetTypeDefinition(handle))
        .Where(definition => (definition.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
        .Select(definition =>
        {
            string name = reader.GetString(definition.Name);
            int arity = name.IndexOf('`');
            return new TypeName(reader.GetString(definition.Namespace), arity >= 0 ? name[..arity] : name, assemblyName);
        })
        .ToList();
}

internal sealed record PlatformSource(string Label, string NamespacePrefix, string Sentinel, IEnumerable<string> Files);

internal readonly record struct TypeName(string Namespace, string SimpleName, string Assembly)
{
    public string FullName => Namespace.Length == 0 ? SimpleName : Namespace + "." + SimpleName;
}
