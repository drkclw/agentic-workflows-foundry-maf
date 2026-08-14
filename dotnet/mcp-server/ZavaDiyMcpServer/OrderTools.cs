using System.ComponentModel;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace ZavaDiyMcpServer;

[McpServerToolType]
public class OrderTools
{
    private readonly IDatabaseService _db;

    public OrderTools(IDatabaseService db)
    {
        _db = db;
    }

    [McpServerTool, Description("Retrieve orders with optional filters by customer, store, or date range. Returns order details including items, products, and totals.")]
    public async Task<string> GetOrders(
        [Description("Customer ID to filter orders by. Optional.")] int? customerId = null,
        [Description("Store ID to filter orders by. Optional.")] int? storeId = null,
        [Description("Start date for the date range filter (inclusive), in yyyy-MM-dd format. Optional.")] string? startDate = null,
        [Description("End date for the date range filter (inclusive), in yyyy-MM-dd format. Optional.")] string? endDate = null,
        [Description("Maximum number of orders to return. Defaults to 50.")] int limit = 50)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = new StringBuilder("""
            SELECT
                o.order_id,
                o.customer_id,
                cu.first_name || ' ' || cu.last_name AS customer_name,
                o.store_id,
                s.store_name,
                o.order_date,
                oi.product_id,
                p.product_name,
                p.sku,
                oi.quantity,
                oi.unit_price,
                oi.discount_percent,
                oi.total_amount AS line_total
            FROM retail.orders o
            JOIN retail.order_items oi ON oi.order_id = o.order_id
            JOIN retail.products p ON p.product_id = oi.product_id
            JOIN retail.stores s ON s.store_id = o.store_id
            JOIN retail.customers cu ON cu.customer_id = o.customer_id
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

        if (customerId.HasValue)
        {
            sql.Append(" AND o.customer_id = @customerId");
            parameters.Add(CreateParam("@customerId", customerId.Value));
        }

        if (storeId.HasValue)
        {
            sql.Append(" AND o.store_id = @storeId");
            parameters.Add(CreateParam("@storeId", storeId.Value));
        }

        if (startDate is not null)
        {
            sql.Append(" AND o.order_date >= @startDate");
            parameters.Add(CreateParam("@startDate", DateOnly.Parse(startDate)));
        }

        if (endDate is not null)
        {
            sql.Append(" AND o.order_date <= @endDate");
            parameters.Add(CreateParam("@endDate", DateOnly.Parse(endDate)));
        }

        sql.Append(" ORDER BY o.order_date DESC, o.order_id, oi.product_id");
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
                ["order_id"] = reader.GetInt32(0),
                ["customer_id"] = reader.GetInt32(1),
                ["customer_name"] = reader.GetString(2),
                ["store_id"] = reader.GetInt32(3),
                ["store_name"] = reader.GetString(4),
                ["order_date"] = reader.GetFieldValue<DateOnly>(5).ToString("yyyy-MM-dd"),
                ["product_id"] = reader.GetInt32(6),
                ["product_name"] = reader.GetString(7),
                ["sku"] = reader.GetString(8),
                ["quantity"] = reader.GetInt32(9),
                ["unit_price"] = reader.GetDecimal(10),
                ["discount_percent"] = reader.GetDecimal(11),
                ["line_total"] = reader.GetDecimal(12)
            });
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }
}
