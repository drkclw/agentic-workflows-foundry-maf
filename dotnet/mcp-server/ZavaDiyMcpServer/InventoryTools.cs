using System.ComponentModel;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace ZavaDiyMcpServer;

[McpServerToolType]
public class InventoryTools
{
    private readonly IDatabaseService _db;

    public InventoryTools(IDatabaseService db)
    {
        _db = db;
    }

    [McpServerTool, Description("Query store-specific stock levels. Can filter by store, product, and identify items below a low stock threshold.")]
    public async Task<string> CheckInventory(
        [Description("The store ID to filter inventory by. Optional.")] int? storeId = null,
        [Description("The product ID to filter inventory by. Optional.")] int? productId = null)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = new StringBuilder("""
            SELECT
                i.store_id,
                s.store_name,
                i.product_id,
                p.product_name AS product_name,
                p.sku,
                c.category_name,
                i.stock_level
            FROM retail.inventory i
            JOIN retail.products p ON p.product_id = i.product_id
            JOIN retail.stores s ON s.store_id = i.store_id
            JOIN retail.product_types pt ON pt.type_id = p.type_id
            JOIN retail.categories c ON c.category_id = pt.category_id
            WHERE 1=1
            """);

        var parameters = new List<DbParameter>();

        if (storeId.HasValue)
        {
            sql.Append(" AND i.store_id = @storeId");
            var p = connection.CreateCommand().CreateParameter();
            p.ParameterName = "@storeId";
            p.Value = storeId.Value;
            parameters.Add(p);
        }

        if (productId.HasValue)
        {
            sql.Append(" AND i.product_id = @productId");
            var p = connection.CreateCommand().CreateParameter();
            p.ParameterName = "@productId";
            p.Value = productId.Value;
            parameters.Add(p);
        }

        sql.Append(" ORDER BY i.stock_level ASC");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var p in parameters) cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            results.Add(new Dictionary<string, object?>
            {
                ["store_id"] = reader.GetInt32(0),
                ["store_name"] = reader.GetString(1),
                ["product_id"] = reader.GetInt32(2),
                ["product_name"] = reader.GetString(3),
                ["sku"] = reader.GetString(4),
                ["category_name"] = reader.GetString(5),
                ["quantity"] = reader.GetInt32(6)
            });
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }
}