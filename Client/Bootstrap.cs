using System.IO;
#if !DEBUG
using System;
using System.Linq;
using System.Reflection;
#endif

public static class Bootstrap
{
#if !DEBUG
    public static byte[] ReadAsBytes(this Stream input)
    {
        var array = new byte[16384];
        byte[] result;
        using (var memoryStream = new MemoryStream())
        {
            int count;
            while ((count = input.Read(array, 0, array.Length)) > 0)
            {
                memoryStream.Write(array, 0, count);
            }
            result = memoryStream.ToArray();
        }
        return result;
    }
#endif

    static void Main(string[] args)
    {
#if !DEBUG
        var assemblies = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.EndsWith(".dll")).ToDictionary(n => n, n => Assembly.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream(n).ReadAsBytes()));
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => assemblies.FirstOrDefault(kv => kv.Key == $"{new AssemblyName(e.Name).Name}.dll").Value;
#endif
        Client.Program.Main(args);
    }
}
