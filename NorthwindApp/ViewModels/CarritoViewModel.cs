namespace NorthwindApp.ViewModels;

/// <summary>
/// Representa el carrito de compras completo con todos los ítems.
/// </summary>
public class CarritoViewModel
{
    public List<CarritoItemViewModel> Items { get; set; } = new();
    public float Total => Items.Sum(i => i.Subtotal);
    public int TotalProductos => Items.Sum(i => i.Quantity);
    public bool EstaVacio => Items.Count == 0;
}
