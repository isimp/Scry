// Reads the code shapes of game methods from assembly_valheim.dll, for Compatibility.Copied.
//
// From the repository's folder, after a game update:
//   dotnet run --project tools/shapes -c Release -- --check    lists what changed or is missing
//   dotnet run --project tools/shapes -c Release -- --update   writes the new shapes into Compatibility.cs
// Both find the game through VALHEIM_DIR, or take --dll <path to assembly_valheim.dll>. --check
// ends with exit code 1 when anything changed or is missing, --update when a method is missing
// (a changed shape it writes in; a missing method needs a change to Scry itself).
//
// Or, to read methods not in the list yet:
//   dotnet run --project tools/shapes -c Release -- <path to assembly_valheim.dll> Type.Method [Type.Method ...]
// which prints each overload with its parameter count and shape, and the animation event receivers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Scry;

class Program
{
    private const string ListFile = "src/Patches/Compatibility.cs";

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: --check | --update [--dll <assembly_valheim.dll>], or <assembly_valheim.dll> Type.Method ...");
            return 2;
        }
        if (args[0] == "--check" || args[0] == "--update") return CheckList(args[0] == "--update", DllFrom(args));
        Print(args[0], args.Skip(1).ToList());
        return 0;
    }

    /// <summary>The game's assembly: given after --dll, else in VALHEIM_DIR.</summary>
    private static string DllFrom(string[] args)
    {
        var at = Array.IndexOf(args, "--dll");
        if (at >= 0 && at + 1 < args.Length) return args[at + 1];
        var game = Environment.GetEnvironmentVariable("VALHEIM_DIR");
        return string.IsNullOrEmpty(game) ? null : Path.Combine(game, "valheim_Data", "Managed", "assembly_valheim.dll");
    }

    /// <summary>Compares every method in the check's list with the game, and tells or writes what changed.</summary>
    private static int CheckList(bool write, string dll)
    {
        if (dll == null || !File.Exists(dll))
        {
            Console.Error.WriteLine($"Cannot find the game's assembly ({dll ?? "set VALHEIM_DIR or pass --dll"}).");
            return 2;
        }
        if (!File.Exists(ListFile))
        {
            Console.Error.WriteLine($"Cannot find {ListFile}; run this from the repository's folder.");
            return 2;
        }

        using var pe = new PEReader(File.OpenRead(dll));
        var md = pe.GetMetadataReader();
        var methods = Methods(pe, md);

        var source = File.ReadAllText(ListFile);
        var count = ShapeList.Parse(source).Count;
        var updated = ShapeList.Update(source, entry =>
        {
            // The first with the name and parameter count, as the check in the game picks it.
            var found = methods.Where(m => m.Type == entry.Type && m.Name == entry.Method && m.Params == entry.Params).ToList();
            if (found.Count > 1) Console.WriteLine($"note     {entry.Type}.{entry.Method} has {found.Count} overloads with {entry.Params} parameters; the first is compared, as the game check does");
            return found.Count > 0 ? found[0].Shape : (uint?)null;
        }, out var report);

        foreach (var line in report) Console.WriteLine(line);
        var changed = report.Count(l => l.StartsWith("changed", StringComparison.Ordinal));
        var missing = report.Count(l => l.StartsWith("missing", StringComparison.Ordinal));
        Console.WriteLine($"{count} methods compared: {count - changed - missing} as they were, {changed} changed, {missing} missing.");

        if (write && changed > 0)
        {
            File.WriteAllText(ListFile, updated);
            Console.WriteLine($"Wrote the {changed} new shape(s) into {ListFile}. Read each changed method against what Scry does before releasing.");
        }
        if (missing > 0) Console.WriteLine("A missing method needs a change to Scry itself: find what the game does now, and change or drop the entry.");
        return missing > 0 || (changed > 0 && !write) ? 1 : 0;
    }

    private struct Found
    {
        public string Type;
        public string Name;
        public int Params;
        public uint Shape;
    }

    /// <summary>Every method of the assembly, with its type's name as the game check looks it up (nested as Outer+Inner).</summary>
    private static List<Found> Methods(PEReader pe, MetadataReader md)
    {
        var all = new List<Found>();
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            var typeName = TypeName(md, type);
            foreach (var mh in type.GetMethods())
            {
                var method = md.GetMethodDefinition(mh);
                var name = md.GetString(method.Name);
                all.Add(new Found { Type = typeName, Name = name, Params = ParamCount(md, method), Shape = StepsOf(pe, md, type, method, name) });
            }
        }
        return all;
    }

    /// <summary>
    /// A method's shape as the game check reads it: a coroutine's is its state machine's
    /// MoveNext, where its steps are, as its own method only hands that out.
    /// </summary>
    private static uint StepsOf(PEReader pe, MetadataReader md, TypeDefinition type, MethodDefinition method, string name)
    {
        foreach (var nh in type.GetNestedTypes())
        {
            var nested = md.GetTypeDefinition(nh);
            if (!IlShape.IsStateMachineOf(md.GetString(nested.Name), name)) continue;
            foreach (var mh in nested.GetMethods())
            {
                var step = md.GetMethodDefinition(mh);
                if (md.GetString(step.Name) == "MoveNext") return ShapeOf(pe, step);
            }
        }
        return ShapeOf(pe, method);
    }

    private static string TypeName(MetadataReader md, TypeDefinition type)
    {
        var name = md.GetString(type.Name);
        var outer = type.GetDeclaringType();
        if (!outer.IsNil) return TypeName(md, md.GetTypeDefinition(outer)) + "+" + name;
        var ns = md.GetString(type.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    private static int ParamCount(MetadataReader md, MethodDefinition method)
    {
        var reader = md.GetBlobReader(method.Signature);
        var header = reader.ReadSignatureHeader();
        if (header.IsGeneric) reader.ReadCompressedInteger();
        return reader.ReadCompressedInteger();
    }

    private static uint ShapeOf(PEReader pe, MethodDefinition method) =>
        method.RelativeVirtualAddress != 0 ? IlShape.Of(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()) : 0;

    /// <summary>Every overload of the named methods, and the animation event receivers' methods.</summary>
    private static void Print(string dll, List<string> names)
    {
        using var pe = new PEReader(File.OpenRead(dll));
        var md = pe.GetMetadataReader();
        var wanted = names.Select(a => a.Split('.')).ToList();
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            var typeName = md.GetString(type.Name);
            foreach (var mh in type.GetMethods())
            {
                var method = md.GetMethodDefinition(mh);
                var name = md.GetString(method.Name);
                var receivers = typeName == "CharacterAnimEvent" || typeName == "AnimationEffect";
                if (!wanted.Any(w => w[0] == typeName && w[1] == name) && !receivers) continue;
                var pub = (method.Attributes & System.Reflection.MethodAttributes.Public) != 0 ? "public" : "private";
                Console.WriteLine($"{(receivers ? "RECV " : "")}{typeName}.{name} params={ParamCount(md, method)} {pub} shape=0x{StepsOf(pe, md, type, method, name):X8}");
            }
        }
    }
}
