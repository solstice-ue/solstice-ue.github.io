using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class LetTests
{
    [Fact]
    public void EX_Let与EX_LetBool的字段()
    {
        foreach (var type in new[] { typeof(EX_Let), typeof(EX_LetBool), typeof(EX_LetValueOnPersistentFrame) })
        {
            Console.WriteLine($"[let] {type.Name}:");
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine($"[let]   {f.FieldType.Name} {f.Name}");
            }
        }
    }
}
