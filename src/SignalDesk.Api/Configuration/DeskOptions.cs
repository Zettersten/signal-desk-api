namespace SignalDesk.Api.Configuration;

public sealed class DeskOptions
{
    public string Mode { get; set; } = "sample";
    public string DatabasePath { get; set; } = "../../data/runtime/signal-desk.db";
    public bool EnableCollectors { get; set; }
    public bool EnableLiveAi { get; set; }
    public decimal DailyBudgetUsd { get; set; }
    public int DailySearchQueries { get; set; } = 50;
    public int DailyDocuments { get; set; } = 50;
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173", "tauri://localhost", "http://tauri.localhost", "https://tauri.localhost"];
}
