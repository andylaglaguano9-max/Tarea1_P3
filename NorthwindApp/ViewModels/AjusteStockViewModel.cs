using System.ComponentModel.DataAnnotations;

namespace NorthwindApp.ViewModels;

/// <summary>
/// ViewModel para el formulario de ajuste de stock (Admin).
/// </summary>
public class AjusteStockViewModel
{
    public short ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public short StockActual { get; set; }

    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(1, short.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public short Cantidad { get; set; }

    /// <summary>Incremento o Reduccion</summary>
    [Required]
    public string Tipo { get; set; } = "Incremento";
}
