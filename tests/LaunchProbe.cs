using System;
using System.IO;
class LaunchProbe {
    static void Main(){File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"launch-probe-count.txt"),"opened"+Environment.NewLine);}
}
