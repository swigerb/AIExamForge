using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.References;

namespace AIExamForge.Application.Validation;

internal sealed class AiExamForgeSchemaReferenceResolver : JsonReferenceResolver
{
    private const string AiexamforgePrefix = "https://aiexamforge/schemas/";

    private readonly ILogger _logger;
    private readonly string _schemasBasePath;

    // Cache loaded documents by absolute URI.
    private readonly ConcurrentDictionary<string, string> _documentJsonCache = new(StringComparer.OrdinalIgnoreCase);

    public AiExamForgeSchemaReferenceResolver(object rootObject, ILogger logger, string schemasBasePath)
        : base(new JsonSchemaAppender(rootObject, new DefaultTypeNameGenerator()))
    {
        _logger = logger;
        _schemasBasePath = schemasBasePath;
    }

    public override async Task<IJsonReference> ResolveUrlReferenceAsync(string url, CancellationToken cancellationToken)
    {
        if (TryMapToLocalSchemaPath(url, out var filePath))
        {
            var json = _documentJsonCache.GetOrAdd(url, _ =>
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Schema file not found for '{url}' at '{filePath}'.", filePath);
                }

                return File.ReadAllText(filePath);
            });

            // Parse the referenced schema using this same resolver for nested refs.
            var schema = await JsonSchema.FromJsonAsync(
                json,
                documentPath: url,
                referenceResolverFactory: root => new AiExamForgeSchemaReferenceResolver(root, _logger, _schemasBasePath),
                cancellationToken: cancellationToken);

            return schema;
        }

        return await base.ResolveUrlReferenceAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private bool TryMapToLocalSchemaPath(string url, out string filePath)
    {
        filePath = string.Empty;

        if (!url.StartsWith(AiexamforgePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // URL forms we currently use:
        // - https://aiexamforge/schemas/common/choice.schema.json
        // - https://aiexamforge/schemas/common/question-base.schema.json
        // - https://aiexamforge/schemas/item-types/M1.schema.json
        // - https://aiexamforge/schemas/question.schema.json
        var relative = url[AiexamforgePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);

        if (relative.StartsWith($"item-types{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            // Item type schemas live under docs/schemas/common/item-types
            filePath = Path.Combine(_schemasBasePath, "item-types", relative[("item-types".Length + 1)..]);
            return true;
        }

        if (relative.StartsWith($"common{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            // Common schemas live under docs/schemas/common
            filePath = Path.Combine(_schemasBasePath, relative[("common".Length + 1)..]);
            return true;
        }

        // Fallback: treat as relative to schemas base.
        filePath = Path.Combine(_schemasBasePath, relative);
        return true;
    }
}
