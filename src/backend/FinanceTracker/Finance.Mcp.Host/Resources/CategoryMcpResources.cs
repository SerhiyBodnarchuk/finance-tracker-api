using System.Text.Json;
using Finance.Business.Services;
using ModelContextProtocol.Server;

namespace Finance.Mcp.Host.Resources;

[McpServerResourceType]
public class CategoryMcpResources(ICategoryService categoryService)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [McpServerResource(UriTemplate = "finance://categories", Name = "All Categories", MimeType = "application/json")]
    public string GetAllCategories() =>
        JsonSerializer.Serialize(categoryService.GetAll(), Json);

    [McpServerResource(UriTemplate = "finance://categories/{id}", Name = "Category by ID", MimeType = "application/json")]
    public string GetCategoryById(int id)
    {
        var category = categoryService.GetById(id);
        if (category is null)
            return JsonSerializer.Serialize(new { error = $"Category {id} not found" }, Json);

        return JsonSerializer.Serialize(category, Json);
    }
}
