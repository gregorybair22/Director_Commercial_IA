using System.Net.Http.Headers;
using System.Net.Http.Headers;
using System.Text.Json;
using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class AsanaService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly SyncLogService _syncLog;

    public AsanaService(HttpClient http, IConfiguration config, SyncLogService syncLog)
    {
        _http = http;
        _config = config;
        _syncLog = syncLog;

        _http.BaseAddress = new Uri("https://app.asana.com/api/1.0/");
        var token = _config["Asana:Token"];
        if (!string.IsNullOrWhiteSpace(token) && token != "PON_AQUI_TU_TOKEN_DE_ASANA")
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }
    }

    public async Task<List<CommercialTask>> GetTasksByProjectAsync(string projectGid)
    {
        var projectName = await GetProjectNameAsync(projectGid);
        var sections = await GetSectionsByProjectAsync(projectGid);

        var result = new List<CommercialTask>();
        foreach (var section in sections)
        {
            var items = await GetTasksFromSectionAsync(projectGid, projectName, section);
            result.AddRange(items);
        }

        return result;
    }

    public async Task AddTaskToProjectAsync(string taskGid, string projectGid)
    {
        var payload = JsonSerializer.Serialize(new { data = new { project = projectGid } });
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"tasks/{taskGid}/addProject", content);
        await EnsureSuccessWithBodyAsync(response);
    }

    public async Task AddTaskToSectionAsync(string taskGid, string sectionGid)
    {
        var payload = JsonSerializer.Serialize(new { data = new { task = taskGid } });
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"sections/{sectionGid}/addTask", content);
        await EnsureSuccessWithBodyAsync(response);
    }

    public async Task<List<AsanaSectionOption>> GetProjectSectionsAsync(string projectGid)
    {
        var sections = await GetSectionsByProjectAsync(projectGid);
        return sections.Select(s => new AsanaSectionOption(s.Gid, s.Name)).ToList();
    }

    public async Task RemoveTaskFromProjectAsync(string taskGid, string projectGid)
    {
        var payload = JsonSerializer.Serialize(new { data = new { project = projectGid } });
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"tasks/{taskGid}/removeProject", content);
        await EnsureSuccessWithBodyAsync(response);
    }

    private async Task<string> GetProjectNameAsync(string projectGid)
    {
        var root = await GetRootAsync($"projects/{projectGid}?opt_fields=name");
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("name", out var nameEl))
        {
            return nameEl.GetString() ?? projectGid;
        }

        return projectGid;
    }

    private async Task<List<AsanaSection>> GetSectionsByProjectAsync(string projectGid)
    {
        var sections = new List<AsanaSection>();
        string? offset = null;

        do
        {
            var url = $"projects/{projectGid}/sections?limit=100&opt_fields=gid,name";
            if (!string.IsNullOrWhiteSpace(offset))
            {
                url += $"&offset={Uri.EscapeDataString(offset)}";
            }

            var root = await GetRootAsync(url);
            foreach (var section in root.GetProperty("data").EnumerateArray())
            {
                sections.Add(new AsanaSection
                {
                    Gid = section.GetProperty("gid").GetString() ?? string.Empty,
                    Name = section.GetProperty("name").GetString() ?? string.Empty
                });
            }

            offset = GetOffset(root);
        } while (!string.IsNullOrWhiteSpace(offset));

        return sections;
    }

    private async Task<List<CommercialTask>> GetTasksFromSectionAsync(string projectGid, string projectName, AsanaSection section)
    {
        var tasks = new List<CommercialTask>();
        string? offset = null;

        do
        {
            var url = $"sections/{section.Gid}/tasks?limit=100" +
                      "&opt_fields=gid,name,notes,assignee.gid,assignee.name,completed,due_on,created_at,modified_at,completed_at,permalink_url,tags.gid,tags.name,custom_fields.gid,custom_fields.name,custom_fields.display_value,num_stories";

            if (!string.IsNullOrWhiteSpace(offset))
            {
                url += $"&offset={Uri.EscapeDataString(offset)}";
            }

            var root = await GetRootAsync(url);
            foreach (var item in root.GetProperty("data").EnumerateArray())
            {
                tasks.Add(MapTask(item, projectGid, projectName, section));
            }

            offset = GetOffset(root);
        } while (!string.IsNullOrWhiteSpace(offset));

        return tasks;
    }

    private static CommercialTask MapTask(JsonElement item, string projectGid, string projectName, AsanaSection section)
    {
        return new CommercialTask
        {
            AsanaTaskGid = item.GetProperty("gid").GetString(),
            Name = item.GetProperty("name").GetString() ?? string.Empty,
            Description = item.TryGetProperty("notes", out var notesEl) ? notesEl.GetString() : null,
            ProjectGid = projectGid,
            ProjectName = projectName,
            SectionGid = section.Gid,
            SectionName = section.Name,
            AssigneeName = item.TryGetProperty("assignee", out var assigneeEl) && assigneeEl.ValueKind != JsonValueKind.Null && assigneeEl.TryGetProperty("name", out var assigneeNameEl)
                ? assigneeNameEl.GetString()
                : null,
            AssigneeGid = item.TryGetProperty("assignee", out var assigneeEl2) && assigneeEl2.ValueKind != JsonValueKind.Null && assigneeEl2.TryGetProperty("gid", out var assigneeGidEl)
                ? assigneeGidEl.GetString()
                : null,
            DueOn = TryParseDate(item, "due_on"),
            CreatedAt = TryParseDate(item, "created_at"),
            ModifiedAt = TryParseDate(item, "modified_at") ?? DateTime.UtcNow,
            CompletedAt = TryParseDate(item, "completed_at"),
            IsCompleted = item.TryGetProperty("completed", out var completedEl) && completedEl.GetBoolean(),
            PermalinkUrl = item.TryGetProperty("permalink_url", out var linkEl) ? linkEl.GetString() : null,
            TagsJson = item.TryGetProperty("tags", out var tagsEl) ? tagsEl.GetRawText() : null,
            CustomFieldsJson = item.TryGetProperty("custom_fields", out var customEl) ? customEl.GetRawText() : null,
            StoryCount = item.TryGetProperty("num_stories", out var storiesEl) && storiesEl.ValueKind == JsonValueKind.Number ? storiesEl.GetInt32() : null,
            LastSyncAt = DateTime.UtcNow,
            Source = "Asana"
        };
    }

    private static DateTime? TryParseDate(JsonElement item, string property)
    {
        if (item.TryGetProperty(property, out var el) && el.ValueKind != JsonValueKind.Null && DateTime.TryParse(el.GetString(), out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static string? GetOffset(JsonElement root)
    {
        if (root.TryGetProperty("next_page", out var nextPage) && nextPage.ValueKind != JsonValueKind.Null && nextPage.TryGetProperty("offset", out var offset))
        {
            return offset.GetString();
        }

        return null;
    }

    private async Task<JsonElement> GetRootAsync(string url)
    {
        _syncLog.LogInfo($"HTTP GET: {_http.BaseAddress}{url}");
        var response = await _http.GetAsync(url);
        _syncLog.LogInfo($"HTTP Response: {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _syncLog.LogError($"Error body: {body}");
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Asana API error {(int)response.StatusCode}: {body}");
        }
    }

    private sealed class AsanaSection
    {
        public string Gid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

public sealed record AsanaSectionOption(string Gid, string Name);
