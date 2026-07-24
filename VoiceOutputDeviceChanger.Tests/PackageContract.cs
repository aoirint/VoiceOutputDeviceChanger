using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace VoiceOutputDeviceChanger.Tests;

internal static class PackageFixtureBuilder
{
    private const int RegularFileAttributes = unchecked((int)0x81A40000);
    private static readonly DateTimeOffset ArchiveTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static void WriteArchive(string archivePath, IReadOnlyList<ArchiveSource> sources)
    {
        using FileStream file = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Encoding.UTF8);
        foreach (ArchiveSource source in sources)
        {
            ZipArchiveEntry entry = archive.CreateEntry(source.Name, CompressionLevel.SmallestSize);
            entry.LastWriteTime = ArchiveTimestamp;
            entry.ExternalAttributes = source.ExternalAttributes;
            using Stream destination = entry.Open();
            destination.Write(source.Content, 0, source.Content.Length);
        }
    }

    internal sealed record ArchiveSource(string Name, byte[] Content, int ExternalAttributes = RegularFileAttributes)
    {
        public static ArchiveSource FromFile(string name, string path) => new(name, File.ReadAllBytes(path));
    }
}

internal static class PackageContract
{
    internal const long MaximumEntryLength = 8 * 1024 * 1024;
    internal const long MaximumArchiveLength = 16 * 1024 * 1024;
    internal const long MaximumTotalExpandedLength = 16 * 1024 * 1024;
    private const long MaximumCompressionRatio = 100;
    private static readonly HashSet<string> ExpectedEntries = new(StringComparer.Ordinal)
    {
        "VoiceOutputDeviceChanger.dll",
        "README.md",
        "CHANGELOG.md",
        "manifest.json",
        "icon.png",
        "LICENSE",
    };

    public static void Validate(string archivePath, string expectedProjectVersion, string expectedArtifactVersion)
    {
        FileInfo archiveFile = new(archivePath);
        if (!archiveFile.Exists || archiveFile.Length == 0 || archiveFile.Length > MaximumArchiveLength)
        {
            throw new InvalidDataException("Archive is missing, empty, or exceeds the size limit.");
        }

        string expectedArchiveName = $"VoiceOutputDeviceChanger-v{expectedArtifactVersion}.zip";
        if (!string.Equals(archiveFile.Name, expectedArchiveName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Archive name must be {expectedArchiveName}.");
        }

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count != ExpectedEntries.Count)
        {
            throw new InvalidDataException("Archive must contain exactly six files.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        long totalExpandedLength = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ValidateEntry(entry);
            totalExpandedLength = checked(totalExpandedLength + entry.Length);
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"Duplicate archive entry: {entry.FullName}");
            }
        }

        if (!names.SetEquals(ExpectedEntries))
        {
            throw new InvalidDataException("Archive entries do not match the package contract.");
        }

        if (totalExpandedLength > MaximumTotalExpandedLength)
        {
            throw new InvalidDataException("Archive expanded size exceeds the package limit.");
        }

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            long compressedLength = Math.Max(1, entry.CompressedLength);
            if (entry.Length > compressedLength * MaximumCompressionRatio)
            {
                throw new InvalidDataException($"Archive entry exceeds the compression ratio limit: {entry.FullName}");
            }
        }

        string expectedPluginVersion = ResolvePluginVersion(expectedProjectVersion);
        string expectedManifestVersion = ResolveManifestVersion(expectedProjectVersion);
        byte[] assemblyBytes = ReadEntry(archive, "VoiceOutputDeviceChanger.dll");
        ValidateAssembly(assemblyBytes, expectedProjectVersion, expectedPluginVersion);
        ValidateReadme(ReadText(archive, "README.md"));
        ValidateChangelog(ReadText(archive, "CHANGELOG.md"), expectedProjectVersion);
        ValidateManifest(ReadText(archive, "manifest.json"), expectedManifestVersion);
        ValidateIcon(ReadEntry(archive, "icon.png"));
        if (ReadEntry(archive, "LICENSE").Length == 0)
        {
            throw new InvalidDataException("Packaged LICENSE must not be empty.");
        }
    }

    internal static string ResolvePluginVersion(string projectVersion) =>
        IsPrerelease(projectVersion) ? "0.0.0" : projectVersion;

    internal static string ResolveManifestVersion(string projectVersion) =>
        IsPrerelease(projectVersion) ? "0.0.0" : projectVersion;

    private static bool IsPrerelease(string version) => version.Contains('-', StringComparison.Ordinal);

    private static void ValidateEntry(ZipArchiveEntry entry)
    {
        string rawName = entry.FullName;
        if (string.IsNullOrWhiteSpace(rawName)
            || rawName.Contains('\\', StringComparison.Ordinal)
            || rawName[0] == '/'
            || Regex.IsMatch(rawName, "^[A-Za-z]:", RegexOptions.CultureInvariant)
            || rawName.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException($"Unsafe archive entry name: {rawName}");
        }

        int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        bool dosDirectory = (entry.ExternalAttributes & 0x10) != 0;
        if (entry.Name.Length == 0 || dosDirectory || (unixType != 0 && unixType != 0x8000))
        {
            throw new InvalidDataException($"Archive entry must be a regular file: {rawName}");
        }

        if (entry.Length < 0 || entry.Length > MaximumEntryLength)
        {
            throw new InvalidDataException($"Archive entry exceeds the size limit: {rawName}");
        }
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.GetEntry(name)
            ?? throw new InvalidDataException($"Missing archive entry: {name}");
        using Stream source = entry.Open();
        using var destination = new MemoryStream();
        source.CopyTo(destination);
        return destination.ToArray();
    }

    private static string ReadText(ZipArchive archive, string name) => Encoding.UTF8.GetString(ReadEntry(archive, name));

    private static void ValidateReadme(string readme)
    {
        (string Value, string Description)[] requiredClaims =
        {
            ("Voice Output Device Changer", "product identity"),
            ("Lethal Company v81", "game version"),
            ("6423525044216269478", "game manifest"),
            ("BepInExPack", "package dependency identity"),
            ("v5.4.2305", "package dependency version"),
        };

        foreach ((string value, string description) in requiredClaims)
        {
            if (!readme.Contains(value, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Packaged README {description} is incorrect.");
            }
        }
    }

    private static void ValidateChangelog(string changelog, string version)
    {
        bool isDevelopmentVersion = string.Equals(version, "0.0.0", StringComparison.Ordinal);
        string requiredHeading = isDevelopmentVersion ? "## Unreleased" : $"## v{version}";
        if (isDevelopmentVersion && changelog.Contains("## v0.0.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Packaged changelog must not present v0.0.0 as a release version.");
        }

        if (!changelog.Contains(requiredHeading, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Packaged changelog is missing {requiredHeading}.");
        }
    }

    private static void ValidateManifest(string manifest, string expectedVersion)
    {
        PackageManifest parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PackageManifest>(manifest)
                ?? throw new InvalidDataException("Packaged manifest must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Packaged manifest is not valid JSON.", exception);
        }

        if (!string.Equals(parsed.Name, "VoiceOutputDeviceChanger", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Packaged manifest name is incorrect.");
        }

        if (!string.Equals(parsed.Version, expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Packaged manifest version is incorrect.");
        }

        string[] expectedDependencies = { "BepInEx-BepInExPack-5.4.2305" };
        if (parsed.Dependencies is null
            || !parsed.Dependencies.SequenceEqual(expectedDependencies, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Packaged manifest dependencies are incorrect.");
        }
    }

    private sealed class PackageManifest
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("version_number")]
        public string? Version { get; init; }

        [JsonPropertyName("dependencies")]
        public string[]? Dependencies { get; init; }
    }

    private static void ValidateIcon(byte[] icon)
    {
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (icon.Length < 24 || !icon.Take(signature.Length).SequenceEqual(signature))
        {
            throw new InvalidDataException("Packaged icon is not a PNG image.");
        }

        int width = ReadBigEndianInt32(icon, 16);
        int height = ReadBigEndianInt32(icon, 20);
        if (width != 256 || height != 256)
        {
            throw new InvalidDataException("Packaged icon must be 256x256 pixels.");
        }
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24)
        | (bytes[offset + 1] << 16)
        | (bytes[offset + 2] << 8)
        | bytes[offset + 3];

    private static void ValidateAssembly(
        byte[] assemblyBytes,
        string expectedProjectVersion,
        string expectedPluginVersion)
    {
        try
        {
            using var stream = new MemoryStream(assemblyBytes, writable: false);
            using ModuleDefinition module = ModuleDefinition.ReadModule(stream);
            if (!string.Equals(module.Assembly.Name.Name, "VoiceOutputDeviceChanger", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Packaged assembly name is incorrect.");
            }

            string expectedAssemblyVersion = $"{expectedProjectVersion.Split('-', 2)[0]}.0";
            if (!string.Equals(module.Assembly.Name.Version.ToString(), expectedAssemblyVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Packaged assembly version is incorrect.");
            }

            CustomAttribute plugin = FindSingleAttribute(module, "BepInEx.BepInPlugin");
            ValidateStringArgument(plugin, 0, "com.aoirint.voiceoutputdevicechanger", "plugin GUID");
            ValidateStringArgument(plugin, 1, "Voice Output Device Changer", "plugin name");
            ValidateStringArgument(plugin, 2, expectedPluginVersion, "plugin version");

            CustomAttribute process = FindSingleAttribute(module, "BepInEx.BepInProcess");
            ValidateStringArgument(process, 0, "Lethal Company.exe", "process restriction");
        }
        catch (BadImageFormatException exception)
        {
            throw new InvalidDataException("Packaged plugin DLL is not a valid managed assembly.", exception);
        }
    }

    private static CustomAttribute FindSingleAttribute(ModuleDefinition module, string attributeType)
    {
        CustomAttribute[] matches = module.Types
            .SelectMany(type => type.CustomAttributes)
            .Where(attribute => string.Equals(attribute.AttributeType.FullName, attributeType, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException($"Expected exactly one {attributeType} attribute.");
        }

        return matches[0];
    }

    private static void ValidateStringArgument(CustomAttribute attribute, int index, string expected, string field)
    {
        if (attribute.ConstructorArguments.Count <= index
            || attribute.ConstructorArguments[index].Value is not string actual
            || !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Packaged {field} is incorrect.");
        }
    }
}

internal static class PackageContractTests
{
    public static void Run(
        string assemblyPath,
        string repositoryRoot,
        string expectedVersion,
        string expectedArtifactVersion,
        string? archivePath)
    {
        string expectedManifestVersion = PackageContract.ResolveManifestVersion(expectedVersion);
        string tempRoot = Path.Combine(Path.GetTempPath(), $"voice-output-package-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            PackageFixtureBuilder.ArchiveSource[] validSources =
                CreateValidSources(assemblyPath, repositoryRoot, expectedManifestVersion);
            AssertAccepted("valid", validSources, tempRoot, expectedVersion);

            AssertRejected("missing-file", validSources.Where(source => source.Name != "README.md"), tempRoot, expectedVersion, "exactly six files");
            AssertRejected("unexpected-file", Replace(validSources, "LICENSE", new("notes.txt", Encoding.UTF8.GetBytes("unexpected"))), tempRoot, expectedVersion, "do not match the package contract");
            AssertRejected("duplicate-file", Replace(validSources, "LICENSE", validSources.Single(source => source.Name == "README.md")), tempRoot, expectedVersion, "Duplicate archive entry");
            AssertRejected("extra-dll", Replace(validSources, "LICENSE", new("Other.dll", new byte[] { 1, 2, 3 })), tempRoot, expectedVersion, "do not match the package contract");
            AssertRejected("traversal", Replace(validSources, "LICENSE", new("../escape", new byte[] { 1 })), tempRoot, expectedVersion, "Unsafe archive entry name");
            AssertRejected("absolute", Replace(validSources, "LICENSE", new("/escape", new byte[] { 1 })), tempRoot, expectedVersion, "Unsafe archive entry name");
            AssertRejected("backslash", Replace(validSources, "LICENSE", new("nested\\escape", new byte[] { 1 })), tempRoot, expectedVersion, "Unsafe archive entry name");
            AssertRejected("symlink", Replace(validSources, "README.md", new("README.md", Encoding.UTF8.GetBytes("target"), unchecked((int)0xA1FF0000))), tempRoot, expectedVersion, "regular file");
            AssertRejected("oversized-entry", Replace(validSources, "README.md", new("README.md", new byte[checked((int)PackageContract.MaximumEntryLength + 1)])), tempRoot, expectedVersion, "entry exceeds the size limit");
            AssertRejected(
                "oversized-expanded-total",
                Replace(
                    Replace(
                        validSources,
                        "README.md",
                        new("README.md", new byte[checked((int)PackageContract.MaximumEntryLength)])),
                    "CHANGELOG.md",
                    new("CHANGELOG.md", new byte[checked((int)PackageContract.MaximumEntryLength)])),
                tempRoot,
                expectedVersion,
                "expanded size exceeds");
            AssertRejected(
                "compression-ratio",
                Replace(
                    validSources,
                    "README.md",
                    new("README.md", new byte[1024 * 1024])),
                tempRoot,
                expectedVersion,
                "compression ratio limit");
            AssertRejected("corrupt-dll", Replace(validSources, "VoiceOutputDeviceChanger.dll", new("VoiceOutputDeviceChanger.dll", new byte[] { 1, 2, 3 })), tempRoot, expectedVersion, "not a valid managed assembly");
            AssertRejected("wrong-assembly", MutateAssembly(validSources, module => module.Assembly.Name.Name = "WrongAssembly"), tempRoot, expectedVersion, "assembly name is incorrect");
            AssertRejected("wrong-assembly-version", MutateAssembly(validSources, module => module.Assembly.Name.Version = new Version(9, 9, 9, 0)), tempRoot, expectedVersion, "assembly version is incorrect");
            AssertRejected("missing-plugin", MutateAssembly(validSources, module => RemoveAttribute(module, "BepInEx.BepInPlugin")), tempRoot, expectedVersion, "Expected exactly one BepInEx.BepInPlugin attribute");
            AssertRejected("wrong-guid", MutateAssembly(validSources, module => SetAttributeArgument(module, "BepInEx.BepInPlugin", 0, "invalid.guid")), tempRoot, expectedVersion, "plugin GUID is incorrect");
            AssertRejected("wrong-name", MutateAssembly(validSources, module => SetAttributeArgument(module, "BepInEx.BepInPlugin", 1, "Wrong Name")), tempRoot, expectedVersion, "plugin name is incorrect");
            AssertRejected("wrong-version", MutateAssembly(validSources, module => SetAttributeArgument(module, "BepInEx.BepInPlugin", 2, "9.9.9")), tempRoot, expectedVersion, "plugin version is incorrect");
            AssertRejected("missing-process", MutateAssembly(validSources, module => RemoveAttribute(module, "BepInEx.BepInProcess")), tempRoot, expectedVersion, "Expected exactly one BepInEx.BepInProcess attribute");
            AssertRejected("wrong-process", MutateAssembly(validSources, module => SetAttributeArgument(module, "BepInEx.BepInProcess", 0, "Wrong.exe")), tempRoot, expectedVersion, "process restriction is incorrect");
            AssertRejected(
                "readme-product",
                Replace(
                    validSources,
                    "README.md",
                    MutateReadme(validSources, "Voice Output Device Changer", "Wrong Product")),
                tempRoot,
                expectedVersion,
                "README product identity is incorrect");
            AssertRejected(
                "readme-game-version",
                Replace(
                    validSources,
                    "README.md",
                    MutateReadme(validSources, "Lethal Company v81", "Lethal Company v80")),
                tempRoot,
                expectedVersion,
                "README game version is incorrect");
            AssertRejected(
                "readme-game-manifest",
                Replace(
                    validSources,
                    "README.md",
                    MutateReadme(validSources, "6423525044216269478", "0000000000000000000")),
                tempRoot,
                expectedVersion,
                "README game manifest is incorrect");
            AssertRejected(
                "readme-dependency-identity",
                Replace(
                    validSources,
                    "README.md",
                    MutateReadme(validSources, "BepInExPack", "WrongPack")),
                tempRoot,
                expectedVersion,
                "README package dependency identity is incorrect");
            AssertRejected(
                "readme-dependency-version",
                Replace(
                    validSources,
                    "README.md",
                    MutateReadme(validSources, "v5.4.2305", "v5.4.2304")),
                tempRoot,
                expectedVersion,
                "README package dependency version is incorrect");
            AssertRejected("changelog", Replace(validSources, "CHANGELOG.md", new("CHANGELOG.md", Encoding.UTF8.GetBytes("# Changelog"))), tempRoot, expectedVersion, "changelog is missing");
            if (string.Equals(expectedVersion, "0.0.0", StringComparison.Ordinal))
            {
                AssertRejected(
                    "development-changelog-version",
                    Replace(
                        validSources,
                        "CHANGELOG.md",
                        new(
                            "CHANGELOG.md",
                            Encoding.UTF8.GetBytes("# Changelog\n\n## Unreleased\n\n## v0.0.0 - Unreleased\n"))),
                    tempRoot,
                    expectedVersion,
                    "must not present v0.0.0 as a release version");
            }

            AssertRejected(
                "manifest-name",
                Replace(validSources, "manifest.json", new("manifest.json", ManifestBytes(expectedManifestVersion, "WrongName"))),
                tempRoot,
                expectedVersion,
                "manifest name is incorrect");
            AssertRejected(
                "manifest-version",
                Replace(validSources, "manifest.json", new("manifest.json", ManifestBytes("9.9.9", "VoiceOutputDeviceChanger"))),
                tempRoot,
                expectedVersion,
                "manifest version is incorrect");
            AssertRejected(
                "manifest-dependencies",
                Replace(
                    validSources,
                    "manifest.json",
                    new("manifest.json", ManifestBytes(expectedManifestVersion, "VoiceOutputDeviceChanger", "Wrong-Dependency-1.0.0"))),
                tempRoot,
                expectedVersion,
                "manifest dependencies are incorrect");
            AssertRejected(
                "manifest-malformed",
                Replace(validSources, "manifest.json", new("manifest.json", Encoding.UTF8.GetBytes("{ not-json"))),
                tempRoot,
                expectedVersion,
                "manifest is not valid JSON");
            AssertRejected(
                "manifest-misleading-field",
                Replace(validSources, "manifest.json", new("manifest.json", MisleadingManifestBytes(expectedManifestVersion))),
                tempRoot,
                expectedVersion,
                "manifest name is incorrect");
            AssertRejected("icon", Replace(validSources, "icon.png", new("icon.png", new byte[] { 1, 2, 3 })), tempRoot, expectedVersion, "icon is not a PNG image");
            AssertRejected("empty-license", Replace(validSources, "LICENSE", new("LICENSE", Array.Empty<byte>())), tempRoot, expectedVersion, "LICENSE must not be empty");
            AssertRejectedArchiveName(validSources, tempRoot, expectedVersion);
            AssertRejectedArchiveSize(tempRoot, expectedVersion);
            if (archivePath is not null)
            {
                PackageContract.Validate(archivePath, expectedVersion, expectedArtifactVersion);
            }

            Console.WriteLine("All package contract tests passed.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static PackageFixtureBuilder.ArchiveSource[] CreateValidSources(
        string assemblyPath,
        string repositoryRoot,
        string expectedManifestVersion) => new[]
    {
        PackageFixtureBuilder.ArchiveSource.FromFile("VoiceOutputDeviceChanger.dll", assemblyPath),
        PackageFixtureBuilder.ArchiveSource.FromFile("README.md", Path.Combine(repositoryRoot, "assets", "README.md")),
        PackageFixtureBuilder.ArchiveSource.FromFile("CHANGELOG.md", Path.Combine(repositoryRoot, "assets", "CHANGELOG.md")),
        ManifestFromFile(Path.Combine(repositoryRoot, "assets", "manifest.json"), expectedManifestVersion),
        PackageFixtureBuilder.ArchiveSource.FromFile("icon.png", Path.Combine(repositoryRoot, "assets", "icon.png")),
        PackageFixtureBuilder.ArchiveSource.FromFile("LICENSE", Path.Combine(repositoryRoot, "LICENSE")),
    };

    private static PackageFixtureBuilder.ArchiveSource ManifestFromFile(string path, string version)
    {
        // CI rewrites only version_number during staging; mirror that
        // production transformation while preserving the repository asset.
        JsonObject manifest = JsonNode.Parse(File.ReadAllBytes(path))?.AsObject()
            ?? throw new InvalidDataException("Source manifest must be a JSON object.");
        manifest["version_number"] = version;
        return new("manifest.json", Encoding.UTF8.GetBytes(manifest.ToJsonString()));
    }

    private static PackageFixtureBuilder.ArchiveSource MutateReadme(
        IReadOnlyList<PackageFixtureBuilder.ArchiveSource> sources,
        string oldValue,
        string newValue)
    {
        PackageFixtureBuilder.ArchiveSource source =
            sources.Single(candidate => candidate.Name == "README.md");
        string readme = Encoding.UTF8.GetString(source.Content);
        string mutated = readme.Replace(oldValue, newValue, StringComparison.Ordinal);
        if (string.Equals(mutated, readme, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"README fixture source is missing: {oldValue}");
        }

        return new("README.md", Encoding.UTF8.GetBytes(mutated));
    }

    private static void AssertAccepted(
        string name,
        IEnumerable<PackageFixtureBuilder.ArchiveSource> sources,
        string tempRoot,
        string expectedVersion)
    {
        string archivePath = WriteFixture(name, sources, tempRoot, expectedVersion);
        PackageContract.Validate(archivePath, expectedVersion, expectedVersion);
    }

    private static void AssertRejected(
        string name,
        IEnumerable<PackageFixtureBuilder.ArchiveSource> sources,
        string tempRoot,
        string expectedVersion,
        string expectedMessage)
    {
        string archivePath = WriteFixture(name, sources, tempRoot, expectedVersion);
        try
        {
            PackageContract.Validate(archivePath, expectedVersion, expectedVersion);
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Mutation fixture {name} reached the wrong rejection: {exception.Message}",
                exception);
        }

        throw new InvalidOperationException($"Mutation fixture was accepted: {name}");
    }

    private static void AssertRejectedArchiveName(
        IReadOnlyList<PackageFixtureBuilder.ArchiveSource> sources,
        string tempRoot,
        string expectedVersion)
    {
        string archivePath = Path.Combine(tempRoot, "wrong-archive-name.zip");
        PackageFixtureBuilder.WriteArchive(archivePath, sources);
        AssertValidationFailure(
            "wrong-archive-name",
            archivePath,
            expectedVersion,
            "Archive name must be");
    }

    private static void AssertRejectedArchiveSize(string tempRoot, string expectedVersion)
    {
        string directory = Path.Combine(tempRoot, "oversized-archive");
        Directory.CreateDirectory(directory);
        string archivePath = Path.Combine(directory, $"VoiceOutputDeviceChanger-v{expectedVersion}.zip");
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(PackageContract.MaximumArchiveLength + 1);
        }

        AssertValidationFailure(
            "oversized-archive",
            archivePath,
            expectedVersion,
            "exceeds the size limit");
    }

    private static void AssertValidationFailure(
        string name,
        string archivePath,
        string expectedVersion,
        string expectedMessage)
    {
        try
        {
            PackageContract.Validate(archivePath, expectedVersion, expectedVersion);
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Mutation fixture {name} reached the wrong rejection: {exception.Message}",
                exception);
        }

        throw new InvalidOperationException($"Mutation fixture was accepted: {name}");
    }

    private static string WriteFixture(
        string name,
        IEnumerable<PackageFixtureBuilder.ArchiveSource> sources,
        string tempRoot,
        string expectedVersion)
    {
        string fixtureDirectory = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(fixtureDirectory);
        string archivePath = Path.Combine(fixtureDirectory, $"VoiceOutputDeviceChanger-v{expectedVersion}.zip");
        PackageFixtureBuilder.WriteArchive(archivePath, sources.ToArray());
        return archivePath;
    }

    private static IEnumerable<PackageFixtureBuilder.ArchiveSource> Replace(
        IEnumerable<PackageFixtureBuilder.ArchiveSource> sources,
        string name,
        PackageFixtureBuilder.ArchiveSource replacement) =>
        sources.Select(source => string.Equals(source.Name, name, StringComparison.Ordinal) ? replacement : source);

    private static byte[] ManifestBytes(
        string version,
        string name,
        params string[] dependencies) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            name,
            version_number = version,
            dependencies = dependencies.Length == 0
                ? new[] { "BepInEx-BepInExPack-5.4.2305" }
                : dependencies,
        });

    private static byte[] MisleadingManifestBytes(string expectedVersion) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            description = $"name VoiceOutputDeviceChanger version_number {expectedVersion} BepInEx-BepInExPack-5.4.2305",
            name = "WrongName",
            version_number = expectedVersion,
            dependencies = new[] { "BepInEx-BepInExPack-5.4.2305" },
        });

    private static IEnumerable<PackageFixtureBuilder.ArchiveSource> MutateAssembly(
        IReadOnlyList<PackageFixtureBuilder.ArchiveSource> sources,
        Action<ModuleDefinition> mutation)
    {
        PackageFixtureBuilder.ArchiveSource assembly = sources.Single(source => source.Name == "VoiceOutputDeviceChanger.dll");
        using var input = new MemoryStream(assembly.Content, writable: false);
        using ModuleDefinition module = ModuleDefinition.ReadModule(input);
        mutation(module);
        using var output = new MemoryStream();
        module.Write(output);
        return Replace(sources, assembly.Name, new(assembly.Name, output.ToArray()));
    }

    private static void RemoveAttribute(ModuleDefinition module, string attributeType)
    {
        TypeDefinition owner = FindAttributeOwner(module, attributeType);
        owner.CustomAttributes.Remove(owner.CustomAttributes.Single(attribute => attribute.AttributeType.FullName == attributeType));
    }

    private static void SetAttributeArgument(ModuleDefinition module, string attributeType, int index, string value)
    {
        TypeDefinition owner = FindAttributeOwner(module, attributeType);
        CustomAttribute attribute = owner.CustomAttributes.Single(item => item.AttributeType.FullName == attributeType);
        attribute.ConstructorArguments[index] = new CustomAttributeArgument(module.TypeSystem.String, value);
    }

    private static TypeDefinition FindAttributeOwner(ModuleDefinition module, string attributeType) =>
        module.Types.Single(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == attributeType));

}
