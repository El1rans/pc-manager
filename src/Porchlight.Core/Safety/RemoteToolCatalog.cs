using Porchlight.Core.Components;

namespace Porchlight.Core.Safety;

/// <summary>The data table of remote-control tools Porchlight recognises. Add a row to teach it a new one.</summary>
public static class RemoteToolCatalog
{
    public const string QuickAssistId = "quickassist";

    public static IReadOnlyList<RemoteToolDefinition> All { get; } =
    [
        new("teamviewer", "TeamViewer",
            ["TeamViewer", "TeamViewer_Service", "tv_w32", "tv_x64"], ["TeamViewer"], ["TeamViewer"]),
        new(ComponentIds.AnyDesk, "AnyDesk", ["AnyDesk"], ["AnyDesk"], ["AnyDesk"]),
        new("rustdesk", "RustDesk", ["rustdesk"], ["RustDesk"], ["RustDesk"]),
        new("ultraviewer", "UltraViewer",
            ["UltraViewer_Desktop", "UltraViewer_Service"], ["UltraViewer"], ["UltraViewer"]),
        new("splashtop", "Splashtop",
            ["SRManager", "SRService", "SRServer", "strwinclt"], ["Splashtop"], ["Splashtop", "SplashtopRemote"]),
        new("chromeremotedesktop", "Chrome Remote Desktop",
            ["remoting_host", "remote_assistance_host"], ["Chrome Remote Desktop"], ["chromoting", "Chrome Remote Desktop"]),
        new("logmein", "LogMeIn",
            ["LogMeIn", "LMIGuardianSvc", "LogMeInSystray"], ["LogMeIn"], ["LogMeIn", "LMIGuardian"]),
        new("connectwise", "ConnectWise ScreenConnect",
            ["ScreenConnect.ClientService", "ScreenConnect.WindowsClient", "ConnectWiseControl.Client"],
            ["ScreenConnect Client", "ConnectWise Control"], ["ScreenConnect Client"]),
        new("supremo", "Supremo", ["Supremo", "SupremoService"], ["Supremo"], ["Supremo"]),

        // Built into Windows: no uninstall entry or service, so it only shows while it is open.
        new(QuickAssistId, "Quick Assist", ["quickassist"], [], []),
    ];
}
