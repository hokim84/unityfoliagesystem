using System.Diagnostics;

public static class CodeTimer
{
    public static void Measure(string label, System.Action action)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        action();
        sw.Stop();
        UnityEngine.Debug.Log($"{label} 소요시간: {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalSeconds:F4} sec)");
    }
}