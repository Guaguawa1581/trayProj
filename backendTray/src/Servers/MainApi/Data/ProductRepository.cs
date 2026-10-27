using Dapper;
using MainApi.Models;

namespace MainApi.Data;

/// <summary>dbo.Products 的讀寫。資料表由 Database\CreateTestData.sql 建立。</summary>
public sealed class ProductRepository(SqlConnectionFactory connections)
{
    private const string Columns = "Id, Name, Category, Price, Stock, IsActive, CreatedAt, UpdatedAt";

    public async Task<PagedResult<Product>> SearchAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        const string where = """
            WHERE (@Keyword IS NULL OR Name LIKE N'%' + @Keyword + N'%')
              AND (@Category IS NULL OR Category = @Category)
              AND (@IsActive IS NULL OR IsActive = @IsActive)
            """;
        const string sql = $"""
            SELECT COUNT(*) FROM dbo.Products {where};

            SELECT {Columns} FROM dbo.Products {where}
            ORDER BY Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            query.Keyword,
            query.Category,
            query.IsActive,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize,
        };

        await using var connection = connections.Create();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<Product>()).AsList();
        return new PagedResult<Product>(items, total, query.Page, query.PageSize);
    }

    public async Task<Product?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Products WHERE Id = @id",
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<Product> CreateAsync(ProductInput input, CancellationToken cancellationToken)
    {
        const string sql = $"""
            INSERT INTO dbo.Products (Name, Category, Price, Stock, IsActive)
            VALUES (@Name, @Category, @Price, @Stock, @IsActive);

            SELECT {Columns} FROM dbo.Products WHERE Id = SCOPE_IDENTITY();
            """;

        await using var connection = connections.Create();
        return await connection.QuerySingleAsync<Product>(new CommandDefinition(sql, input, cancellationToken: cancellationToken));
    }

    /// <returns>修改後的資料；找不到時回傳 null。</returns>
    public async Task<Product?> UpdateAsync(int id, ProductInput input, CancellationToken cancellationToken)
    {
        const string sql = $"""
            UPDATE dbo.Products
            SET Name = @Name, Category = @Category, Price = @Price, Stock = @Stock, IsActive = @IsActive, UpdatedAt = SYSDATETIME()
            WHERE Id = @Id;

            SELECT {Columns} FROM dbo.Products WHERE Id = @Id;
            """;

        var parameters = new { Id = id, input.Name, input.Category, input.Price, input.Stock, input.IsActive };

        await using var connection = connections.Create();
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <returns>有刪除到資料時回傳 true。</returns>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.Products WHERE Id = @id",
            new { id },
            cancellationToken: cancellationToken));
        return affected > 0;
    }
}
