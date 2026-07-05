namespace NorthwindApp.ViewModels;

/// <summary>
/// Representa un ítem dentro del carrito de compras.
/// </summary>
public class CarritoItemViewModel
{
    public short ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public float UnitPrice { get; set; }
    public short Quantity { get; set; }
    public float Subtotal => UnitPrice * Quantity;
}
