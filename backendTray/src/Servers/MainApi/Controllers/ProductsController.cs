using MainApi.Data;
using MainApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MainApi.Controllers;

/// <summary>資料來源是 SQL Server 的範例 API（dbo.Products）。</summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController(ProductRepository products) : ControllerBase
{
    /// <summary>查詢，可依名稱關鍵字、分類、是否上架篩選，並分頁。</summary>
    [HttpGet]
    public Task<PagedResult<Product>> Search([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        products.SearchAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> Get(int id, CancellationToken cancellationToken) =>
        await products.GetAsync(id, cancellationToken) is { } product ? product : NotFound();

    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductInput input, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, ProductInput input, CancellationToken cancellationToken) =>
        await products.UpdateAsync(id, input, cancellationToken) is { } product ? product : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await products.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
