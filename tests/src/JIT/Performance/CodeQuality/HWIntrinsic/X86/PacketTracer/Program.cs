// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

//[assembly: OptimizeForBenchmarks]

class Program
{
#if DEBUG

    private const int RunningTime = 200;
    private const int Width = 248;
    private const int Height = 248;
    private const int Iterations = 1;
    private const int MaxIterations = 1000;

#else

    private const int RunningTime = 1000;
    private const int Width = 248;
    private const int Height = 248;
    private const int Iterations = 7;
    private const int MaxIterations = 1000;

#endif

    private double framesPerSecond;
    private int frames;
    private int[] rgbBuffer;

    public Program()
    {
        rgbBuffer = new int[Width * 3 * Height]; // Each pixel has 3 fields (RGB)
    }

    static unsafe int Main(string[] args)
    {
        if (Avx2.IsSupported)
        {
            var r = new Program();
            // We can use `RenderTo` to generate a picture in a PPM file for debugging
            // r.RenderTo("./pic.ppm", true);
            bool result = r.Run();
            return (result ? 100 : -1);
        }
        return 100;
    }

    private unsafe void RenderLoop(int iterations)
    {
        // Create a ray tracer, and create a reference to "sphere2" that we are going to bounce
        var packetTracer = new Packet256Tracer(Width, Height);
        var scene = packetTracer.DefaultScene;
        var sphere2 = (SpherePacket256)scene.Things[0]; // The first item is assumed to be our sphere
        var baseY = sphere2.Radiuses;
        sphere2.Centers.Ys = sphere2.Radiuses;

        // Timing determines how fast the ball bounces as well as diagnostics frames/second info
        var renderingTime = new Stopwatch();
        var totalTime = Stopwatch.StartNew();

        // Keep rendering until the iteration count is hit
        for (frames = 0; frames < iterations; frames++)
        {
            // Determine the new position of the sphere based on the current time elapsed
            float dy2 = 0.8f * MathF.Abs(MathF.Sin((float)(totalTime.ElapsedMilliseconds * Math.PI / 3000)));
            sphere2.Centers.Ys = Avx.Add(baseY, Avx.SetAllVector256(dy2));

            // Render the scene
            renderingTime.Reset();
            renderingTime.Start();

            fixed (int* ptr = rgbBuffer)
            {
                packetTracer.RenderVectorized(scene, ptr);
            }

            renderingTime.Stop();
            framesPerSecond = (1000.0 / renderingTime.ElapsedMilliseconds);
        }
    }

    public bool Run()
    {
        RenderLoop(MaxIterations);
        Console.WriteLine("{0} frames, {1} frames/sec",
            frames,
            framesPerSecond.ToString("F2"));
        return true;
    }

    private unsafe void RenderTo(string fileName, bool wirteToFile)
    {
        var packetTracer = new Packet256Tracer(Width, Height);
        var scene = packetTracer.DefaultScene;
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        fixed (int* ptr = rgbBuffer)
        {
            packetTracer.RenderVectorized(scene, ptr);
        }
        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
           ts.Hours, ts.Minutes, ts.Seconds,
           ts.Milliseconds / 10);
        Console.WriteLine("RunTime " + elapsedTime);

        if (wirteToFile)
        {
            using (var file = new System.IO.StreamWriter(fileName))
            {
                file.WriteLine("P3");
                file.WriteLine(Width + " " + Height);
                file.WriteLine("255");

                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        // Each pixel has 3 fields (RGB)
                        int pos = (i * Width + j) * 3;
                        file.Write(rgbBuffer[pos] + " " + rgbBuffer[pos + 1] + " " + rgbBuffer[pos + 2] + " ");
                    }
                    file.WriteLine();
                }
            }
        }
    }

}
