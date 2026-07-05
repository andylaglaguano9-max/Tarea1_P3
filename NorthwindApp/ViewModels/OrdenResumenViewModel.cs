namespace NorthwindApp.ViewModels;

/// <summary>
/// Resumen de una orden confirmada para mostrar al usuario.
/// </summary>
public class OrdenResumenViewModel
{
    public short OrderId { get; set; }
    public DateOnly? OrderDate { get; set; }
    public List<CarritoItemViewModel> Items { get; set; } = new();
    public float Total => Items.Sum(i => i.Subtotal);
    public int TotalProductos => Items.Sum(i => i.Quantity);
}
