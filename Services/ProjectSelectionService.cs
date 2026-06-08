namespace DirectorComercialIA.Services;

public class ProjectSelectionService
{
    public string SelectedProjectGid { get; private set; } = "1215050986054369";

    public IReadOnlyList<AsanaProjectOption> Projects { get; } =
    [
        new("General", "736297665014865"),
        new("General International", "1215050986054369")
    ];

    public event Action? Changed;

    public void SetSelectedProject(string projectGid, bool notify = true)
    {
        SelectedProjectGid = projectGid;
        if (notify)
        {
            Changed?.Invoke();
        }
    }

    public string GetSelectedProjectName()
    {
        return Projects.FirstOrDefault(p => p.Gid == SelectedProjectGid)?.Name ?? SelectedProjectGid;
    }
}

public sealed record AsanaProjectOption(string Name, string Gid);
