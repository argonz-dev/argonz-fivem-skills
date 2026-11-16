using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using CodeWalker.GameFiles;

internal static class Program
{
    private const string CliVersion = "1.1.0";

    private const string Help = @"CodeWalker CLI (.NET Framework 4.8)

Usage:
  codewalker-cli version [--json]
  codewalker-cli help [--json]
  codewalker-cli rpf list ARCHIVE [--contains TEXT] [--game-dir PATH] [--json]
  codewalker-cli rpf extract ARCHIVE ENTRY OUTPUT [--game-dir PATH] [--json]
  codewalker-cli xml export INPUT OUTPUT_DIRECTORY [--game-dir PATH] [--names-dir PATH] [--json]
  codewalker-cli xml import INPUT_XML OUTPUT [--json]

ENTRY is the full internal path shown by rpf list (without the outer RPF name).
XML export writes XML, any embedded DDS textures, and resolves hashes from strings.txt,
sibling asset files, --names-dir, and (optionally) --game-dir.
XML import expects CodeWalker filenames such as prop.ydr.xml or map.ymap.xml.
Existing outputs are rejected. Exit codes: 0 success, 1 error, 2 usage.";

    private static readonly Dictionary<string, MetaFormat> Formats =
        new Dictionary<string, MetaFormat>(StringComparer.OrdinalIgnoreCase)
        {
            { ".ymap", MetaFormat.RSC }, { ".ytyp", MetaFormat.RSC },
            { ".ydr", MetaFormat.Ydr }, { ".ydd", MetaFormat.Ydd },
            { ".yft", MetaFormat.Yft }, { ".ytd", MetaFormat.Ytd },
            { ".ybn", MetaFormat.Ybn }, { ".ycd", MetaFormat.Ycd }
        };

    private static readonly HashSet<string> ValueOptions =
        new HashSet<string>(StringComparer.Ordinal) { "--contains", "--game-dir", "--names-dir" };

    private static readonly string[] AssetNamePatterns =
        new[] { "*.ydr", "*.ydd", "*.yft", "*.ytd", "*.ybn", "*.ycd", "*.ytyp", "*.ymap", "*.sps" };

    private static readonly Regex UnresolvedHashRegex =
        new Regex(@"hash_[0-9A-Fa-f]{8}", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        bool json = HasJsonFlag(args);
        try
        {
            List<string> positional;
            Dictionary<string, string> options;
            bool helpFlag;
            bool versionFlag;
            ParseArgs(args, out positional, out options, out json, out helpFlag, out versionFlag);

            if (helpFlag || IsHelpRequest(positional))
            {
                if (options.Count > 0 || versionFlag)
                    throw new ArgumentException("Help cannot be combined with other options.");
                if (json) WriteJson(new { ok = true, help = Help });
                else Console.WriteLine(Help);
                return 0;
            }

            if (versionFlag)
            {
                if (positional.Count > 0 || options.Count > 0)
                    throw new ArgumentException("Usage: codewalker-cli version [--json]");
                positional.Add("version");
            }

            if (positional.Count == 0)
            {
                if (json) WriteJson(new { ok = true, help = Help });
                else Console.WriteLine(Help);
                return 0;
            }

            Execute(positional, options, json);
            return 0;
        }
        catch (Exception error)
        {
            int code = error is ArgumentException ? 2 : 1;
            if (json) WriteJson(new { ok = false, error = error.Message, exitCode = code });
            else Console.Error.WriteLine(error.Message);
            return code;
        }
    }

    private static bool HasJsonFlag(string[] args)
    {
        if (args == null) return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--") break;
            if (args[i] == "--json") return true;
        }
        return false;
    }

    private static void ParseArgs(
        string[] args,
        out List<string> positional,
        out Dictionary<string, string> options,
        out bool json,
        out bool helpFlag,
        out bool versionFlag)
    {
        positional = new List<string>();
        options = new Dictionary<string, string>();
        json = false;
        helpFlag = false;
        versionFlag = false;
        bool literal = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!literal && arg == "--") { literal = true; continue; }
            if (!literal && arg == "--json") { json = true; continue; }
            if (!literal && (arg == "-h" || arg == "--help")) { helpFlag = true; continue; }
            if (!literal && arg == "--version") { versionFlag = true; continue; }
            if (!literal && arg.StartsWith("--"))
            {
                if (!ValueOptions.Contains(arg))
                    throw new ArgumentException("Unknown option: " + arg);
                if (options.ContainsKey(arg))
                    throw new ArgumentException("Duplicate option: " + arg);
                if (++i == args.Length || IsOptionToken(args[i]))
                    throw new ArgumentException("Expected one value for " + arg);
                options.Add(arg, args[i]);
            }
            else
            {
                positional.Add(arg);
            }
        }
    }

    private static bool IsOptionToken(string value)
    {
        return value == "--" || value == "--json" || value == "-h" || value == "--help"
            || value == "--version" || ValueOptions.Contains(value);
    }

    private static bool IsHelpRequest(List<string> positional)
    {
        if (positional.Count == 1 && string.Equals(positional[0], "help", StringComparison.OrdinalIgnoreCase))
            return true;
        if (positional.Count >= 2 && string.Equals(positional[positional.Count - 1], "help", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static void ValidateOptions(Dictionary<string, string> options, params string[] allowed)
    {
        foreach (string key in options.Keys)
        {
            if (!allowed.Contains(key))
                throw new ArgumentException("Option " + key + " is not valid for this command.");
        }
    }

    private static void Execute(List<string> args, Dictionary<string, string> options, bool json)
    {
        if (args.Count == 1 && args[0] == "version")
        {
            ValidateOptions(options);
            string core = typeof(RpfFile).Assembly.GetName().Version.ToString();
            if (json) WriteJson(new { ok = true, cli = CliVersion, core = core });
            else Console.WriteLine("CodeWalker CLI " + CliVersion + " | CodeWalker.Core " + core);
            return;
        }

        if (args.Count == 3 && args[0] == "rpf" && args[1] == "list")
        {
            ValidateOptions(options, "--contains", "--game-dir");
            string filter;
            options.TryGetValue("--contains", out filter);
            var entries = Entries(OpenArchive(args[2], options))
                .Where(e => filter == null || e.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(e => new { path = e.Key, size = e.Value.GetFileSize() }).ToArray();
            if (json) WriteJson(new { ok = true, count = entries.Length, entries = entries });
            else foreach (var entry in entries) Console.WriteLine("{0}\t{1}", entry.size, entry.path);
            return;
        }

        if (args.Count == 5 && args[0] == "rpf" && args[1] == "extract")
        {
            ValidateOptions(options, "--game-dir");
            string target = EnsureNewFileTarget(args[4]);
            var archive = OpenArchive(args[2], options);
            string wanted = args[3].Replace('/', '\\').TrimStart('\\');
            var entry = Entries(archive).SingleOrDefault(e => string.Equals(e.Key, wanted, StringComparison.OrdinalIgnoreCase)).Value;
            if (entry == null) throw new FileNotFoundException("Archive entry not found: " + wanted);
            byte[] data = entry.File.ExtractFile(entry);
            if (data == null) throw new IOException(FirstLine(entry.File.LastError) ?? "Archive extraction failed.");
            var resource = entry as RpfResourceFileEntry;
            if (resource != null)
                data = ResourceBuilder.AddResourceHeader(resource, ResourceBuilder.Compress(data));
            string written = WriteNew(target, data);
            WriteOutputResult(written, json);
            return;
        }

        if (args.Count == 4 && args[0] == "xml" && args[1] == "export")
        {
            ValidateOptions(options, "--game-dir", "--names-dir");
            int unresolvedHashes;
            string written = ExportXml(args[2], args[3], options, out unresolvedHashes);
            if (json) WriteJson(new { ok = true, output = written, unresolvedHashes = unresolvedHashes });
            else Console.WriteLine(written);
            return;
        }

        if (args.Count == 4 && args[0] == "xml" && args[1] == "import")
        {
            ValidateOptions(options);
            string written = ImportXml(args[2], args[3]);
            WriteOutputResult(written, json);
            return;
        }

        throw new ArgumentException("Invalid command. Run codewalker-cli help.");
    }

    private static void WriteOutputResult(string output, bool json)
    {
        if (json) WriteJson(new { ok = true, output = output });
        else Console.WriteLine(output);
    }

    private static bool DetectGen9(string gameDir)
    {
        return File.Exists(Path.Combine(gameDir, "GTA5_Enhanced.exe"))
            && !File.Exists(Path.Combine(gameDir, "GTA5.exe"));
    }

    private static RpfFile OpenArchive(string path, Dictionary<string, string> options)
    {
        string gameDir;
        if (options.TryGetValue("--game-dir", out gameDir))
        {
            string fullGameDir = Path.GetFullPath(gameDir);
            if (!Directory.Exists(fullGameDir))
                throw new DirectoryNotFoundException("Game directory not found: " + fullGameDir);
            GTA5Keys.LoadFromPath(fullGameDir, DetectGen9(fullGameDir));
        }

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Archive not found: " + path, path);

        var scanErrors = new List<string>();
        var archive = new RpfFile(path, Path.GetFileName(path));
        archive.ScanStructure(_ => { }, message =>
        {
            if (!string.IsNullOrWhiteSpace(message))
                scanErrors.Add(FirstLine(message));
        });

        if (archive.Root == null || !string.IsNullOrEmpty(archive.LastError))
        {
            string detail = !string.IsNullOrEmpty(archive.LastError)
                ? FirstLine(archive.LastError)
                : (scanErrors.Count > 0 ? scanErrors[0] : "Unknown RPF error");
            string hint = options.ContainsKey("--game-dir")
                ? string.Empty
                : " Encrypted archives require --game-dir pointing to GTA V Legacy.";
            throw new IOException("Cannot read RPF (" + detail + ")." + hint);
        }

        return archive;
    }

    private static IEnumerable<KeyValuePair<string, RpfFileEntry>> Entries(RpfFile archive)
    {
        int prefix = archive.Path.Length + 1;
        var pending = new Stack<RpfFile>();
        pending.Push(archive);
        while (pending.Count > 0)
        {
            RpfFile current = pending.Pop();
            foreach (var entry in current.AllEntries.OfType<RpfFileEntry>())
                yield return new KeyValuePair<string, RpfFileEntry>(entry.Path.Substring(prefix), entry);
            if (current.Children != null)
            {
                foreach (var child in current.Children)
                {
                    if (child.Root == null || !string.IsNullOrEmpty(child.LastError))
                    {
                        string childPath = child.Path.Length > prefix ? child.Path.Substring(prefix) : child.Name;
                        string detail = string.IsNullOrEmpty(child.LastError)
                            ? "failed to open/decrypt (check --game-dir)"
                            : FirstLine(child.LastError);
                        throw new IOException("Cannot read nested RPF '" + childPath + "': " + detail);
                    }
                    pending.Push(child);
                }
            }
        }
    }

    private static void PopulateJenkIndex(string inputPath, Dictionary<string, string> options)
    {
        for (int i = 0; i < 100; i++)
            JenkIndex.Ensure(i.ToString("00"));

        string stringsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "strings.txt");
        if (File.Exists(stringsPath))
        {
            foreach (string raw in File.ReadAllLines(stringsPath))
            {
                string line = raw != null ? raw.Trim() : null;
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;
                JenkIndex.Ensure(line);
            }
        }

        string gameDir;
        if (options.TryGetValue("--game-dir", out gameDir))
        {
            string fullGameDir = Path.GetFullPath(gameDir);
            if (!Directory.Exists(fullGameDir))
                throw new DirectoryNotFoundException("Game directory not found: " + fullGameDir);
            bool gen9 = DetectGen9(fullGameDir);
            GTA5Keys.LoadFromPath(fullGameDir, gen9);
            var manager = new RpfManager { BuildExtendedJenkIndex = true };
            manager.Init(fullGameDir, gen9, _ => { }, _ => { });
        }

        string inputDir = Path.GetDirectoryName(inputPath);
        if (!string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir))
            RegisterAssetFilenames(inputDir, SearchOption.TopDirectoryOnly);

        string namesDir;
        if (options.TryGetValue("--names-dir", out namesDir))
        {
            string fullNamesDir = Path.GetFullPath(namesDir);
            if (!Directory.Exists(fullNamesDir))
                throw new DirectoryNotFoundException("Names directory not found: " + fullNamesDir);
            RegisterAssetFilenames(fullNamesDir, SearchOption.AllDirectories);
        }
    }

    private static void RegisterAssetFilenames(string folder, SearchOption depth)
    {
        foreach (string pattern in AssetNamePatterns)
        {
            foreach (string file in Directory.GetFiles(folder, pattern, depth))
            {
                string fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName)) continue;
                string lower = fileName.ToLowerInvariant();
                if (lower.EndsWith(".sps"))
                    JenkIndex.Ensure(lower);
                string stem = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(stem)) continue;
                JenkIndex.Ensure(stem);
                JenkIndex.Ensure(stem.ToLowerInvariant());
            }
        }
    }

    private static string ExportXml(string input, string outputDirectory, Dictionary<string, string> options, out int unresolvedHashes)
    {
        input = Path.GetFullPath(input);
        if (!File.Exists(input))
            throw new FileNotFoundException("Input asset not found: " + input, input);
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new IOException("Output already exists: " + outputDirectory);

        MetaFormat format = FormatFor(input);
        byte[] data = File.ReadAllBytes(input);
        if (data.Length < 16) throw new InvalidDataException("Asset file is too short.");

        RpfFileEntry entry;
        if (BitConverter.ToUInt32(data, 0) == 0x37435352)
        {
            entry = RpfFile.CreateResourceFileEntry(ref data, 0);
            try { data = ResourceBuilder.Decompress(data); }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to decompress RSC7 resource: " + ex.Message, ex);
            }
            if (data == null || data.Length == 0)
                throw new InvalidDataException("Failed to decompress RSC7 resource.");
        }
        else if (format == MetaFormat.RSC)
        {
            entry = new RpfBinaryFileEntry { FileSize = (uint)data.Length, FileUncompressedSize = (uint)data.Length };
        }
        else
        {
            throw new InvalidDataException("Expected an RSC7 resource.");
        }

        PopulateJenkIndex(input, options);

        entry.Name = Path.GetFileName(input);
        entry.NameLower = entry.Name.ToLowerInvariant();
        entry.NameHash = JenkHash.GenHash(entry.NameLower);
        entry.ShortNameHash = JenkHash.GenHash(Path.GetFileNameWithoutExtension(entry.NameLower));
        entry.Path = input;

        string parent = Path.GetDirectoryName(outputDirectory);
        if (string.IsNullOrEmpty(parent))
            throw new ArgumentException("Invalid output directory path: " + outputDirectory);
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".codewalker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            string filename;
            string xml = MetaXml.GetXml(entry, data, out filename, staging);
            if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(filename))
                throw new InvalidDataException("CodeWalker could not export this asset.");
            unresolvedHashes = UnresolvedHashRegex.Matches(xml).Count;
            filename = Path.GetFileName(filename);
            File.WriteAllText(Path.Combine(staging, filename), xml, new UTF8Encoding(false));
            Directory.Move(staging, outputDirectory);
            return Path.Combine(outputDirectory, filename);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static string ImportXml(string input, string output)
    {
        input = Path.GetFullPath(input);
        if (!File.Exists(input))
            throw new FileNotFoundException("Input XML not found: " + input, input);
        if (!input.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected a CodeWalker XML file.");

        int trimLength;
        MetaFormat xmlFormat = XmlMeta.GetXMLFormat(Path.GetFileName(input).ToLowerInvariant(), out trimLength);
        bool alternate = xmlFormat == MetaFormat.PSO || xmlFormat == MetaFormat.RBF;
        string assetPath = input.Substring(0, input.Length - (alternate ? trimLength : 4));
        MetaFormat format = FormatFor(assetPath);
        if (alternate && format == MetaFormat.RSC) format = xmlFormat;
        if (!string.Equals(Path.GetExtension(output), Path.GetExtension(assetPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output extension must match the asset type.");

        string target = EnsureNewFileTarget(output);
        var doc = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
            doc.Load(reader);
        string sidecars = Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(assetPath));
        byte[] data = XmlMeta.GetData(doc, format, sidecars);
        if (data == null || data.Length == 0)
            throw new InvalidDataException("CodeWalker did not generate binary data.");
        return WriteNew(target, data);
    }

    private static MetaFormat FormatFor(string path)
    {
        MetaFormat format;
        if (!Formats.TryGetValue(Path.GetExtension(path), out format))
            throw new ArgumentException("Unsupported asset type: " + Path.GetExtension(path));
        return format;
    }

    private static string EnsureNewFileTarget(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new IOException("Output already exists: " + fullPath);
        string parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parent))
            throw new ArgumentException("Invalid output file path: " + fullPath);
        return fullPath;
    }

    private static string WriteNew(string fullPath, byte[] data)
    {
        string parent = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".codewalker-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(staging, data);
            File.Move(staging, fullPath);
            return fullPath;
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        int nl = text.IndexOfAny(new[] { '\r', '\n' });
        string line = nl >= 0 ? text.Substring(0, nl) : text;
        return line.Trim();
    }

    private static void WriteJson(object value)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        Console.WriteLine(serializer.Serialize(value));
    }
}
