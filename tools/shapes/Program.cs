// Reads the code shapes of game methods from assembly_valheim.dll, for Compatibility.Copied.
// Usage: dotnet run -c Release -- <path to assembly_valheim.dll> Type.Method [Type.Method ...]
// Prints each overload with its parameter count and shape, and the animation event receivers.
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Prints every overload of the named methods with its parameter count and code shape,
// and the public methods of the animation event receivers.
class Program
{
    static void Main(string[] args)
    {
        var path = args[0];
        using var pe = new PEReader(File.OpenRead(path));
        var md = pe.GetMetadataReader();
        var wanted = args.Skip(1).Select(a => a.Split('.')).ToList();
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
                var reader = md.GetBlobReader(method.Signature);
                var header = reader.ReadSignatureHeader();
                if (header.IsGeneric) reader.ReadCompressedInteger();
                var count = reader.ReadCompressedInteger();
                uint shape = 0;
                if (method.RelativeVirtualAddress != 0) shape = Scry.IlShape.Of(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes());
                var pub = (method.Attributes & System.Reflection.MethodAttributes.Public) != 0 ? "public" : "private";
                Console.WriteLine($"{(receivers ? "RECV " : "")}{typeName}.{name} params={count} {pub} shape=0x{shape:X8}");
            }
        }
    }
}
