using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NorthwindApp.Data;
using NorthwindApp.Models;
using NorthwindApp.ViewModels;

namespace NorthwindApp.Controllers;

public class ProductsController : Controller
{
    private readonly NorthwindContext _context;

    public ProductsController(NorthwindContext context)
    {
        _context = context;
    }

    // =====================================================================
    // CONSULTA LINQ #3: 10 productos con nombre de categoría usando Include
    // =====================================================================

    /// <summary>
    /// GET: Products/Index
    /// Vista administrativa - muestra todos los productos con sus relaciones.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var productos = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .OrderBy(p => p.ProductName)
            .Take(10)
            .ToListAsync();
        return View(productos);
    }

    // =====================================================================
    // CONSULTA LINQ: Tienda - productos disponibles para compra
    // =====================================================================

    /// <summary>
    /// GET: Products/Tienda
    /// Vista de tienda para Customer y Admin.
    /// Muestra solo productos disponibles (no descontinuados y con stock > 0).
    /// Incluye búsqueda por nombre y ordenamiento.
    /// </summary>
    [Authorize(Roles = "Customer,Admin")]
    public async Task<IActionResult> Tienda(string? buscar, string? ordenar)
    {
        // Consulta LINQ: productos disponibles (no descontinuados, stock > 0)
        var query = _context.Products
            .Include(p => p.Category)
            .Where(p => p.Discontinued == 0 && p.UnitsInStock > 0)
            .AsQueryable();

        // Consulta LINQ: búsqueda por nombre
        if (!string.IsNullOrEmpty(buscar))
        {
            query = query.Where(p => p.ProductName.Contains(buscar));
        }

        // Consulta LINQ: ordenamiento por nombre o precio
        query = ordenar switch
        {
            "precio_asc" => query.OrderBy(p => p.UnitPrice),
            "precio_desc" => query.OrderByDescending(p => p.UnitPrice),
            _ => query.OrderBy(p => p.ProductName)
        };

        var productos = await query.ToListAsync();

        ViewData["Buscar"] = buscar;
        ViewData["Ordenar"] = ordenar;

        return View(productos);
    }

    // GET: Products/Details/5
    public async Task<IActionResult> Details(short? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(m => m.ProductId == id);

        if (product == null) return NotFound();
        return View(product);
    }

    // GET: Products/Create
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName");
        ViewData["SupplierId"] = new SelectList(_context.Suppliers, "SupplierId", "CompanyName");
        return View();
    }

    // POST: Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("ProductId,ProductName,SupplierId,CategoryId,QuantityPerUnit,UnitPrice,UnitsInStock,UnitsOnOrder,ReorderLevel,Discontinued")] Product product)
    {
        if (ModelState.IsValid)
        {
            _context.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
        ViewData["SupplierId"] = new SelectList(_context.Suppliers, "SupplierId", "CompanyName", product.SupplierId);
        return View(product);
    }

    // GET: Products/Edit/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(short? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
        ViewData["SupplierId"] = new SelectList(_context.Suppliers, "SupplierId", "CompanyName", product.SupplierId);
        return View(product);
    }

    // POST: Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(short id, [Bind("ProductId,ProductName,SupplierId,CategoryId,QuantityPerUnit,UnitPrice,UnitsInStock,UnitsOnOrder,ReorderLevel,Discontinued")] Product product)
    {
        if (id != product.ProductId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(product);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.ProductId)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
        ViewData["SupplierId"] = new SelectList(_context.Suppliers, "SupplierId", "CompanyName", product.SupplierId);
        return View(product);
    }

    // GET: Products/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(short? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(m => m.ProductId == id);

        if (product == null) return NotFound();
        return View(product);
    }

    // POST: Products/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(short id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null) _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // =====================================================================
    // GESTIÓN DE INVENTARIO (Solo Admin)
    // =====================================================================

    /// <summary>
    /// GET: Products/AjustarStock/5
    /// Muestra el formulario para incrementar o reducir el stock de un producto.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AjustarStock(short? id)
    {
        if (id == null) return NotFound();

        // Consulta LINQ: buscar producto por ID
        var producto = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (producto == null) return NotFound();

        var vm = new AjusteStockViewModel
        {
            ProductId = producto.ProductId,
            ProductName = producto.ProductName,
            StockActual = producto.UnitsInStock ?? 0,
            Tipo = "Incremento"
        };

        return View(vm);
    }

    /// <summary>
    /// POST: Products/AjustarStock
    /// Valida y aplica el ajuste de stock (incremento o reducción).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AjustarStock(AjusteStockViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        // Validación: cantidad debe ser positiva
        if (vm.Cantidad <= 0)
        {
            ModelState.AddModelError("Cantidad", "La cantidad debe ser mayor que cero.");
            return View(vm);
        }

        // Consulta LINQ: obtener producto actualizado desde la DB
        var producto = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == vm.ProductId);

        if (producto == null) return NotFound();

        short stockActual = producto.UnitsInStock ?? 0;

        if (vm.Tipo == "Incremento")
        {
            producto.UnitsInStock = (short)(stockActual + vm.Cantidad);
            TempData["Exito"] = $"Stock incrementado. Nuevo stock de '{producto.ProductName}': {producto.UnitsInStock} unidades.";
        }
        else if (vm.Tipo == "Reduccion")
        {
            // Validación: no puede quedar stock negativo
            if (vm.Cantidad > stockActual)
            {
                ModelState.AddModelError("Cantidad", $"No se puede reducir {vm.Cantidad} unidades. Stock disponible: {stockActual}.");
                vm.StockActual = stockActual;
                return View(vm);
            }
            producto.UnitsInStock = (short)(stockActual - vm.Cantidad);
            TempData["Exito"] = $"Stock reducido. Nuevo stock de '{producto.ProductName}': {producto.UnitsInStock} unidades.";
        }
        else
        {
            ModelState.AddModelError("Tipo", "Tipo de ajuste inválido.");
            return View(vm);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // =====================================================================
    // CONSULTA LINQ: Productos con bajo stock
    // =====================================================================

    /// <summary>
    /// GET: Products/BajoStock
    /// Muestra productos cuyo stock es menor o igual al nivel de reorden,
    /// pero aún tienen existencias. Solo para Admin.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BajoStock()
    {
        // Consulta LINQ: productos con stock <= ReorderLevel y stock > 0
        var productos = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.UnitsInStock <= p.ReorderLevel && p.UnitsInStock > 0)
            .OrderBy(p => p.UnitsInStock)
            .ToListAsync();

        return View(productos);
    }

    // =====================================================================
    // CONSULTA LINQ: Productos sin existencias
    // =====================================================================

    /// <summary>
    /// GET: Products/SinStock
    /// Muestra productos con stock igual a cero. Solo para Admin.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SinStock()
    {
        // Consulta LINQ: productos con stock = 0
        var productos = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.UnitsInStock == 0 || p.UnitsInStock == null)
            .OrderBy(p => p.ProductName)
            .ToListAsync();

        return View(productos);
    }

    // =====================================================================
    // CONSULTA LINQ: Productos descontinuados
    // =====================================================================

    /// <summary>
    /// GET: Products/Descontinuados
    /// Muestra productos marcados como descontinuados (Discontinued = 1).
    /// Solo para Admin.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Descontinuados()
    {
        // Consulta LINQ: productos con Discontinued != 0
        var productos = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Discontinued != 0)
            .OrderBy(p => p.ProductName)
            .ToListAsync();

        return View(productos);
    }

    // =====================================================================
    // CONSULTAS LINQ PREVIAS (mantenidas del examen anterior)
    // =====================================================================

    // Consulta LINQ #1: Los 10 productos más caros (OrderByDescending + Take)
    public async Task<IActionResult> MasCaros()
    {
        var productos = await _context.Products
            .Include(p => p.Category)
            .OrderByDescending(p => p.UnitPrice)
            .Take(10)
            .ToListAsync();
        return View(productos);
    }

    // Consulta LINQ #2: Productos cuyo nombre contenga "Ch" (Where con condición)
    public async Task<IActionResult> BuscarPorNombre()
    {
        string palabraBusqueda = "Ch";
        var productos = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.ProductName.Contains(palabraBusqueda))
            .OrderBy(p => p.ProductName)
            .ToListAsync();
        ViewData["PalabraBusqueda"] = palabraBusqueda;
        return View(productos);
    }

    // Consulta LINQ #4: 10 productos con nombre de proveedor usando Include
    public async Task<IActionResult> ProductosPorProveedor()
    {
        var productos = await _context.Products
            .Include(p => p.Supplier)
            .OrderBy(p => p.ProductName)
            .Take(10)
            .ToListAsync();
        return View(productos);
    }

    // Consulta LINQ #5: Productos de una categoría específica usando Join
    public async Task<IActionResult> ProductosPorCategoria()
    {
        var resultado = await (from p in _context.Products
                               join c in _context.Categories on p.CategoryId equals c.CategoryId
                               where c.CategoryName == "Beverages"
                               orderby p.ProductName
                               select new ProductoCategoriaViewModel
                               {
                                   ProductName = p.ProductName,
                                   UnitPrice = p.UnitPrice,
                                   UnitsInStock = p.UnitsInStock,
                                   CategoryName = c.CategoryName
                               }).ToListAsync();
        return View(resultado);
    }

    // Consulta LINQ #6: Productos de un proveedor específico (Where + condición compuesta)
    public async Task<IActionResult> ProductosProveedorEspecifico()
    {
        var productos = await _context.Products
            .Include(p => p.Supplier)
            .Include(p => p.Category)
            .Where(p => p.Supplier != null && p.Supplier.Country == "USA" && p.UnitPrice > 10)
            .OrderByDescending(p => p.UnitPrice)
            .ToListAsync();
        return View(productos);
    }

    private bool ProductExists(short id)
    {
        return _context.Products.Any(e => e.ProductId == id);
    }
}

// ViewModel para la consulta con Join (mantenido del examen anterior)
public class ProductoCategoriaViewModel
{
    public string ProductName { get; set; } = null!;
    public float? UnitPrice { get; set; }
    public short? UnitsInStock { get; set; }
    public string CategoryName { get; set; } = null!;
}
