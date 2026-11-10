using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebSiteBuild
{
    public static void Build()
    {
        var output = Path.GetFullPath("docs");
        if (Directory.Exists(output))
            Directory.Delete(output, true);
        Directory.CreateDirectory(output);

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.memorySize = 256;

        var options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        options.locationPathName = output;
        options.target = BuildTarget.WebGL;
        options.options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("WebGL build failed: " + report.summary.result + " errors=" + report.summary.totalErrors);
            EditorApplication.Exit(1);
            return;
        }

        File.WriteAllText(Path.Combine(output, ".nojekyll"), "");
        Debug.Log("WebGL build succeeded bytes=" + report.summary.totalSize);
        EditorApplication.Exit(0);
    }
}
