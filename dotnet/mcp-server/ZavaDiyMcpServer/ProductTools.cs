using System.ComponentModel;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace ZavaDiyMcpServer;

[McpServerToolType]
public class ProductTools
{
    private readonly IDatabaseService _db;

    public ProductTools(IDatabaseService db)
    {
        _db = db;
    }

    [McpServerTool, Description("Look up product details including pricing, category, and description. Can filter by product ID, SKU, or category.")]
    public async Task<string> GetProductInfo(
        [Description("Product ID to look up. Optional.")] int? productId = null,
        [Description("Product SKU to look up. Optional.")] string? sku = null,
        [Description("Category name to filter products by. Optional.")] string? category = null,
        [Description("Maximum number of products to return. Defaults to 50.")] int limit = 50)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = new StringBuilder("""
            SELECT
                p.product_id,
                p.product_name,
                p.sku,
                p.product_description,
                p.cost AS cost_price,
                p.base_price AS retail_price,
                pt.type_name,
                c.category_name
            FROM retail.products p
            JOIN retail.product_types pt ON pt.type_id = p.type_id
            JOIN retail.categories c ON c.category_id = pt.category_id
            WHERE 1=1
            """);

        DbParameter CreateParam(string name, object value)
        {
            var p = connection.CreateCommand().CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            return p;
        }

        var parameters = new List<DbParameter>();

        if (productId.HasValue)
        {
            sql.Append(" AND p.product_id = @productId");
            parameters.Add(CreateParam("@productId", productId.Value));
        }

        if (sku is not null)
        {
            sql.Append(" AND p.sku = @sku");
            parameters.Add(CreateParam("@sku", sku));
        }

        if (category is not null)
        {
            sql.Append(" AND c.category_name ILIKE @category");
            parameters.Add(CreateParam("@category", category));
        }

        sql.Append(" ORDER BY c.category_name, pt.type_name, p.product_name");
        sql.Append(" LIMIT @limit");
        parameters.Add(CreateParam("@limit", limit));

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var p in parameters) cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            results.Add(new Dictionary<string, object?>
            {
                ["product_id"] = reader.GetInt32(0),
                ["product_name"] = reader.GetString(1),
                ["sku"] = reader.GetString(2),
                ["description"] = reader.GetString(3),
                ["cost_price"] = reader.GetDecimal(4),
                ["retail_price"] = reader.GetDecimal(5),
                ["product_type"] = reader.GetString(6),
                ["category"] = reader.GetString(7)
            });
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Search products by text query against product names and descriptions. Returns matching products ranked by relevance.")]
    public async Task<string> SearchProducts(
        [Description("The search text to match against product names and descriptions.")] string queryText,
        [Description("Maximum number of results to return. Defaults to 10.")] int topK = 10)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = """
            SELECT
                p.product_id,
                p.product_name,
                p.sku,
                p.product_description,
                p.cost AS cost_price,
                p.base_price AS retail_price,
                pt.type_name,
                c.category_name
            FROM retail.products p
            JOIN retail.product_types pt ON pt.type_id = p.type_id
            JOIN retail.categories c ON c.category_id = pt.category_id
            WHERE p.product_name ILIKE @pattern OR p.product_description ILIKE @pattern
            ORDER BY
                CASE WHEN p.product_name ILIKE @pattern THEN 0 ELSE 1 END,
                p.product_name
            LIMIT @topK
            """;

        var pattern = $"%{queryText}%";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var pPattern = cmd.CreateParameter();
        pPattern.ParameterName = "@pattern";
        pPattern.Value = pattern;
        cmd.Parameters.Add(pPattern);
        var pTopK = cmd.CreateParameter();
        pTopK.ParameterName = "@topK";
        pTopK.Value = topK;
        cmd.Parameters.Add(pTopK);

        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            results.Add(new Dictionary<string, object?>
            {
                ["product_id"] = reader.GetInt32(0),
                ["product_name"] = reader.GetString(1),
                ["sku"] = reader.GetString(2),
                ["description"] = reader.GetString(3),
                ["cost_price"] = reader.GetDecimal(4),
                ["retail_price"] = reader.GetDecimal(5),
                ["product_type"] = reader.GetString(6),
                ["category"] = reader.GetString(7)
            });
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }
}
