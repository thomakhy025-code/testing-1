using System;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Starts the local MCP HTTP server and connects this Editor to it.
/// Later launches are handled by MCP for Unity's own auto-start preference.
/// </summary>
[InitializeOnLoad]
public static class McpConnectionBootstrap
{
    const string SessionKey = "McpConnectionBootstrap.Handled.Py312";
    const string UvxPath = @"C:\Users\User\.local\bin\uvx.exe";

    static bool _launchRequested;
    static double _giveUpAt;

    static McpConnectionBootstrap()
    {
        EditorApplication.delayCall += Begin;
    }

    static void Begin()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (SessionState.GetBool("HttpAutoStartHandler.SessionInitialized", false))
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        try
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorPrefs.SetBool("MCPForUnity.HttpServerLaunchConfirmed", true);
            EditorPrefs.SetBool("MCPForUnity.SetupCompleted", true);
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
            if (System.IO.File.Exists(UvxPath))
                EditorPrefs.SetString("MCPForUnity.UvxPath", UvxPath);

            EditorConfigurationCache.Instance.SetUseHttpTransport(true);
            var summary = MCPServiceLocator.Client.ConfigureAllDetectedClients();
            Debug.Log("[MCP Bootstrap] " + summary.GetSummaryMessage());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MCP Bootstrap] setup: " + ex.Message);
        }

        SessionState.SetBool(SessionKey, true);
        _giveUpAt = EditorApplication.timeSinceStartup + 240.0;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            var server = MCPServiceLocator.Server;
            var bridge = MCPServiceLocator.Bridge;

            if (bridge.IsRunning)
            {
                Debug.Log("[MCP Bootstrap] Unity bridge is connected.");
                EditorApplication.update -= Tick;
                return;
            }

            if (!_launchRequested && !server.IsLocalHttpServerReachable())
            {
                _launchRequested = true;
                bool started = server.StartLocalHttpServer(quiet: true);
                Debug.Log("[MCP Bootstrap] HTTP server launch requested: " + started);
                return;
            }

            if (server.IsLocalHttpServerReachable())
            {
                _launchRequested = true;
                Debug.Log("[MCP Bootstrap] HTTP server is up. Connecting Unity...");
                EditorApplication.update -= Tick;
                ConnectBridge();
                return;
            }

            if (EditorApplication.timeSinceStartup > _giveUpAt)
            {
                Debug.LogWarning("[MCP Bootstrap] HTTP server did not become reachable in time.");
                server.LogLocalHttpServerLaunchFailure();
                EditorApplication.update -= Tick;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MCP Bootstrap] " + ex.Message);
        }
    }

    static async void ConnectBridge()
    {
        try
        {
            bool connected = await MCPServiceLocator.Bridge.StartAsync();
            var verify = await MCPServiceLocator.Bridge.VerifyAsync();
            Debug.Log("[MCP Bootstrap] connected=" + connected + " verify=" + verify.Success + " " + verify.Message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[MCP Bootstrap] connect failed: " + ex.Message);
        }
    }
}
