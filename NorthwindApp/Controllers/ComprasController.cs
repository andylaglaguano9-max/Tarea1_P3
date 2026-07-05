using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NorthwindApp.Data;

namespace NorthwindApp.Controllers;

/// <summary>
/// Controlador para que el Cliente consulte sus órdenes realizadas.
/// Accesible solo para el rol Customer.
/// </summary>
[Authorize(Roles = "Customer")]
public class ComprasController : Controller
{
    private readonly NorthwindContext _context;

    public ComprasController(NorthwindContext context)
    {
        _context = context;
    }

    // =====================================================================
    // CONSULTA LINQ: Órdenes del usuario autenticado
    // =====================================================================

    /// <summary>
    /// GET: Compras/MisOrdenes
    /// Lista todas las órdenes del usuario actual (identificado por su email en ShipName).
    /// </summary>
    public async Task<IActionResult> MisOrdenes()
    {
        string userEmail = User.Identity?.Name ?? "";

        // Consulta LINQ: filtrar órdenes por email del usuario (guardado en ShipName)
        var ordenes = await _context.Orders
            .Where(o => o.ShipName == userEmail)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(ordenes);
    }

    // =====================================================================
    // CONSULTA LINQ: Detalle de una orden del cliente
    // =====================================================================

    /// <summary>
    /// GET: Compras/DetalleOrden/5
    /// Muestra el detalle completo de una orden del usuario actual,
    /// incluyendo los productos, cantidades, precios y subtotales.
    /// </summary>
    public async Task<IActionResult> DetalleOrden(short? id)
    {
        if (id == null) return NotFound();

        string userEmail = User.Identity?.Name ?? "";

        // Consulta LINQ: obtener la orden con sus detalles y productos relacionados
        var orden = await _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.ShipName == userEmail);

        if (orden == null)
        {
            TempData["Error"] = "La orden no fue encontrada o no tienes permiso para verla.";
            return RedirectToAction("MisOrdenes");
        }

        // Calcular el total de la orden usando LINQ
        // Total = Suma de (UnitPrice * Quantity) para cada OrderDetail
        ViewData["TotalOrden"] = orden.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);

        return View(orden);
    }
}
