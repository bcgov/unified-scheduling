using System.Text.Json.Serialization;

namespace Unified.Db.Models.UserManagement;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Gender
{
    Male,
    Female,
    Other,
}