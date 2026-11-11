using System.Text;
using System.Text.RegularExpressions;

// Extracts the fenced C# and XAML blocks of Markdown files into compilable files for the documentation snippet
// projects (tests/KumikoUI.Docs.Snippets.Maui and tests/KumikoUI.Docs.Snippets.Uno).
//
//   dotnet run --project eng/DocsSnippets -c Release [-- options]
//
//   --file <path-or-glob>   Markdown file to read, relative to the repository root. Repeatable. Replaces the
//                           default list (README.md and docs/nuget/*.md). A missing file or a glob with no
//                           match is skipped with a note.
//   --root <dir>            Repository root. Default: the nearest parent directory that has a global.json.
//   --maui-out <dir>        Output directory for the MAUI project. Default: tests/KumikoUI.Docs.Snippets.Maui/Generated
//   --uno-out <dir>         Output directory for the Uno project.  Default: tests/KumikoUI.Docs.Snippets.Uno/Generated
//
// Every ```csharp, ```cs, ```xml and ```xaml block is compiled for both hosts unless an HTML comment on the line
// before the fence says otherwise (invisible in the rendered Markdown):
//
//   <!-- snippet: maui -->                    compile for .NET MAUI only      (uno, both: the other choices)
//   <!-- snippet: maui file -->               host plus kind
//   <!-- snippet: shared -->                  file-level types that other snippets of the file use too
//   <!-- snippet: usings -->                  the standard `using` block the file's other C# snippets share
//   <!-- snippet: skip because a csproj -->   not compiled; everything after `skip` is the reason
//
// Kinds for C# (default: detected): `statements` (wrapped in a method), `file` (types in their own namespace),
// `members` (members of a class: `members=Base.Class.Name` gives it a base type). Blocks in other languages
// (shell, plain text) are ignored. Anything the tool cannot classify is an error, so nothing is skipped silently.
// See "Documentation snippets" in CONTRIBUTING.md.

return Run(args);

static int Run(string[] args)
{
    string? root = null, mauiOut = null, unoOut = null;
    var files = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
        switch (args[i])
        {
            case "--root": root = Next(); break;
            case "--file": files.Add(Next()); break;
            case "--maui-out": mauiOut = Next(); break;
            case "--uno-out": unoOut = Next(); break;
            default: Console.Error.WriteLine($"Unknown argument '{args[i]}'."); return 2;
        }
    }

    root = Path.GetFullPath(root ?? FindRoot());
    Emitter.Root = root;
    if (files.Count == 0) { files.Add("README.md"); files.Add("docs/nuget/*.md"); }
    mauiOut = Path.GetFullPath(mauiOut ?? Path.Combine(root, "tests", "KumikoUI.Docs.Snippets.Maui", "Generated"));
    unoOut = Path.GetFullPath(unoOut ?? Path.Combine(root, "tests", "KumikoUI.Docs.Snippets.Uno", "Generated"));

    var errors = new List<string>();
    var emitted = new Dictionary<Host, List<Output>> { [Host.Maui] = new(), [Host.Uno] = new() };
    var skipped = new List<string>();
    var sources = new List<string>();

    foreach (var pattern in files)
        foreach (var rel in Expand(root, pattern))
            sources.Add(rel);

    if (sources.Count == 0)
    {
        Console.Error.WriteLine("No Markdown file found.");
        return 1;
    }

    foreach (var rel in sources)
    {
        var lines = File.ReadAllLines(Path.Combine(root, rel));
        var blocks = Parser.Parse(rel, lines, errors);
        var usingsLines = blocks.Where(b => b.Kind == "usings").SelectMany(b => b.Code).ToList();

        foreach (var block in blocks)
        {
            if (block.Skip is not null) { skipped.Add($"{rel}:{block.FenceLine} ({block.Skip})"); continue; }
            foreach (var host in new[] { Host.Maui, Host.Uno })
            {
                try
                {
                    var output = block.IsXml ? Emitter.Xaml(block, host) : Emitter.CSharp(block, host, usingsLines);
                    if (output is not null) emitted[host].Add(output);
                }
                catch (SnippetException e)
                {
                    errors.Add($"{rel}:{block.FenceLine}: {e.Message} ({host})");
                }
            }
        }
    }

    if (errors.Count > 0)
    {
        foreach (var e in errors.Distinct()) Console.Error.WriteLine($"error: {e}");
        return 1;
    }

    foreach (var (host, dir) in new[] { (Host.Maui, mauiOut), (Host.Uno, unoOut) })
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        foreach (var o in emitted[host])
            File.WriteAllText(Path.Combine(dir, o.FileName), o.Text, new UTF8Encoding(false));
        Console.WriteLine($"{host,-4}: {emitted[host].Count(o => o.IsXaml ? false : !o.IsUsings)} C# + " +
                          $"{emitted[host].Count(o => o.IsXaml)} XAML snippets" +
                          $" + {emitted[host].Count(o => o.IsUsings)} using block(s) -> {Path.GetRelativePath(root, dir)}");
    }
    Console.WriteLine($"Skipped: {skipped.Count}" + (skipped.Count > 0 ? " - " + string.Join("; ", skipped) : ""));
    Console.WriteLine($"Sources: {string.Join(", ", sources)}");
    return 0;
}

static string FindRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
    throw new InvalidOperationException("No global.json found above the current directory; pass --root.");
}

static IEnumerable<string> Expand(string root, string pattern)
{
    var star = pattern.IndexOf('*');
    if (star < 0)
    {
        if (File.Exists(Path.Combine(root, pattern))) yield return pattern.Replace('\\', '/');
        else Console.WriteLine($"Note: {pattern} does not exist; skipped.");
        yield break;
    }
    var dirPart = Path.GetDirectoryName(pattern) ?? "";
    var dirFull = Path.Combine(root, dirPart);
    var any = false;
    if (Directory.Exists(dirFull))
        foreach (var f in Directory.EnumerateFiles(dirFull, Path.GetFileName(pattern)).Order(StringComparer.Ordinal))
        {
            any = true;
            yield return Path.GetRelativePath(root, f).Replace('\\', '/');
        }
    if (!any) Console.WriteLine($"Note: {pattern} matches nothing; skipped.");
}

internal enum Host { Maui, Uno }

internal sealed class SnippetException(string message) : Exception(message);

internal sealed record Output(string FileName, string Text, bool IsXaml, bool IsUsings);

internal sealed class Block
{
    public required string Source { get; init; }
    public required int FenceLine { get; init; }          // 1-based line of the opening fence
    public required int CodeLine { get; init; }           // 1-based line of the first code line
    public required string Lang { get; init; }
    public required List<string> Code { get; init; }      // dedented lines
    public string? Skip { get; init; }                    // reason when skipped
    public string? Hosts { get; init; }                   // maui | uno | null (both)
    public string? Kind { get; init; }                    // statements | file | members | usings | null (detect)
    public string? Base { get; init; }                    // members=<Base>
    public bool Shared { get; init; }                     // file-level types visible to every snippet
    public bool IsXml => Lang is "xml" or "xaml";
    public string Id => Regex.Replace(Source[..^3], "[^A-Za-z0-9]+", "_").Trim('_') + "_L" + FenceLine;
}

internal static class Parser
{
    private static readonly Regex Fence = new(@"^(?<indent>\s*)(?<ticks>`{3,})\s*(?<lang>[\w#+-]*)\s*$", RegexOptions.Compiled);
    private static readonly Regex Annotation = new(@"^\s*<!--\s*snippet:\s*(?<body>.*?)\s*-->\s*$", RegexOptions.Compiled);

    public static List<Block> Parse(string source, string[] lines, List<string> errors)
    {
        var blocks = new List<Block>();
        (int Line, string Body)? pending = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var fence = Fence.Match(line);
            if (!fence.Success)
            {
                var ann = Annotation.Match(line);
                if (ann.Success)
                {
                    if (pending is not null) errors.Add($"{source}:{i + 1}: a second snippet annotation before the same code block");
                    pending = (i + 1, ann.Groups["body"].Value);
                }
                else if (pending is not null && line.Trim().Length > 0)
                {
                    errors.Add($"{source}:{pending.Value.Line}: snippet annotation is not directly followed by a code block");
                    pending = null;
                }
                continue;
            }

            // Collect the block.
            var indent = fence.Groups["indent"].Value.Length;
            var ticks = fence.Groups["ticks"].Value.Length;
            var lang = fence.Groups["lang"].Value.ToLowerInvariant();
            var code = new List<string>();
            var j = i + 1;
            for (; j < lines.Length; j++)
            {
                var close = Fence.Match(lines[j]);
                if (close.Success && close.Groups["lang"].Value.Length == 0 && close.Groups["ticks"].Value.Length >= ticks) break;
                var l = lines[j];
                var strip = 0;
                while (strip < indent && strip < l.Length && l[strip] == ' ') strip++;
                code.Add(l[strip..]);
            }
            if (j >= lines.Length) errors.Add($"{source}:{i + 1}: code block is never closed");

            var normalized = lang switch { "csharp" or "cs" or "c#" => "csharp", "xml" or "xaml" => "xml", _ => "" };
            var ann2 = pending;
            pending = null;

            if (normalized.Length == 0)
            {
                if (ann2 is not null) errors.Add($"{source}:{ann2.Value.Line}: snippet annotation on a '{lang}' block (only csharp and xml blocks are compiled)");
            }
            else
            {
                blocks.Add(MakeBlock(source, i + 1, normalized, code, ann2, errors));
            }
            i = j;
        }

        if (pending is not null) errors.Add($"{source}:{pending.Value.Line}: snippet annotation at the end of the file has no code block");
        return blocks;
    }

    private static Block MakeBlock(string source, int fenceLine, string lang, List<string> code, (int Line, string Body)? ann, List<string> errors)
    {
        string? skip = null, hosts = null, kind = null, @base = null;
        var shared = false;
        if (ann is not null)
        {
            var tokens = ann.Value.Body.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
            for (var t = 0; t < tokens.Length; t++)
            {
                var token = tokens[t];
                if (token == "skip")
                {
                    skip = string.Join(' ', tokens.Skip(t + 1));
                    if (skip.Length == 0)
                    {
                        skip = "skipped";
                    }

                    break;
                }
                else if (token is "maui" or "uno") hosts = token;
                else if (token == "both") hosts = null;
                else if (token == "shared") shared = true;
                else if (token is "statements" or "file" or "usings") kind = token;
                else if (token.StartsWith("members", StringComparison.Ordinal))
                {
                    kind = "members";
                    @base = token.Length > 8 && token[7] == '=' ? token[8..] : null;
                }
                else errors.Add($"{source}:{ann.Value.Line}: unknown snippet annotation token '{token}'");
            }
            if (lang == "xml" && kind is not null) errors.Add($"{source}:{ann.Value.Line}: '{kind}' applies to C# blocks only");
        }
        return new Block
        {
            Source = source, FenceLine = fenceLine, CodeLine = fenceLine + 1, Lang = lang, Code = code,
            Skip = skip, Hosts = hosts, Kind = kind, Base = @base, Shared = shared,
        };
    }
}

internal static class Emitter
{
    private static readonly Regex UsingLine = new(@"^\s*using\s+(?<static>static\s+)?(?<ns>[A-Za-z_][\w.]*)\s*;", RegexOptions.Compiled);
    private static readonly Regex TypeDecl = new(
        @"^\s*(\[[^\]]*\]\s*)*((public|internal|private|protected|file|sealed|static|abstract|partial|readonly|unsafe|ref)\s+)*(class|struct|record|interface|enum|delegate)\b",
        RegexOptions.Compiled);
    private static readonly string[] FullPageRoots = ["ContentPage", "ContentView", "Page", "UserControl"];

    private static string HostNs(Host h) => h == Host.Maui ? "KumikoUI.Maui" : "KumikoUI.Uno";

    private static bool Applies(Block b, Host h) => b.Hosts is null || b.Hosts == (h == Host.Maui ? "maui" : "uno");

    // ───── C# ─────

    public static Output? CSharp(Block b, Host host, List<string> usingsLines)
    {
        if (!Applies(b, host)) return null;

        var (hoisted, bodyStart) = HoistUsings(b.Code, host, rewriteHost: b.Kind == "usings");
        var body = b.Code.Skip(bodyStart).ToList();
        var hasBody = body.Any(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("//", StringComparison.Ordinal));

        var preamble = usingsLines.Count == 0 ? [] : HoistUsings(usingsLines, host, rewriteHost: true).Usings;
        var sb = new StringBuilder();
        sb.AppendLine($"// <auto-generated> from {b.Source}:{b.FenceLine} by eng/DocsSnippets. Do not edit; fix the Markdown.");
        sb.AppendLine("#nullable enable"); // generated files default to nullable-disabled

        if (b.Kind == "usings")
        {
            if (hasBody) throw new SnippetException("a 'usings' block may only contain using directives");
            foreach (var u in preamble) sb.AppendLine(u);
            sb.AppendLine("namespace DocsSnippets.Generated;");
            sb.AppendLine($"internal static class Usings_{b.Id} {{ }}");
            return new Output($"{b.Id}.cs", sb.ToString(), IsXaml: false, IsUsings: true);
        }

        if (b.Shared && (b.Kind ?? Detect(body)) != "file")
            throw new SnippetException("'shared' applies to blocks of type declarations (file kind)");
        if (!hasBody)
            throw new SnippetException("the block has only using directives; annotate it '<!-- snippet: usings -->' (or add code)");

        var kind = b.Kind ?? Detect(body);
        var allUsings = preamble.Concat(hoisted).Concat(["using DocsSnippets;"]).Distinct().ToList();
        if (kind is "statements" or "members") allUsings.Add("using static DocsSnippets.Fixtures;");
        foreach (var u in allUsings) sb.AppendLine(u);
        sb.AppendLine();

        var codeLine = b.CodeLine + bodyStart;
        var text = string.Join('\n', body);
        switch (kind)
        {
            case "file":
                if (!body.Any(l => l.TrimStart().StartsWith("namespace ", StringComparison.Ordinal)))
                    sb.AppendLine(b.Shared ? "namespace DocsSnippets;" : $"namespace DocsSnippets.Generated.{b.Id};");
                sb.AppendLine();
                Line(sb, codeLine, b.Source);
                sb.AppendLine(text);
                sb.AppendLine("#line default");
                break;
            case "members":
                sb.AppendLine($"namespace DocsSnippets.Generated.{b.Id};");
                sb.AppendLine();
                sb.AppendLine($"internal partial class Snippet{(b.Base is null ? "" : " : " + b.Base)}");
                sb.AppendLine("{");
                Line(sb, codeLine, b.Source);
                sb.AppendLine(text);
                sb.AppendLine("#line default");
                sb.AppendLine("}");
                break;
            default: // statements
                sb.AppendLine($"namespace DocsSnippets.Generated.{b.Id};");
                sb.AppendLine();
                sb.AppendLine("internal static class Snippet");
                sb.AppendLine("{");
                sb.AppendLine(Regex.IsMatch(text, @"\bawait\b")
                    ? "    internal static async System.Threading.Tasks.Task Run()"
                    : "    internal static void Run()");
                sb.AppendLine("    {");
                Line(sb, codeLine, b.Source);
                sb.AppendLine(text);
                sb.AppendLine("#line default");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                break;
        }
        return new Output($"{b.Id}.cs", sb.ToString(), IsXaml: false, IsUsings: false);
    }

    /// <summary>Repository root; <c>#line</c> uses the Markdown file's full path so the compiler can open (and embed) it.</summary>
    public static string Root { get; set; } = "";

    // Compiler errors point at the README line, not at the generated file.
    private static void Line(StringBuilder sb, int line, string source) =>
        sb.AppendLine($"#line {line} \"{Path.Join(Root, source).Replace('\\', '/')}\"");

    /// <summary>Collects the leading using directives (and blank/comment lines between them).</summary>
    private static (List<string> Usings, int BodyStart) HoistUsings(List<string> code, Host host, bool rewriteHost)
    {
        var usings = new List<string>();
        var i = 0;
        for (; i < code.Count; i++)
        {
            var t = code[i].Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal)) continue;
            var m = UsingLine.Match(code[i]);
            if (!m.Success) break;
            var ns = m.Groups["ns"].Value;
            if (rewriteHost && ns is "KumikoUI.Maui" or "KumikoUI.Uno") ns = HostNs(host);
            usings.Add($"using {m.Groups["static"].Value}{ns};");
        }
        // Leave trailing blank/comment lines that precede the body with the body, so #line stays accurate.
        while (i > 0 && (code[i - 1].Trim().Length == 0 || code[i - 1].TrimStart().StartsWith("//", StringComparison.Ordinal))) i--;
        return (usings, i);
    }

    private static string Detect(List<string> body)
    {
        foreach (var l in body)
        {
            var t = l.Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal)) continue;
            if (t.StartsWith("namespace ", StringComparison.Ordinal) || TypeDecl.IsMatch(l)) return "file";
            if (t.StartsWith('[')) continue; // attribute on the next line's type
            return "statements";
        }
        return "statements";
    }

    // ───── XAML ─────

    public static Output? Xaml(Block b, Host host)
    {
        var text = string.Join('\n', b.Code).Trim();
        var bare = Regex.Replace(text, @"<!--.*?-->", "", RegexOptions.Singleline);
        var root = Regex.Match(bare, @"<\s*(?<name>[A-Za-z_][\w:.]*)");
        if (!root.Success) throw new SnippetException("no XML element found");
        var name = root.Groups["name"].Value;
        var cls = $"DocsSnippets.Generated.{b.Id}";
        var origin = $"<!-- from {b.Source}:{b.FenceLine} by eng/DocsSnippets. Do not edit; fix the Markdown. -->";

        if (FullPageRoots.Contains(name))
        {
            var pageHost = bare.Contains("schemas.microsoft.com/dotnet/2021/maui", StringComparison.Ordinal) ? Host.Maui
                : bare.Contains("schemas.microsoft.com/winfx/2006/xaml/presentation", StringComparison.Ordinal) ? Host.Uno
                : throw new SnippetException($"cannot tell which host the <{name}> page is for: its default xmlns is neither MAUI's nor WinUI's");
            if (b.Hosts is not null && b.Hosts != (pageHost == Host.Maui ? "maui" : "uno"))
                throw new SnippetException($"annotation says {b.Hosts} but the page's xmlns is {pageHost}");
            if (host != pageHost) return null;
            if (bare.Contains("x:Class", StringComparison.Ordinal)) throw new SnippetException("page already has x:Class; remove it from the README (the tool adds one)");
            if (!bare.Contains("xmlns:x=", StringComparison.Ordinal)) throw new SnippetException("page does not declare the x prefix");
            var m = Regex.Match(text, @"<\s*" + Regex.Escape(name) + @"\b");
            text = text.Insert(m.Index + m.Length, $" x:Class=\"{cls}\"");
            return new Output($"{b.Id}.xaml", $"{origin}\n{text}\n", IsXaml: true, IsUsings: false);
        }

        if (!Applies(b, host)) return null;

        // Fragments: a data grid (placed in a layout) or content of a data grid (columns, summary rows, ...).
        if (!name.StartsWith("kumiko:", StringComparison.Ordinal) && !name.StartsWith("core:", StringComparison.Ordinal)
            && !name.StartsWith("render:", StringComparison.Ordinal))
            throw new SnippetException($"unrecognized XML root <{name}>: not a page and not a kumiko:/core:/render: fragment; annotate it '<!-- snippet: skip reason -->'");

        var content = name == "kumiko:KumikoDataGrid" ? text : $"<kumiko:KumikoDataGrid>\n{text}\n</kumiko:KumikoDataGrid>";
        var (page, layout, xmlns) = host == Host.Maui
            ? ("ContentPage", "VerticalStackLayout",
               "xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" " +
               "xmlns:kumiko=\"clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui\" " +
               "xmlns:core=\"clr-namespace:KumikoUI.Core.Models;assembly=KumikoUI.Core\" " +
               "xmlns:render=\"clr-namespace:KumikoUI.Core.Rendering;assembly=KumikoUI.Core\"")
            : ("Page", "StackPanel",
               "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
               "xmlns:kumiko=\"using:KumikoUI.Uno\" xmlns:core=\"using:KumikoUI.Core.Models\" xmlns:render=\"using:KumikoUI.Core.Rendering\"");
        var sb = new StringBuilder();
        sb.AppendLine(origin);
        sb.AppendLine($"<{page} x:Class=\"{cls}\" {xmlns}>");
        sb.AppendLine($"<{layout}>");
        sb.AppendLine(content);
        sb.AppendLine($"</{layout}>");
        sb.AppendLine($"</{page}>");
        return new Output($"{b.Id}.xaml", sb.ToString(), IsXaml: true, IsUsings: false);
    }
}
