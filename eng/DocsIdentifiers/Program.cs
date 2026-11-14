using System.Text;
using System.Text.RegularExpressions;

// Fails when an instruction or architecture document names code that does not exist.
//
//   dotnet run --project eng/DocsIdentifiers -c Release [-- options]
//
//   --file <path-or-glob>   Markdown file to check, relative to the repository root. Repeatable. Replaces the
//                           default list (see DefaultFiles). A missing file or a glob with no match is an error.
//   --root <dir>            Repository root. Default: the nearest parent directory that has a global.json.
//   --allowlist <path>      Identifiers that are not code. Default: eng/DocsIdentifiers/allowlist.txt
//
// What is checked: every inline `code span`, and the multi-hump names (DrawRows, HitRegion.Cell) in fenced blocks.
//   * A span that names a file (a path, or a name ending in .cs, .csproj, .md, ...) must name a file that exists
//     (a path relative to the repository root or to the document; a bare name anywhere in the repository).
//   * Every other span: each identifier in it that starts with an uppercase letter (at least three characters, not
//     all capitals) must occur in the code. "The code" is the non-comment text of src/**/*.cs and the build files
//     (csproj, props, targets, workflows); tests/ and eng/ count too (the documents name test helpers), and
//     so does samples/ for the documents about the samples and the hosts.
//   * `Type.Member`: when Type is declared in src, Member must occur in the files that declare Type or its base
//     types and interfaces.
// An identifier the tool cannot find must be fixed in the document, or, when it is not code (an external API, a
// placeholder, an example), listed in the allowlist with a reason. An allowlist entry no document uses is an error,
// so the list cannot rot.
//
// This is a word check, not a compiler: it catches a documented name that was renamed or removed, not a member
// moved to another type, a changed signature, or a member that is now internal or private (the documents name
// internal and private members on purpose, so any declaration counts). Code blocks that matter are compiled by eng/DocsSnippets.

return Checker.Run(args);

static class Checker
{
    static readonly string[] FileExtensions =
        [".cs", ".csproj", ".props", ".targets", ".md", ".yml", ".yaml", ".json", ".sln", ".xaml", ".sh", ".txt", ".runsettings"];

    static readonly string[] DefaultFiles =
    [
        ".github/copilot-instructions.md",
        ".github/instructions/*.md",
        ".github/prompts/*.md",
        "docs/ARCHITECTURE.md",
        "docs/RENDERING.md",
        "docs/uno/README.md",
    ];

    static readonly string[] PathRoots = ["src/", "tests/", "samples/", "docs/", "eng/", ".github/", "bench/"];

    public static int Run(string[] args)
    {
        string? root = null, allowlistPath = null;
        var files = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--root": root = Next(); break;
                case "--file": files.Add(Next()); break;
                case "--allowlist": allowlistPath = Next(); break;
                default: Console.Error.WriteLine($"Unknown argument '{args[i]}'."); return 2;
            }
        }

        root = Path.GetFullPath(root ?? FindRoot());
        allowlistPath ??= Path.Combine(root, "eng", "DocsIdentifiers", "allowlist.txt");
        bool defaultRun = files.Count == 0;
        if (defaultRun) files.AddRange(DefaultFiles);

        var allow = LoadAllowlist(allowlistPath);
        var repo = new Repo(root);
        var errors = new List<string>();
        var usedAllow = new HashSet<string>(StringComparer.Ordinal);
        int documents = 0, spans = 0, identifiers = 0;

        foreach (var pattern in files)
        {
            var matches = Expand(root, pattern).ToList();
            if (matches.Count == 0) { errors.Add($"{pattern}: no such file"); continue; }
            foreach (var rel in matches)
            {
                documents++;
                var scope = repo.ScopeFor(rel);
                var lines = File.ReadAllLines(Path.Combine(root, rel));
                bool fenced = false;
                for (var n = 0; n < lines.Length; n++)
                {
                    var line = lines[n];
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { fenced = !fenced; continue; }
                    if (fenced)
                    {
                        CheckBlockLine(line, rel, n + 1, scope, repo, allow, usedAllow, errors, ref identifiers);
                        continue;
                    }
                    foreach (Match m in Regex.Matches(line, @"`([^`\n]+)`"))
                    {
                        spans++;
                        CheckSpan(m.Groups[1].Value.Trim(), rel, n + 1, scope, repo, root, allow, usedAllow, errors, ref identifiers);
                    }
                }
            }
        }

        // An allowlist entry that nothing uses is stale; only a run over the default documents can tell.
        foreach (var a in allow.Where(a => defaultRun && !usedAllow.Contains(a)).OrderBy(a => a, StringComparer.Ordinal))
            errors.Add($"{Path.GetRelativePath(root, allowlistPath)}: allowlist entry '{a}' is not used by any document; remove it");

        var distinct = errors.Distinct().ToList();
        foreach (var e in distinct) Console.Error.WriteLine(e);
        Console.WriteLine($"DocsIdentifiers: {documents} documents, {spans} code spans, {identifiers} identifiers checked, {distinct.Count} problems.");
        return distinct.Count == 0 ? 0 : 1;
    }

    static void CheckSpan(string span, string doc, int line, HashSet<string> scope, Repo repo, string root,
        HashSet<string> allow, HashSet<string> usedAllow, List<string> errors, ref int identifiers)
    {
        // The whole span can be allowlisted (a command, a placeholder, a shorthand).
        if (allow.Contains(span)) { usedAllow.Add(span); return; }

        // A file: a path with a known extension or a trailing slash, or a bare file name.
        var pathLike = span.Split(' ', 2)[0];
        bool hasExt = FileExtensions.Any(e => pathLike.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        bool isPath = pathLike.Contains('/') && !pathLike.Contains("://")
            && (hasExt || pathLike.EndsWith('/') || PathRoots.Any(r => pathLike.StartsWith(r, StringComparison.Ordinal)));
        if (isPath)
        {
            identifiers++;
            var clean = pathLike.TrimEnd('/', ',', '.', ')', ':');
            // A glob: some file of its directory must match. A placeholder ({Name}): the part before it must exist.
            int wild = clean.IndexOfAny(['*', '{', '<', '[', '?']);
            bool found;
            if (wild >= 0 && clean[wild] == '*' && clean.IndexOf('/', wild) < 0)
            {
                int slash = clean.LastIndexOf('/', wild);
                var dir = Path.Combine(root, slash < 0 ? "" : clean[..slash]);
                found = Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir, clean[(slash + 1)..].Replace("**", "*")).Any();
            }
            else
            {
                // A placeholder inside a name (Drawn{Type}.cs): the folder before it must exist.
                var prefix = (wild >= 0 ? clean[..(clean.LastIndexOf('/', wild) < 0 ? 0 : clean.LastIndexOf('/', wild))] : clean).TrimEnd('/');
                found = prefix.Length == 0 || Exists(root, doc, prefix)
                    // A folder named without its parents (`Input/`): some folder of that name exists.
                    || (!PathRoots.Any(r => prefix.StartsWith(r, StringComparison.Ordinal)) && repo.FileNames.Contains(prefix.Split('/')[^1]));
            }
            if (!found)
            {
                if (allow.Contains(clean)) usedAllow.Add(clean);
                else errors.Add($"{doc}:{line}: path '{clean}' does not exist");
            }
            return;
        }
        if (!span.Contains(' ') && hasExt)
        {
            if (span.Contains('{')) return;   // a template name: {ClassName}Tests.cs
            identifiers++;
            if (!repo.FileNames.Contains(span))
            {
                if (allow.Contains(span)) usedAllow.Add(span);
                else errors.Add($"{doc}:{line}: file '{span}' does not exist in the repository");
            }
            return;
        }

        // Identifiers: strip string literals and {placeholders}, then walk the tokens.
        var text = Regex.Replace(span, "\"[^\"]*\"|'[^']*'|\\{[^}]*\\}", " ");
        var tokens = Regex.Matches(text, @"[A-Za-z_][A-Za-z0-9_]*").Select(t => (t.Value, t.Index)).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var (tok, index) = tokens[i];
            if (!IsCandidate(tok)) continue;
            identifiers++;

            // Type.Member: check the member against the type when the type is ours.
            string? qualifier = null;
            if (i > 0 && index > 0 && text[index - 1] == '.' && tokens[i - 1].Index + tokens[i - 1].Value.Length == index - 1)
                qualifier = tokens[i - 1].Value;

            if (allow.Contains(tok)) { usedAllow.Add(tok); continue; }
            if (qualifier != null && allow.Contains(qualifier + "." + tok)) { usedAllow.Add(qualifier + "." + tok); continue; }

            // `Populate*` or `Drawn...`: a prefix of a family of names.
            bool prefix = index + tok.Length < text.Length && (text[index + tok.Length] == '*' || text.AsSpan(index + tok.Length).StartsWith("..."));
            if (prefix ? !scope.Any(w => w.StartsWith(tok, StringComparison.Ordinal)) : !scope.Contains(tok))
            {
                errors.Add($"{doc}:{line}: '{tok}' (in `{span}`) does not occur in the code");
                continue;
            }
            if (qualifier != null && repo.IsDeclaredType(qualifier) && !repo.TypeHasMember(qualifier, tok))
                errors.Add($"{doc}:{line}: '{qualifier}.{tok}' (in `{span}`): {qualifier} has no member '{tok}'");
        }
    }

    // A line of a fenced block (a diagram, C# or XAML): only multi-hump names (DrawRows, HitRegion.Cell) are checked,
    // so prose in a diagram and an example's own single words (Name, Employee) are not mistaken for code. Comments
    // are skipped. Blocks that must compile are compiled by eng/DocsSnippets.
    static void CheckBlockLine(string line, string doc, int lineNumber, HashSet<string> scope, Repo repo,
        HashSet<string> allow, HashSet<string> usedAllow, List<string> errors, ref int identifiers)
    {
        var text = BlockNoise.Replace(line, " ");
        var tokens = Regex.Matches(text, @"[A-Za-z_][A-Za-z0-9_]*").Select(t => (t.Value, t.Index)).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var (tok, index) = tokens[i];
            if (!IsMultiHump(tok)) continue;
            identifiers++;
            string? qualifier = null;
            if (i > 0 && index > 0 && text[index - 1] == '.' && tokens[i - 1].Index + tokens[i - 1].Value.Length == index - 1)
                qualifier = tokens[i - 1].Value;
            if (allow.Contains(tok)) { usedAllow.Add(tok); continue; }
            if (qualifier != null && allow.Contains(qualifier + "." + tok)) { usedAllow.Add(qualifier + "." + tok); continue; }
            if (!scope.Contains(tok))
                errors.Add($"{doc}:{lineNumber}: '{tok}' (in a code block) does not occur in the code");
            else if (qualifier != null && repo.IsDeclaredType(qualifier) && !repo.TypeHasMember(qualifier, tok))
                errors.Add($"{doc}:{lineNumber}: '{qualifier}.{tok}' (in a code block): {qualifier} has no member '{tok}'");
        }
    }

    // String literals, XML comments, // comments, and # comments (diagram annotations) of a block line.
    static readonly Regex BlockNoise = new("\"[^\"]*\"|<!--.*?-->|//.*$|\\s#\\s.*$|^\\s*#.*$|\\w*\\{[A-Za-z_][A-Za-z0-9_]*\\}\\w*", RegexOptions.Compiled);

    // Uppercase first letter and a later capital after a lowercase letter: DrawRows and HitRegion, not SKCanvas.
    // A name with an underscore is a test name (Method_Scenario_Expected) the document invents, not code.
    static bool IsMultiHump(string token) =>
        !token.Contains('_') && IsCandidate(token) && Regex.IsMatch(token, "[a-z][A-Z0-9]*[A-Z]");

    // Starts uppercase, at least three characters, at least one lowercase letter (all-capital names are acronyms:
    // DIP, GPU, RTL).
    static bool IsCandidate(string token) =>
        token.Length >= 3 && char.IsUpper(token[0]) && token.Any(char.IsLower);

    static bool Exists(string root, string doc, string rel)
    {
        if (File.Exists(Path.Combine(root, rel)) || Directory.Exists(Path.Combine(root, rel))) return true;
        var docDir = Path.GetDirectoryName(Path.Combine(root, doc))!;
        return File.Exists(Path.Combine(docDir, rel)) || Directory.Exists(Path.Combine(docDir, rel));
    }

    static HashSet<string> LoadAllowlist(string path)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return set;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            if (line.Length > 0) set.Add(line);
        }
        return set;
    }

    static IEnumerable<string> Expand(string root, string pattern)
    {
        if (!pattern.Contains('*'))
        {
            if (File.Exists(Path.Combine(root, pattern))) yield return pattern;
            yield break;
        }
        var dir = Path.GetDirectoryName(pattern) ?? "";
        var full = Path.Combine(root, dir);
        if (!Directory.Exists(full)) yield break;
        foreach (var f in Directory.EnumerateFiles(full, Path.GetFileName(pattern)).OrderBy(f => f, StringComparer.Ordinal))
            yield return Path.GetRelativePath(root, f).Replace('\\', '/');
    }

    static string FindRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No global.json found above the current directory; pass --root.");
    }
}

/// <summary>The words of the repository's code, by area.</summary>
sealed class Repo
{
    static readonly HashSet<string> SkippedDirs = new(StringComparer.Ordinal)
        { "bin", "obj", ".git", "node_modules", ".vs", "Generated", "TestResults", "worktrees", ".claude" };

    readonly HashSet<string> _src = new(StringComparer.Ordinal);
    readonly HashSet<string> _build = new(StringComparer.Ordinal);
    readonly HashSet<string> _tests = new(StringComparer.Ordinal);
    readonly HashSet<string> _samples = new(StringComparer.Ordinal);
    readonly Dictionary<string, HashSet<string>> _typeWords = new(StringComparer.Ordinal);
    readonly Dictionary<string, HashSet<string>> _typeBases = new(StringComparer.Ordinal);

    /// <summary>The names of every file in the repository.</summary>
    public HashSet<string> FileNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    static readonly Regex Word = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    static readonly Regex TypeDecl = new(
        @"\b(?:class|struct|enum|interface|record)\s+(?:struct\s+|class\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>{(]*>)?\s*(?:\([^)]*\))?\s*(?::\s*([^{;]+))?",
        RegexOptions.Compiled);

    public Repo(string root)
    {
        foreach (var file in Walk(root))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            FileNames.Add(Path.GetFileName(file));
            foreach (var part in rel.Split('/')[..^1]) FileNames.Add(part);   // folder names count too
            var ext = Path.GetExtension(file);
            bool inSrc = rel.StartsWith("src/", StringComparison.Ordinal);
            bool inSamples = rel.StartsWith("samples/", StringComparison.Ordinal);
            bool inTests = rel.StartsWith("tests/", StringComparison.Ordinal);
            if (ext == ".cs")
            {
                var code = StripComments(File.ReadAllText(file));
                var words = Words(code);
                if (inSrc) { _src.UnionWith(words); IndexTypes(code, words); }
                else if (inSamples) _samples.UnionWith(words);
                else _tests.UnionWith(words);   // tests/, eng/, bench/
            }
            else if (ext is ".csproj" or ".props" or ".targets" or ".yml" or ".yaml" or ".xaml" or ".sh" or ".json" or ".sln" or ".slnx")
            {
                var words = Words(File.ReadAllText(file));
                if (inSamples) _samples.UnionWith(words);
                else if (inTests) _tests.UnionWith(words);
                else _build.UnionWith(words);
            }
        }
    }

    /// <summary>The identifiers a document may name: the library, the build files, and by topic the tests and samples.</summary>
    public HashSet<string> ScopeFor(string doc)
    {
        var scope = new HashSet<string>(_src, StringComparer.Ordinal);
        scope.UnionWith(_build);
        scope.UnionWith(_tests);
        var name = doc.ToLowerInvariant();
        if (name.Contains("maui") || name.Contains("uno") || name.Contains("sample") || name.Contains("configure-datagrid") || name.Contains("copilot-instructions"))
            scope.UnionWith(_samples);
        return scope;
    }

    public bool IsDeclaredType(string name) => _typeWords.ContainsKey(name);

    public bool TypeHasMember(string type, string member)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(type);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (!seen.Add(t)) continue;
            if (_typeWords.TryGetValue(t, out var words) && words.Contains(member)) return true;
            if (_typeBases.TryGetValue(t, out var bases)) foreach (var b in bases) queue.Enqueue(b);
        }
        return false;
    }

    void IndexTypes(string code, HashSet<string> fileWords)
    {
        foreach (Match m in TypeDecl.Matches(code))
        {
            var name = m.Groups[1].Value;
            if (!_typeWords.TryGetValue(name, out var words)) _typeWords[name] = words = new HashSet<string>(StringComparer.Ordinal);
            words.UnionWith(fileWords);
            if (!m.Groups[2].Success) continue;
            if (!_typeBases.TryGetValue(name, out var bases)) _typeBases[name] = bases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in m.Groups[2].Value.Split(','))
            {
                var w = Word.Match(b.Replace("global::", ""));
                if (w.Success) bases.Add(w.Value);
            }
        }
    }

    static HashSet<string> Words(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Word.Matches(text)) set.Add(m.Value);
        return set;
    }

    // Removes // and /* */ comments (XML documentation included) outside string literals.
    static string StripComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"')
            {
                bool verbatim = (i > 0 && s[i - 1] == '@') || (i > 1 && s[i - 2] == '@' && s[i - 1] == '$') || (i > 1 && s[i - 2] == '$' && s[i - 1] == '@');
                sb.Append(c);
                for (i++; i < s.Length; i++)
                {
                    sb.Append(s[i]);
                    if (!verbatim && s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); continue; }
                    if (s[i] == '"')
                    {
                        if (verbatim && i + 1 < s.Length && s[i + 1] == '"') { sb.Append(s[++i]); continue; }
                        break;
                    }
                }
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                sb.Append('\n');
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i++;
                sb.Append(' ');
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    static IEnumerable<string> Walk(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir)) yield return f;
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            if (SkippedDirs.Contains(Path.GetFileName(d))) continue;
            foreach (var f in Walk(d)) yield return f;
        }
    }
}
