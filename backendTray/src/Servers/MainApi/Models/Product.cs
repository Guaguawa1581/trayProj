using System.ComponentModel.DataAnnotations;

namespace MainApi.Models;

/// <summary>dbo.Products 的一筆資料，欄位名稱與資料表相同（Dapper 依名稱對應）。</summary>
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Category { get; set; } = "";

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新增與修改時的內容。長度與範圍跟資料表的欄位定義一致。</summary>
public class ProductInput
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, StringLength(50)]
    public string Category { get; set; } = "";

    [Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>查詢條件，全部都可省略。</summary>
public class ProductQuery
{
    /// <summary>名稱包含這段文字。</summary>
    public string? Keyword { get; set; }

    public string? Category { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
