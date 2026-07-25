using System.Text.Json;

namespace Andy.Engine;

/// <summary>Lifecycle state for one structured plan item.</summary>
public enum AgentPlanItemStatus
{
    /// <summary>Work has not started.</summary>
    Pending,
    /// <summary>Work is currently active.</summary>
    InProgress,
    /// <summary>Work finished and cannot be reopened.</summary>
    Completed,
}

/// <summary>One stable, user-visible unit of work in an agent plan.</summary>
public sealed record AgentPlanItem
{
    /// <summary>Stable identifier reused across plan revisions.</summary>
    public required string Id { get; init; }
    /// <summary>Concise task description.</summary>
    public required string Text { get; init; }
    /// <summary>Current task lifecycle state.</summary>
    public AgentPlanItemStatus Status { get; init; }
}

/// <summary>Immutable authoritative plan state at one revision.</summary>
public sealed record AgentPlanSnapshot
{
    /// <summary>Monotonically increasing revision, starting at one.</summary>
    public int Revision { get; init; }
    /// <summary>Items in the model's intended execution/display order.</summary>
    public IReadOnlyList<AgentPlanItem> Items { get; init; } = Array.Empty<AgentPlanItem>();
}

/// <summary>Describes how a plan snapshot changed.</summary>
public enum AgentPlanChangeKind
{
    /// <summary>The first plan was created.</summary>
    Created,
    /// <summary>An existing plan changed.</summary>
    Updated,
    /// <summary>The plan was explicitly replaced with an empty item list.</summary>
    Cleared,
}

/// <summary>Event payload carrying a complete plan snapshot.</summary>
public sealed class AgentPlanChangedEventArgs : EventArgs
{
    /// <summary>The kind of plan transition.</summary>
    public required AgentPlanChangeKind Kind { get; init; }
    /// <summary>The complete plan after the transition.</summary>
    public required AgentPlanSnapshot Plan { get; init; }
    /// <summary>Time at which the engine applied the transition.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class AgentPlanState
{
    private AgentPlanSnapshot? _current;

    public AgentPlanSnapshot? Current => _current;

    public PlanApplyResult Apply(string argumentsJson)
    {
        IReadOnlyList<AgentPlanItem> items;
        try
        {
            items = ParseItems(argumentsJson);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return PlanApplyResult.Failure(ex.Message);
        }

        var prior = _current;
        if (prior != null)
        {
            var priorById = prior.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (priorById.TryGetValue(item.Id, out var previous) &&
                    previous.Status == AgentPlanItemStatus.Completed &&
                    item.Status != AgentPlanItemStatus.Completed)
                {
                    return PlanApplyResult.Failure(
                        $"Plan item '{item.Id}' is completed and cannot transition back to {StatusName(item.Status)}.");
                }
            }
        }

        if (prior != null && PlansEqual(prior.Items, items))
            return PlanApplyResult.Success(prior, change: null);

        var revision = (prior?.Revision ?? 0) + 1;
        var snapshot = new AgentPlanSnapshot
        {
            Revision = revision,
            Items = items.Select(CloneItem).ToArray(),
        };
        _current = snapshot;
        var kind = prior == null
            ? AgentPlanChangeKind.Created
            : items.Count == 0
                ? AgentPlanChangeKind.Cleared
                : AgentPlanChangeKind.Updated;
        return PlanApplyResult.Success(snapshot, kind);
    }

    public void Restore(AgentPlanSnapshot? plan)
    {
        if (plan == null)
        {
            _current = null;
            return;
        }

        ValidateItems(plan.Items);
        if (plan.Revision < 1)
            throw new ArgumentException("A restored plan revision must be at least 1.");
        _current = new AgentPlanSnapshot
        {
            Revision = plan.Revision,
            Items = plan.Items.Select(CloneItem).ToArray(),
        };
    }

    public void Clear() => _current = null;

    private static IReadOnlyList<AgentPlanItem> ParseItems(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            throw new ArgumentException("update_plan requires a JSON object with an items array.");

        using var document = JsonDocument.Parse(argumentsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("update_plan requires an items array.");
        }

        var items = new List<AgentPlanItem>();
        foreach (var element in itemsElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Each plan item must be an object.");

            var id = RequiredString(element, "id");
            var text = RequiredString(element, "text");
            var statusText = RequiredString(element, "status");
            var status = statusText switch
            {
                "pending" => AgentPlanItemStatus.Pending,
                "in_progress" => AgentPlanItemStatus.InProgress,
                "completed" => AgentPlanItemStatus.Completed,
                _ => throw new ArgumentException(
                    $"Plan item '{id}' has invalid status '{statusText}'. " +
                    "Expected pending, in_progress, or completed."),
            };
            items.Add(new AgentPlanItem { Id = id, Text = text, Status = status });
        }

        ValidateItems(items);
        return items;
    }

    private static void ValidateItems(IReadOnlyList<AgentPlanItem> items)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var inProgress = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new ArgumentException("Plan item ids cannot be blank.");
            if (!ids.Add(item.Id))
                throw new ArgumentException($"Duplicate plan item id '{item.Id}'.");
            if (string.IsNullOrWhiteSpace(item.Text))
                throw new ArgumentException($"Plan item '{item.Id}' has blank text.");
            if (!Enum.IsDefined(item.Status))
                throw new ArgumentException($"Plan item '{item.Id}' has an invalid status.");
            if (item.Status == AgentPlanItemStatus.InProgress)
                inProgress++;
        }

        if (inProgress > 1)
            throw new ArgumentException("At most one plan item can be in_progress.");
    }

    private static string RequiredString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ArgumentException($"Plan item property '{propertyName}' must be a non-empty string.");
        }
        return value.GetString()!.Trim();
    }

    private static AgentPlanItem CloneItem(AgentPlanItem item) => new()
    {
        Id = item.Id,
        Text = item.Text,
        Status = item.Status,
    };

    private static bool PlansEqual(
        IReadOnlyList<AgentPlanItem> left,
        IReadOnlyList<AgentPlanItem> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Text == pair.Second.Text &&
            pair.First.Status == pair.Second.Status);

    internal static string StatusName(AgentPlanItemStatus status) => status switch
    {
        AgentPlanItemStatus.Pending => "pending",
        AgentPlanItemStatus.InProgress => "in_progress",
        AgentPlanItemStatus.Completed => "completed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

internal sealed record PlanApplyResult(
    bool IsSuccessful,
    AgentPlanSnapshot? Plan,
    AgentPlanChangeKind? Change,
    string? Error)
{
    public static PlanApplyResult Success(
        AgentPlanSnapshot plan,
        AgentPlanChangeKind? change) =>
        new(true, plan, change, null);

    public static PlanApplyResult Failure(string error) =>
        new(false, null, null, error);
}
