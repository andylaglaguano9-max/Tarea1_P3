using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NorthwindApp.Data;
using NorthwindApp.Models;

namespace NorthwindApp.Controllers;

/// <summary>
/// Controlador de órdenes para el rol Admin.
/// Permite consultar todas las órdenes, sus detalles y los productos más comprados.
/// </summary>
[Authorize(Roles = "Admin")]
public class OrdersController : Controller
{
    private readonly NorthwindContext _context;

    public OrdersController(NorthwindContext context)
    {
        _context = context;
    }

    // =====================================================================
    // CONSULTA LINQ #7: Todas las órdenes (sin límite) para Admin
    // =====================================================================

    /// <summary>
    /// GET: Orders/Index
    /// Muestra todas las órdenes registradas, ordenadas por fecha descendente.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Consulta LINQ: todas las órdenes con cliente y empleado, ordenadas por fecha
        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Employee)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        return View(orders);
    }

    // =====================================================================
    // CONSULTA LINQ: Detalle de orden con productos y subtotales
    // =====================================================================

    /// <summary>
    /// GET: Orders/Details/5
    /// Muestra el detalle completo de una orden con sus productos y cálculo de total.
    /// </summary>
    public async Task<IActionResult> Details(short? id)
    {
        if (id == null) return NotFound();

        // Consulta LINQ: orden con detalles y productos relacionados
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Employee)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .FirstOrDefaultAsync(m => m.OrderId == id);

        if (order == null) return NotFound();

        // Consulta LINQ: calcular total de la orden = suma de subtotales
        ViewData["TotalOrden"] = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);

        return View(order);
    }

    // =====================================================================
    // CONSULTA LINQ: Productos más comprados (GroupBy + Sum + OrderByDescending)
    // =====================================================================

    /// <summary>
    /// GET: Orders/ProductosMasComprados
    /// Consulta LINQ avanzada: agrupa los detalles de órdenes por producto
    /// y calcula el total de unidades vendidas de cada uno.
    /// </summary>
    public async Task<IActionResult> ProductosMasComprados()
    {
        // Consulta LINQ: GroupBy sobre OrderDetails para obtener los más vendidos
        var resultado = await _context.OrderDetails
            .Include(od => od.Product)
            .GroupBy(od => new { od.ProductId, od.Product.ProductName })
            .Select(g => new ProductoMasCompradoViewModel
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalVendido = g.Sum(x => (int)x.Quantity),
                TotalIngresos = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.TotalVendido)
            .Take(10)
            .ToListAsync();

        return View(resultado);
    }
}

/// <summary>
/// ViewModel para la consulta de productos más comprados.
/// </summary>
public class ProductoMasCompradoViewModel
{
    public short ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int TotalVendido { get; set; }
    public float TotalIngresos { get; set; }
}
