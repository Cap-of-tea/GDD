using System.IO;

namespace GDD.Mcp.Tools;

public static class HelpTools
{
    public static void Register(McpToolRegistry registry)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_manual",
                Description =
                    "Returns the full GDD manual: setup, all 36 tool descriptions with parameters, " +
                    "agent rules, anti-patterns, and real-world workflow examples. " +
                    "Call this once at the start of a session to learn how to use GDD effectively.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        section = new
                        {
                            type = "string",
                            description =
                                "Optional section filter. Returns only the matching section. " +
                                "Values: setup, tools, rules, examples, architecture, config. " +
                                "Omit to get the full manual.",
                            @enum = new[] { "setup", "tools", "rules", "examples", "architecture", "config" }
                        }
                    }
                }
            },
            async args =>
            {
                var manual = LoadManual();
                if (string.IsNullOrEmpty(manual))
                    return McpResult.Error("GDD-MANUAL.md not found");

                string? section = null;
                if (args?.TryGetProperty("section", out var sectionEl) == true)
                    section = sectionEl.GetString();

                if (section is not null)
                {
                    var filtered = ExtractSection(manual, section);
                    if (filtered is null)
                        return McpResult.Error($"Section '{section}' not found. Available: setup, tools, rules, examples, architecture, config");
                    return McpResult.Text(filtered);
                }

                await Task.CompletedTask;
                return McpResult.Text(manual);
            });
    }

    private static string? LoadManual()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "GDD-MANUAL.md");
        if (File.Exists(path))
            return File.ReadAllText(path);

        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "GDD-MANUAL.md");
        if (File.Exists(path))
            return File.ReadAllText(path);

        return null;
    }

    private static readonly Dictionary<string, string> SectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setup"] = "## 2. Setup & Launch",
        ["tools"] = "## 3. Tool Reference",
        ["rules"] = "## 4. Claude Agent Rules",
        ["examples"] = "## 5. Real-World Workflow Examples",
        ["architecture"] = "## 6. Architecture Overview",
        ["config"] = "## 7. Configuration",
    };

    private static string? ExtractSection(string manual, string section)
    {
        if (!SectionHeaders.TryGetValue(section, out var header))
            return null;

        var start = manual.IndexOf(header, StringComparison.Ordinal);
        if (start < 0) return null;

        var nextSection = manual.IndexOf("\n## ", start + header.Length, StringComparison.Ordinal);
        return nextSection < 0
            ? manual[start..]
            : manual[start..nextSection].TrimEnd();
    }
}
