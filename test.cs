using System;
using System.IO;
using System.Threading.Tasks;

class P {
    static async Task Main() {
        var tempPath = "test.txt.tmp";
        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        await using (var sw = new StreamWriter(fs))
        {
            await sw.WriteAsync("hello");
            await sw.FlushAsync();
        }
        Console.WriteLine("Disposed? " + !File.Exists(tempPath));
        try {
            File.Move(tempPath, "test.txt", true);
            Console.WriteLine("Moved!");
        } catch(Exception e) {
            Console.WriteLine("Error: " + e.Message);
        }
    }
}
