using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;

namespace Finance.Mcp;

public class McpServer : IMcpServer
{
    private static readonly JsonSerializerOptions SerialiserOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> BusinessLogicFields =
        new(StringComparer.OrdinalIgnoreCase) { "categoryType", "transactionType" };

    private readonly IMcpContextStore _store;
    private readonly IContextRedactor _redactor;
    private readonly IMcpIterationLogger _logger;
    private readonly ICategoryService _categoryService;
    private readonly ITransactionService _transactionService;
    private readonly int _maxIterations;

    private readonly Dictionary<string, string> _pendingActions = new();
    private readonly Dictionary<string, string> _resolvedActions = new();
    private readonly Dictionary<string, AgentResult> _receivedResults = new();
    private readonly Dictionary<string, int> _iterationCounts = new();

    public McpServer(
        IMcpContextStore store,
        IContextRedactor redactor,
        IMcpIterationLogger logger,
        ICategoryService categoryService,
        ITransactionService transactionService,
        int maxIterations = 3)
    {
        _store = store;
        _redactor = redactor;
        _logger = logger;
        _categoryService = categoryService;
        _transactionService = transactionService;
        _maxIterations = maxIterations;
    }

    public string SendContext(
        IReadOnlyList<PendingTransactionItem> pendingTransactions,
        IReadOnlyList<int> ambiguousIds,
        int? ttlMinutes = null)
    {
        if (pendingTransactions.Count == 0)
            throw new ArgumentException("PendingTransactions must not be empty.", nameof(pendingTransactions));

        if (ambiguousIds.Count == 0)
            throw new ArgumentException("AmbiguousIds must not be empty.", nameof(ambiguousIds));

        var pendingIds = pendingTransactions.Select(t => t.Id).ToHashSet();
        if (!ambiguousIds.All(id => pendingIds.Contains(id)))
            throw new ArgumentException(
                "All ambiguous IDs must reference a pending transaction.", nameof(ambiguousIds));

        var categoryMappings = _categoryService.GetAll()
            .Select(c => new CategoryMappingItem(c.Id, c.Name, c.Type.ToString()))
            .ToList()
            .AsReadOnly();

        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var context = new McpContext(
            Id: id,
            Version: 1,
            Ttl: now.AddMinutes(ttlMinutes ?? 30),
            CategoryMappings: categoryMappings,
            PendingTransactions: pendingTransactions,
            Reconciliation: new ReconciliationContext(
                AmbiguousTransactionIds: ambiguousIds,
                SuggestedCategoryIds: new Dictionary<int, IReadOnlyList<int>>(),
                DecisionStatus: ContextStatus.Active),
            Status: ContextStatus.Active,
            CreatedAt: now);

        _store.Set(id, context);
        _iterationCounts[id] = 0;
        return id;
    }

    public AgentActionRequest RequestAction(string contextId, ActionType actionType)
    {
        var context = GetActiveContext(contextId);

        var current = _iterationCounts.GetValueOrDefault(contextId, 0);
        if (current >= _maxIterations)
        {
            // Auto-rollback and signal loop exhaustion via a sentinel action
            Rollback(contextId, "max iterations exceeded");
            var exhaustedActionId = $"exhausted-{Guid.NewGuid():N}";
            return new AgentActionRequest(
                ActionId: exhaustedActionId,
                ContextId: contextId,
                ActionType: actionType,
                IterationNumber: current + 1,
                PromptText: "[LOOP EXHAUSTED]",
                ContextFields: new Dictionary<string, object> { ["loopExhausted"] = true });
        }

        _iterationCounts[contextId] = current + 1;

        var redacted = _redactor.Redact(context);
        var contextFields = BuildContextFields(redacted);
        var promptText = BuildPromptText(redacted, actionType, current + 1);
        var actionId = Guid.NewGuid().ToString("N");

        _pendingActions[actionId] = contextId;

        return new AgentActionRequest(
            ActionId: actionId,
            ContextId: contextId,
            ActionType: actionType,
            IterationNumber: current + 1,
            PromptText: promptText,
            ContextFields: contextFields);
    }

    public void ReceiveResult(AgentResult result)
    {
        if (!_pendingActions.TryGetValue(result.ActionId, out var contextId))
            throw new InvalidOperationException(
                $"Action '{result.ActionId}' not found or already resolved.");

        var context = _store.Get(contextId)!;
        ValidateCategoryCompatibility(result.ProposedChanges, context);

        _resolvedActions[result.ActionId] = contextId;
        _receivedResults[result.ActionId] = result;
        _pendingActions.Remove(result.ActionId);
    }

    private static void ValidateCategoryCompatibility(
        IReadOnlyList<ProposedChange> changes,
        McpContext context)
    {
        var txById = context.PendingTransactions.ToDictionary(t => t.Id);
        var catById = context.CategoryMappings.ToDictionary(c => c.Id);

        foreach (var change in changes)
        {
            if (!change.TargetField.Equals("categoryId", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!int.TryParse(change.ProposedValue?.ToString(), out var categoryId))
                throw new InvalidOperationException(
                    $"ProposedValue '{change.ProposedValue}' for targetField 'categoryId' is not a valid integer.");

            if (!catById.TryGetValue(categoryId, out var category))
                throw new InvalidOperationException(
                    $"Category id={categoryId} does not exist.");

            if (!txById.TryGetValue(change.TransactionId, out var tx))
                throw new InvalidOperationException(
                    $"Transaction id={change.TransactionId} is not in this context.");

            var compatible = (tx.TransactionType, category.Type) switch
            {
                ("Expense", "Expense") => true,
                ("Expense", "Both")    => true,
                ("Income",  "Income")  => true,
                ("Income",  "Both")    => true,
                _                      => false
            };

            if (!compatible)
                throw new InvalidOperationException(
                    $"Category '{category.Name}' (type={category.Type}) is incompatible with " +
                    $"transaction id={tx.Id} (type={tx.TransactionType}).");
        }
    }

    public ConfirmResult Confirm(string contextId)
    {
        var context = _store.Get(contextId)
            ?? throw new InvalidOperationException($"Context '{contextId}' not found or expired.");

        if (context.Status == ContextStatus.Confirmed)
            return new ConfirmResult(RequiresApproval: false, Status: ContextStatus.Confirmed);

        if (context.Status == ContextStatus.RolledBack || context.Status == ContextStatus.Expired)
            throw new InvalidOperationException(
                $"Cannot confirm context in state '{context.Status}'.");

        var latestResult = _resolvedActions
            .Where(kv => kv.Value == contextId)
            .Select(kv => _receivedResults.GetValueOrDefault(kv.Key))
            .LastOrDefault(r => r is not null);

        var touchesBusinessLogic = latestResult?.ProposedChanges
            .Any(c => BusinessLogicFields.Contains(c.TargetField)) ?? false;

        ContextStatus newStatus;
        bool requiresApproval;

        if (touchesBusinessLogic)
        {
            newStatus = ContextStatus.PendingApproval;
            requiresApproval = true;
        }
        else
        {
            newStatus = ContextStatus.Confirmed;
            requiresApproval = false;
        }

        _store.Set(contextId, context.WithStatus(newStatus));

        if (newStatus == ContextStatus.Confirmed)
            ApplyProposedChanges(contextId);

        var snapshot = TakeSnapshot(contextId);
        _logger.Log(new IterationLogEntry(
            Timestamp: DateTime.UtcNow.ToString("O"),
            Prompt: latestResult?.Explanation ?? string.Empty,
            ContextSnapshotHash: snapshot.Hash,
            ModelName: "unknown",
            AgentOutput: SerialiseResult(latestResult),
            AcceptedDiff: requiresApproval ? null : SerialiseChanges(latestResult?.ProposedChanges),
            DecisionReason: requiresApproval ? "pending human approval" : "confirmed"));

        return new ConfirmResult(RequiresApproval: requiresApproval, Status: newStatus);
    }

    public void Rollback(string contextId, string reason)
    {
        var context = _store.Get(contextId)
            ?? throw new InvalidOperationException($"Context '{contextId}' not found or expired.");

        if (context.Status == ContextStatus.Confirmed)
            throw new InvalidOperationException("Cannot roll back a confirmed context.");

        _store.Set(contextId, context.WithStatus(ContextStatus.RolledBack));

        var snapshot = TakeSnapshot(contextId);
        _logger.Log(new IterationLogEntry(
            Timestamp: DateTime.UtcNow.ToString("O"),
            Prompt: string.Empty,
            ContextSnapshotHash: snapshot.Hash,
            ModelName: "unknown",
            AgentOutput: string.Empty,
            AcceptedDiff: null,
            DecisionReason: reason));
    }

    public void ApproveConfirm(string contextId)
    {
        var context = _store.Get(contextId)
            ?? throw new InvalidOperationException($"Context '{contextId}' not found or expired.");

        if (context.Status != ContextStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Context '{contextId}' is not in PendingApproval state (current: {context.Status}).");

        _store.Set(contextId, context.WithStatus(ContextStatus.Confirmed));
        ApplyProposedChanges(contextId);

        var latestResult = _resolvedActions
            .Where(kv => kv.Value == contextId)
            .Select(kv => _receivedResults.GetValueOrDefault(kv.Key))
            .LastOrDefault(r => r is not null);

        var snapshot = TakeSnapshot(contextId);
        _logger.Log(new IterationLogEntry(
            Timestamp: DateTime.UtcNow.ToString("O"),
            Prompt: latestResult?.Explanation ?? string.Empty,
            ContextSnapshotHash: snapshot.Hash,
            ModelName: "unknown",
            AgentOutput: SerialiseResult(latestResult),
            AcceptedDiff: SerialiseChanges(latestResult?.ProposedChanges),
            DecisionReason: "human approved"));
    }

    public ContextSnapshot TakeSnapshot(string contextId)
    {
        var context = _store.Get(contextId)
            ?? throw new InvalidOperationException($"Context '{contextId}' not found or expired.");

        var redacted = _redactor.Redact(context);
        var json = JsonSerializer.Serialize(redacted, SerialiserOptions);
        var hash = ComputeHash(json);

        return new ContextSnapshot(
            ContextId: contextId,
            SerialisedAt: DateTime.UtcNow,
            Hash: hash,
            RedactedJson: json);
    }

    private void ApplyProposedChanges(string contextId)
    {
        var context = _store.Get(contextId)!;

        var latestResult = _resolvedActions
            .Where(kv => kv.Value == contextId)
            .Select(kv => _receivedResults.GetValueOrDefault(kv.Key))
            .LastOrDefault(r => r is not null);

        if (latestResult is null)
            return;

        var changesByTx = latestResult.ProposedChanges
            .GroupBy(c => c.TransactionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var tx in context.PendingTransactions)
        {
            changesByTx.TryGetValue(tx.Id, out var changes);
            changes ??= [];

            var categoryChange = changes.FirstOrDefault(c =>
                c.TargetField.Equals("categoryId", StringComparison.OrdinalIgnoreCase));

            var typeChange = changes.FirstOrDefault(c =>
                c.TargetField.Equals("transactionType", StringComparison.OrdinalIgnoreCase));

            if (categoryChange is null && typeChange is null)
                continue;

            var type = Enum.TryParse<TransactionType>(
                typeChange?.ProposedValue?.ToString() ?? tx.TransactionType,
                ignoreCase: true,
                out var parsed)
                ? parsed
                : Enum.Parse<TransactionType>(tx.TransactionType, ignoreCase: true);

            IReadOnlyList<int> categoryIds = categoryChange is not null
                && int.TryParse(categoryChange.ProposedValue?.ToString(), out var categoryId)
                ? [categoryId]
                : [];

            _transactionService.Create(new TransactionCreateRequest(
                Timestamp: tx.Date,
                Description: tx.Description,
                Amount: tx.Amount,
                Type: type,
                CategoryIds: categoryIds));
        }
    }

    private McpContext GetActiveContext(string contextId)
    {
        var context = _store.Get(contextId);
        if (context is null)
            throw new InvalidOperationException($"Context '{contextId}' not found or expired.");

        if (context.Status is ContextStatus.Confirmed or ContextStatus.RolledBack or ContextStatus.Expired)
            throw new InvalidOperationException(
                $"Context '{contextId}' is in terminal state '{context.Status}'.");

        return context;
    }

    private static string ComputeHash(string json)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, object> BuildContextFields(McpContext redacted) =>
        new Dictionary<string, object>
        {
            ["contextId"] = redacted.Id,
            ["ambiguousTransactionIds"] = redacted.Reconciliation.AmbiguousTransactionIds,
            ["categoryMappings"] = redacted.CategoryMappings
                .Select(c => new { c.Id, c.Name, c.Type })
                .ToList()
        };

    private static string BuildPromptText(McpContext ctx, ActionType actionType, int iteration)
    {
        var ids = string.Join(", ", ctx.Reconciliation.AmbiguousTransactionIds);
        return $"[Iteration {iteration}] {actionType}: Suggest categories for transaction IDs [{ids}].";
    }

    private static string SerialiseResult(AgentResult? result) =>
        result is null ? string.Empty : JsonSerializer.Serialize(result, SerialiserOptions);

    private static string? SerialiseChanges(IReadOnlyList<ProposedChange>? changes) =>
        changes is null or { Count: 0 }
            ? null
            : JsonSerializer.Serialize(changes, SerialiserOptions);
}
