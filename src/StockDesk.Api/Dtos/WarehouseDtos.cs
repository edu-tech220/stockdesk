using System.ComponentModel.DataAnnotations;

namespace StockDesk.Api.Dtos;

public record WarehouseDto(int Id, string Code, string Name, string Location, bool IsActive);

public class CreateWarehouseRequest
{
    public CreateWarehouseRequest()
    {
    }

    public CreateWarehouseRequest(string code, string name, string? location)
    {
        Code = code;
        Name = name;
        Location = location;
    }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }
}
