using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NorthwindApp.Data;
using NorthwindApp.Models;
using NorthwindApp.ViewModels;
using System.Text.Json;

namespace NorthwindApp.Controllers;

/// <summary>
/// Controlador del carrito de compras y confirmación de órdenes.
/// Accesible para usuarios con rol Customer o Admin.
/// </summary>
[Authorize(Roles = "Customer,Admin")]
public class ShoppingCartController : Controller
{
    private readonly NorthwindContext _context;
    private const string CartSessionKey = "ShoppingCart";

    public ShoppingCartController(NorthwindContext context)
    {
        _context = context;
    }

    // =====================================================================
    // HELPERS DE SESIÓN
    // =====================================================================

    /// <summary>Obtiene el carrito desde la sesión.</summary>
    private CarritoViewModel ObtenerCarrito()
    {
        var json = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
            return new CarritoViewModel();
        return JsonSerializer.Deserialize<CarritoViewModel>(json) ?? new CarritoViewModel();
    }

    /// <summary>Guarda el carrito en la sesión.</summary>
    private void GuardarCarrito(CarritoViewModel carrito)
    {
        var json = JsonSerializer.Serialize(carrito);
        HttpContext.Session.SetString(CartSessionKey, json);
    }

    // =====================================================================
    // CONSULTA LINQ: Ver carrito actual
    // =====================================================================

    /// <summary>
    /// GET: ShoppingCart/Index
    /// Muestra todos los productos agregados al carrito con subtotales y total.
    /// </summary>
    public IActionResult Index()
    {
        var carrito = ObtenerCarrito();
        return View(carrito);
    }

    // =====================================================================
    // CONSULTA LINQ: Agregar producto al carrito con validación de stock
    // =====================================================================

    /// <summary>
    /// POST: ShoppingCart/AgregarProducto
    /// Valida el producto y la cantidad antes de agregarlo al carrito.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarProducto(short productId, short cantidad)
    {
        // Validación: cantidad debe ser mayor que 0
        if (cantidad <= 0)
        {
            TempData["Error"] = "La cantidad debe ser mayor que cero.";
            return RedirectToAction("Tienda", "Products");
        }

        // Consulta LINQ: Verificar que el producto existe en la base de datos
        var producto = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (producto == null)
        {
            TempData["Error"] = "El producto no existe.";
            return RedirectToAction("Tienda", "Products");
        }

        // Validación: producto descontinuado
        if (producto.Discontinued != 0)
        {
            TempData["Error"] = $"El producto '{producto.ProductName}' está descontinuado y no puede comprarse.";
            return RedirectToAction("Tienda", "Products");
        }

        // Validación: stock disponible
        short stockDisponible = producto.UnitsInStock ?? 0;
        if (stockDisponible <= 0)
        {
            TempData["Error"] = $"El producto '{producto.ProductName}' no tiene stock disponible.";
            return RedirectToAction("Tienda", "Products");
        }

        var carrito = ObtenerCarrito();

        // Verificar si ya está en el carrito
        var itemExistente = carrito.Items.FirstOrDefault(i => i.ProductId == productId);
        short cantidadTotal = cantidad;
        if (itemExistente != null)
            cantidadTotal += itemExistente.Quantity;

        // Validación: cantidad total no supera el stock
        if (cantidadTotal > stockDisponible)
        {
            TempData["Error"] = $"Stock insuficiente. Disponible: {stockDisponible} unidades. Intentó agregar: {cantidadTotal} unidades.";
            return RedirectToAction("Tienda", "Products");
        }

        if (itemExistente != null)
        {
            itemExistente.Quantity = cantidadTotal;
        }
        else
        {
            carrito.Items.Add(new CarritoItemViewModel
            {
                ProductId = productId,
                ProductName = producto.ProductName,
                UnitPrice = producto.UnitPrice ?? 0,
                Quantity = cantidad
            });
        }

        GuardarCarrito(carrito);
        TempData["Exito"] = $"'{producto.ProductName}' agregado al carrito.";
        return RedirectToAction("Tienda", "Products");
    }

    // =====================================================================
    // Actualizar cantidad de un producto en el carrito
    // =====================================================================

    /// <summary>
    /// POST: ShoppingCart/ActualizarCantidad
    /// Modifica la cantidad de un producto ya agregado al carrito.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarCantidad(short productId, short cantidad)
    {
        if (cantidad <= 0)
        {
            TempData["Error"] = "La cantidad debe ser mayor que cero.";
            return RedirectToAction("Index");
        }

        // Re-verificar stock desde la base de datos
        var producto = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (producto == null)
        {
            TempData["Error"] = "Producto no encontrado.";
            return RedirectToAction("Index");
        }

        short stockDisponible = producto.UnitsInStock ?? 0;
        if (cantidad > stockDisponible)
        {
            TempData["Error"] = $"Stock insuficiente. Disponible: {stockDisponible} unidades.";
            return RedirectToAction("Index");
        }

        var carrito = ObtenerCarrito();
        var item = carrito.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            item.Quantity = cantidad;
            item.UnitPrice = producto.UnitPrice ?? 0; // Precio actualizado desde DB
        }

        GuardarCarrito(carrito);
        TempData["Exito"] = "Cantidad actualizada.";
        return RedirectToAction("Index");
    }

    // =====================================================================
    // Eliminar producto del carrito
    // =====================================================================

    /// <summary>
    /// POST: ShoppingCart/EliminarProducto
    /// Elimina un producto del carrito antes de confirmar la compra.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarProducto(short productId)
    {
        var carrito = ObtenerCarrito();
        carrito.Items.RemoveAll(i => i.ProductId == productId);
        GuardarCarrito(carrito);
        TempData["Exito"] = "Producto eliminado del carrito.";
        return RedirectToAction("Index");
    }

    // =====================================================================
    // TRANSACCIÓN: Confirmar compra
    // =====================================================================

    /// <summary>
    /// POST: ShoppingCart/Confirmar
    /// Ejecuta la transacción completa: crea Order, OrderDetails y actualiza stock.
    /// Usa transacción de base de datos para garantizar atomicidad.
    /// Si ocurre un error, se revierte todo (Rollback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar()
    {
        var carrito = ObtenerCarrito();

        // Validación: carrito no puede estar vacío
        if (carrito.EstaVacio)
        {
            TempData["Error"] = "No puedes confirmar una compra sin productos.";
            return RedirectToAction("Index");
        }

        // Iniciar transacción de base de datos
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Re-verificar stock para cada producto antes de confirmar
            foreach (var item in carrito.Items)
            {
                // Consulta LINQ: obtener producto actualizado desde la base de datos
                var productoDB = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (productoDB == null)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"El producto '{item.ProductName}' ya no existe en el catálogo.";
                    return RedirectToAction("Index");
                }

                if (productoDB.Discontinued != 0)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"El producto '{productoDB.ProductName}' fue descontinuado.";
                    return RedirectToAction("Index");
                }

                short stockActual = productoDB.UnitsInStock ?? 0;
                if (item.Quantity > stockActual)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Stock insuficiente para '{productoDB.ProductName}'. Disponible: {stockActual}, Solicitado: {item.Quantity}.";
                    return RedirectToAction("Index");
                }
            }

            // Generar nuevo OrderId = MAX(order_id) + 1
            // Consulta LINQ: obtener el máximo ID existente
            short maxOrderId = await _context.Orders
                .MaxAsync(o => (short?)o.OrderId) ?? 0;
            short nuevoOrderId = (short)(maxOrderId + 1);

            string userEmail = User.Identity?.Name ?? "guest";

            // Crear la nueva orden
            var orden = new Order
            {
                OrderId = nuevoOrderId,
                OrderDate = DateOnly.FromDateTime(DateTime.Today),
                RequiredDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                ShipName = userEmail,   // Usamos ShipName para rastrear el usuario
                ShipAddress = "Online Order",
                ShipCity = "N/A",
                ShipCountry = "EC",
                Freight = 0
            };
            _context.Orders.Add(orden);
            await _context.SaveChangesAsync();

            // Crear los OrderDetails y actualizar el stock
            foreach (var item in carrito.Items)
            {
                // Consulta LINQ: precio siempre desde la base de datos (nunca del browser)
                var productoDB = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (productoDB == null) continue;

                // Crear detalle de la orden
                var detalle = new OrderDetail
                {
                    OrderId = nuevoOrderId,
                    ProductId = item.ProductId,
                    UnitPrice = productoDB.UnitPrice ?? 0,  // Precio desde DB
                    Quantity = item.Quantity,
                    Discount = 0   // Sin descuento
                };
                _context.OrderDetails.Add(detalle);

                // Actualizar el stock: UnitsInStock - Quantity
                productoDB.UnitsInStock = (short)((productoDB.UnitsInStock ?? 0) - item.Quantity);
            }

            await _context.SaveChangesAsync();

            // Confirmar la transacción
            await transaction.CommitAsync();

            // Guardar resumen para mostrar
            var resumen = new OrdenResumenViewModel
            {
                OrderId = nuevoOrderId,
                OrderDate = orden.OrderDate,
                Items = new List<CarritoItemViewModel>(carrito.Items)
            };

            // Limpiar carrito de la sesión
            HttpContext.Session.Remove(CartSessionKey);

            return View("Resumen", resumen);
        }
        catch (Exception)
        {
            // Revertir toda la transacción en caso de error
            await transaction.RollbackAsync();
            TempData["Error"] = "Ocurrió un error al procesar la compra. La operación fue revertida. Por favor intente de nuevo.";
            return RedirectToAction("Index");
        }
    }
}
