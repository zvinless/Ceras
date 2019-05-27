#addin "nuget:?package=Cake.Compression&version=0.2.3"
#addin "nuget:?package=YamlDotNet&version=6.0.0"

using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using Path = Cake.Core.IO.Path;

var createUnityPackage = Argument("target", "Ceras.UnityAddon");
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

// Task("Ceras")
// 	.Does(() => DotNetCoreBuild("src/Ceras/Ceras.csproj", dotNetBuildConfig));

// Task("Compress")
// 	.Does(() => {
// 		Zip("src/Ceras/bin/Any CPU/Release/netstandard2.0", "ceras_netstandard2.0.zip");
// 	});
// Task("Ceras.ImmutableCollections")
// 	.IsDependentOn("Ceras")
// 	.Does(() => DotNetCoreBuild("src/Ceras.ImmutableCollections/Ceras.ImmutableCollections.csproj", dotNetBuildConfig));
// Task("Ceras.Test")
// 	.IsDependentOn("Ceras")
// 	.Does(() => DotNetCoreBuild("src/Ceras.Test/Ceras.Test.csproj", dotNetBuildConfig));
// Task("Ceras.AotGenerator")
// 	.IsDependentOn("Ceras")
// 	.Does(() => DotNetCoreBuild("src/Ceras.AotGenerator/Ceras.AotGenerator.csproj", dotNetBuildConfig));
// Task("Ceras.AotGeneratorApp")
// 	.IsDependentOn("Ceras.AotGenerator")
// 	.Does(() => DotNetCoreBuild("src/Ceras.AotGeneratorApp/Ceras.AotGeneratorApp.csproj", dotNetBuildConfig));

var packageDir = Directory("src/Ceras.UnityAddon/Release");
var runtimeDir = packageDir + Directory("Runtime");
var editorDir = packageDir + Directory("Editor");

Task("Ceras.UnityAddon")
	.Does(() => {
		// Collect runtime dependencies
		// Technically this isn't necessary since net47 happens to have the same dependencies,
		// but this shouldn't take very long
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
		CopyFiles("src/Ceras/bin/Release/netstandard2.0/publish/System.*.dll", runtimeDir);
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

RunTarget(createUnityPackage);
