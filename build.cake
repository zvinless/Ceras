#addin "nuget:?package=YamlDotNet&version=6.0.0"

using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using Path = Cake.Core.IO.Path;

var av = AppVeyor.IsRunningOnAppVeyor;
var target = Argument("target", "All");
var releaseSubPath = $"bin/{(av ? "Any CPU/" : "")}Release";

var dotNetBuildConfig = new DotNetCoreBuildSettings {
	Verbosity = DotNetCoreVerbosity.Minimal,
	Configuration = "Release"
};

void DeleteDirectoryIfExists(string root, string dir)
{
	var path = Directory(root) + Directory(dir);
	if (DirectoryExists(path))
		DeleteDirectory(path, new DeleteDirectorySettings { Recursive = true });
}

void GenerateMetaFiles(ISerializer serializer, string templatePath, IEnumerable<Path> paths)
{
	using (var sr = System.IO.File.OpenText(templatePath))
	{
		var yaml = new YamlStream();
		yaml.Load(sr);
		var root = (YamlMappingNode) yaml.Documents[0].RootNode;
		var guid = (YamlScalarNode) root.Children[new YamlScalarNode("guid")];

		foreach (var path in paths)
		{
			guid.Value = Guid.NewGuid().ToString("N");
			using (var sw = new StreamWriter(path + Directory(".meta")))
				serializer.Serialize(sw, root);
		}
	}
}

Task("Clean")
	.Does(() => {
		DeleteFiles("./*.zip");
		DotNetCoreClean(".", new DotNetCoreCleanSettings { Configuration = "Release" });
	});

Task("Ceras")
	.Does(() => DotNetCoreBuild("src/Ceras/Ceras.csproj", dotNetBuildConfig));
Task("Ceras.ImmutableCollections")
	.Does(() => DotNetCoreBuild("src/Ceras.ImmutableCollections/Ceras.ImmutableCollections.csproj", dotNetBuildConfig));
Task("Ceras.Test")
	.Does(() => DotNetCoreBuild("tests/Ceras.Test/Ceras.Test.csproj", dotNetBuildConfig));
Task("Ceras.AotGenerator")
	.Does(() => DotNetCoreBuild("src/Ceras.AotGenerator/Ceras.AotGenerator.csproj", dotNetBuildConfig));
Task("Ceras.AotGeneratorApp")
	.Does(() => DotNetCoreBuild("src/Ceras.AotGeneratorApp/Ceras.AotGeneratorApp.csproj", dotNetBuildConfig));

Task("Compress")
	.IsDependentOn("Ceras")
	.Does(() => {
		Zip($"src/Ceras/{releaseSubPath}/netstandard2.0", "ceras_netstandard2.0.zip");
		Zip($"src/Ceras/{releaseSubPath}/net45", "ceras_net45.zip");
		Zip($"src/Ceras/{releaseSubPath}/net47", "ceras_net47.zip");
	});

// Put this outside of the repository on AppVeyor
// We'll copy it over after switching branches
var packageDir = Directory($"{(av ? ".." : "src")}/Ceras.UnityAddon/Release");
var runtimeDir = packageDir + Directory("Runtime");
var editorDir = packageDir + Directory("Editor");

Task("Ceras.UnityAddon")
	.IsDependentOn("Ceras")
	.Does(() => {
		// Collect runtime dependencies
		DotNetCorePublish("src/Ceras/Ceras.csproj", new DotNetCorePublishSettings {
			Framework = "netstandard2.0",
			Configuration = "Release"
		});

		// Root
		EnsureDirectoryExists(packageDir);
		CleanDirectory(packageDir);
		CopyFile("src/Ceras.UnityAddon/package.json", packageDir.Path.GetFilePath("package.json"));
		CopyFiles("*.md", packageDir); // LICENSE, REAMDE

		// Runtime
		EnsureDirectoryExists(runtimeDir);
		CopyFiles($"src/Ceras/{releaseSubPath}/netstandard2.0/publish/System.*.dll", runtimeDir);
		CopyFiles("src/Ceras/**/*.cs", runtimeDir, true);
		DeleteDirectoryIfExists(runtimeDir, "obj");
		CopyDirectory("src/Ceras.UnityAddon/Runtime", runtimeDir);

		// Editor
		EnsureDirectoryExists(editorDir);
		CopyFiles("src/Ceras.AotGenerator/**/*.cs", editorDir, true);
		DeleteDirectoryIfExists(editorDir, "obj");
		DeleteDirectoryIfExists(editorDir, "bin");
		CopyDirectory("src/Ceras.UnityAddon/Editor", editorDir);

		// Generate .meta files
		var serializer = new SerializerBuilder().DisableAliases().Build();

		var directories = GetDirectories(packageDir.ToString() + "/**");
		GenerateMetaFiles(serializer, "src/Ceras.UnityAddon/MetaTemplates/folder.yaml", directories.Cast<Path>());

		var fileData = new [] {
			("/**/*.dll",    "dll.yaml"),
			("/**/*.cs",     "cs.yaml"),
			("/**/*.asmdef", "asmdef.yaml"),
			("/**/*.md",     "text.yaml"),
			("/**/*.xml",    "text.yaml"),
			("/**/*.json",   "text.yaml"),
		};

		foreach (var data in fileData)
		{
			var (glob, template) = data;
			var files = GetFiles(packageDir.ToString() + glob);
			GenerateMetaFiles(serializer, $"src/Ceras.UnityAddon/MetaTemplates/{template}", files.Cast<Path>());
		}
	});

Task("All")
	.IsDependentOn("Clean")
	.IsDependentOn("Ceras")
	.IsDependentOn("Ceras.ImmutableCollections")
	.IsDependentOn("Ceras.Test")
	.IsDependentOn("Ceras.AotGenerator")
	.IsDependentOn("Ceras.AotGeneratorApp")
	.IsDependentOn("Compress")
	.IsDependentOn("Ceras.UnityAddon");

RunTarget(target);
