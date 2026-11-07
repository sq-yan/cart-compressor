using System;
using System.Linq;
using Mono.Cecil;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Buffers.Binary;

using var mod = AssemblyDefinition.ReadAssembly(args[0]);
using var game = AssemblyDefinition.ReadAssembly(args[1]);
var refs = mod.MainModule.AssemblyReferences.Select(x => x.Name).ToArray();
foreach (var forbidden in new[] { "ShrinkCart", "ScalerCore", "REPOConfig" })
    if (refs.Contains(forbidden)) throw new Exception("Unexpected dependency: " + forbidden);
var resources = mod.MainModule.Resources.OfType<EmbeddedResource>().ToArray();
var art = resources.Single(x => x.Name == "CartCompressor.CartCrunch.png").GetResourceData();
if (art.Length < 1000 || art[0] != 137 || art[1] != 80) throw new Exception("Missing embedded PNG");
int count = 0;
foreach (var type in mod.MainModule.Types)
foreach (var attribute in type.CustomAttributes.Where(x => x.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
{
    var values = attribute.ConstructorArguments;
    if (values.Count < 2 || values[0].Value is not TypeReference owner || values[1].Value is not string name) continue;
    var gameType = game.MainModule.GetType(owner.FullName) ?? throw new Exception("Missing game type " + owner.FullName);
    var methods = gameType.Methods.Where(x => x.Name == name).ToArray();
    if (methods.Length != 1) throw new Exception("Missing or ambiguous target: " + owner.FullName + "." + name);
    Console.WriteLine("Patch target OK: " + owner.FullName + "." + name);
    count++;
}
if (count != 6) throw new Exception("Expected 6 patches, found " + count);
Console.WriteLine("Dependencies OK: " + string.Join(", ", refs));
Console.WriteLine("Embedded artwork OK: " + art.Length + " bytes");
if (args.Length > 2)
{
    using var zip = ZipFile.OpenRead(args[2]);
    string[] expected = { "CartCompressor.dll", "manifest.json", "README.md", "CHANGELOG.md", "LICENSE", "icon.png" };
    if (!zip.Entries.Select(x => x.FullName).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)))
        throw new Exception("Unexpected or missing package files");
    using var manifestStream = zip.GetEntry("manifest.json").Open();
    using var manifest = JsonDocument.Parse(manifestStream);
    string version = manifest.RootElement.GetProperty("version_number").GetString();
    if (version != mod.Name.Version.ToString(3)) throw new Exception("Manifest and DLL versions differ");
    using var image = new MemoryStream();
    zip.GetEntry("icon.png").Open().CopyTo(image);
    byte[] png = image.ToArray();
    if (png.Length < 24 || png[0] != 137 || png[1] != 80 ||
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) != 256 ||
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) != 256)
        throw new Exception("Package icon must be a 256x256 PNG");
    using var packed = new MemoryStream();
    zip.GetEntry("CartCompressor.dll").Open().CopyTo(packed);
    if (!packed.ToArray().SequenceEqual(File.ReadAllBytes(args[0]))) throw new Exception("Packed DLL differs from build");
    Console.WriteLine("Thunderstore package OK: exact file list, matching version/DLL, 256x256 PNG.");
}
