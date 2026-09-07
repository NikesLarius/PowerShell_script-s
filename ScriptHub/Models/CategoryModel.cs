using System;
using System.Text.Json.Serialization;

namespace ScriptHub.Models;

public class CategoryModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "Folder24";
    public string ColorHex { get; set; } = "#0078D4";
    public bool IsSystem { get; set; } = false;

    public override string ToString() => Name;
}
